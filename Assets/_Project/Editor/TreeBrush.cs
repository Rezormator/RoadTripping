using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TreeBrush : EditorWindow
{
    public List<GameObject> prefabs = new List<GameObject>();
    public float radius = 8f;
    public int treesPerDab = 6;
    public float minDistance = 2f;
    public Vector2 scaleRange = new Vector2(0.85f, 1.2f);
    public Vector2 yawRange = new Vector2(0f, 360f);
    public bool alignToSurface = false;
    public float randomTilt = 0f;
    public float sink = 0f;
    public bool restrictToLayers = false;
    public float minLayerWeight = 0.5f;
    public List<TerrainLayer> allowedLayers = new List<TerrainLayer>();

    bool active;
    Vector3 lastDab;
    Vector2 scroll;
    const string ContainerName = "TreeBrush_Objects";

    [MenuItem("Tools/Tree Brush")]
    static void Open() { GetWindow<TreeBrush>("Tree Brush"); }

    void OnEnable() { SceneView.duringSceneGui += OnScene; }
    void OnDisable() { SceneView.duringSceneGui -= OnScene; }

    void MinMax(string label, ref Vector2 range, float min, float max)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(110));
        range.x = EditorGUILayout.FloatField(range.x, GUILayout.Width(45));
        EditorGUILayout.MinMaxSlider(ref range.x, ref range.y, min, max);
        range.y = EditorGUILayout.FloatField(range.y, GUILayout.Width(45));
        EditorGUILayout.EndHorizontal();
        range.x = Mathf.Clamp(range.x, min, range.y);
        range.y = Mathf.Clamp(range.y, range.x, max);
    }

    bool LayerAllowed(TerrainData td, float nx, float nz)
    {
        if (!restrictToLayers || allowedLayers.Count == 0) return true;
        var layers = td.terrainLayers;
        if (layers == null || layers.Length == 0) return true;

        int res = td.alphamapResolution;
        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (res - 1)), 0, res - 1);
        int az = Mathf.Clamp(Mathf.RoundToInt(nz * (res - 1)), 0, res - 1);
        float[,,] a = td.GetAlphamaps(ax, az, 1, 1);

        float w = 0f;
        for (int i = 0; i < layers.Length && i < a.GetLength(2); i++)
            if (layers[i] != null && allowedLayers.Contains(layers[i])) w += a[0, 0, i];
        return w >= minLayerWeight;
    }
    
    void OnGUI()
    {
        Rect drop = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Перетягни сюди префаби");
        Event e = Event.current;
        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && drop.Contains(e.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences)
                    if (o is GameObject g && PrefabUtility.IsPartOfPrefabAsset(g) && !prefabs.Contains(g))
                        prefabs.Add(g);
            }
            e.Use();
        }

        EditorGUILayout.LabelField("Префабів у пензлі: " + prefabs.Count, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(180));
        for (int i = prefabs.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(24))) prefabs.RemoveAt(i);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Очистити список")) prefabs.Clear();

        EditorGUILayout.Space();
        radius = EditorGUILayout.Slider("Brush Size", radius, 1f, 100f);
        treesPerDab = EditorGUILayout.IntSlider("Density (per dab)", treesPerDab, 1, 60);
        minDistance = EditorGUILayout.Slider("Min Distance", minDistance, 0f, 15f);
        MinMax("Scale", ref scaleRange, 0.1f, 3f);
        MinMax("Rotation Y", ref yawRange, 0f, 360f);

        EditorGUILayout.Space();
        alignToSurface = EditorGUILayout.Toggle("Align To Surface", alignToSurface);
        if (alignToSurface)
        {
            EditorGUI.indentLevel++;
            randomTilt = EditorGUILayout.Slider("Random Tilt", randomTilt, 0f, 30f);
            sink = EditorGUILayout.Slider("Sink (м)", sink, 0f, 1f);
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox("Об'єкти ставляться як звичайні префаби в сцені (не Terrain-дерева), бо Terrain-дерево не вміє нахилятись.", MessageType.None);
        }
        
        EditorGUILayout.Space();
        restrictToLayers = EditorGUILayout.Toggle("Restrict To Layers", restrictToLayers);
        if (restrictToLayers)
        {
            EditorGUI.indentLevel++;
            minLayerWeight = EditorGUILayout.Slider("Min Layer Weight", minLayerWeight, 0.05f, 1f);
            var tt = Terrain.activeTerrain;
            if (tt == null)
                EditorGUILayout.HelpBox("Виділи Terrain у сцені, щоб побачити його шари.", MessageType.None);
            else
                foreach (var l in tt.terrainData.terrainLayers)
                {
                    if (l == null) continue;
                    bool on = allowedLayers.Contains(l);
                    bool now = EditorGUILayout.ToggleLeft(l.name, on);
                    if (now && !on) allowedLayers.Add(l);
                    if (!now && on) allowedLayers.Remove(l);
                }
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.HelpBox("ЛКМ: малювати. Shift + ЛКМ: стирати об'єкти зі списку. Alt: камера.", MessageType.Info);
        active = GUILayout.Toggle(active, active ? "Painting: ON" : "Painting: OFF", "Button", GUILayout.Height(30));
    }

    bool RaycastTerrain(Event e, out Terrain terrain, out RaycastHit hit)
    {
        terrain = null; hit = default;
        var hits = Physics.RaycastAll(HandleUtility.GUIPointToWorldRay(e.mousePosition));
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            var t = h.collider.GetComponent<Terrain>();
            if (t != null) { terrain = t; hit = h; return true; }
        }
        return false;
    }

    void OnScene(SceneView sv)
    {
        if (!active || prefabs.Count == 0) return;
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        if (!RaycastTerrain(e, out var terrain, out var hit)) return;

        Handles.color = e.shift ? Color.red : Color.green;
        Handles.DrawWireDisc(hit.point, hit.normal, radius);
        if (e.type == EventType.MouseMove) sv.Repaint();

        if (e.alt || e.button != 0) return;
        if (e.type == EventType.MouseDown)
        {
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Tree Brush");
            lastDab = hit.point;
            if (e.shift) Erase(terrain, hit.point); else Dab(terrain, hit.point);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag)
        {
            if ((hit.point - lastDab).magnitude > radius * 0.5f)
            {
                lastDab = hit.point;
                if (e.shift) Erase(terrain, hit.point); else Dab(terrain, hit.point);
            }
            e.Use();
        }
    }

    Transform GetContainer()
    {
        var go = GameObject.Find(ContainerName);
        if (go == null)
        {
            go = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Tree Brush container");
        }
        return go.transform;
    }

    int GetIndex(TerrainData td, GameObject prefab)
    {
        var protos = new List<TreePrototype>(td.treePrototypes);
        for (int i = 0; i < protos.Count; i++)
            if (protos[i].prefab == prefab) return i;
        protos.Add(new TreePrototype { prefab = prefab }); // у кінець, старі індекси не ламаються
        td.treePrototypes = protos.ToArray();
        return protos.Count - 1;
    }

    void Dab(Terrain t, Vector3 center)
    {
        var td = t.terrainData;
        Vector3 tp = t.transform.position;

        var valid = prefabs.FindAll(p => p != null);
        if (valid.Count == 0) return;

        float reach = radius + minDistance;
        var nearby = new List<Vector2>();
        foreach (var ti in td.treeInstances)
        {
            var w = Vector3.Scale(ti.position, td.size) + tp;
            if (new Vector2(w.x - center.x, w.z - center.z).sqrMagnitude < reach * reach)
                nearby.Add(new Vector2(w.x, w.z));
        }
        var existing = GameObject.Find(ContainerName);
        if (existing != null)
            foreach (Transform c in existing.transform)
                if (new Vector2(c.position.x - center.x, c.position.z - center.z).sqrMagnitude < reach * reach)
                    nearby.Add(new Vector2(c.position.x, c.position.z));

        Transform container = alignToSurface ? GetContainer() : null;

        int placed = 0, attempts = 0;
        while (placed < treesPerDab && attempts < treesPerDab * 4)
        {
            attempts++;
            Vector2 r = Random.insideUnitCircle * radius;
            float wx = center.x + r.x, wz = center.z + r.y;
            float nx = (wx - tp.x) / td.size.x, nz = (wz - tp.z) / td.size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            if (!LayerAllowed(td, nx, nz)) continue;
            
            var p = new Vector2(wx, wz);
            bool tooClose = false;
            foreach (var q in nearby)
                if ((q - p).sqrMagnitude < minDistance * minDistance) { tooClose = true; break; }
            if (tooClose) continue;
            nearby.Add(p);

            var prefab = valid[Random.Range(0, valid.Count)]; // рівні імовірності
            float s = Random.Range(scaleRange.x, scaleRange.y);
            float yaw = Random.Range(yawRange.x, yawRange.y);
            float h = t.SampleHeight(new Vector3(wx, 0, wz)); // відносно позиції Terrain

            if (alignToSurface)
            {
                Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                Quaternion align = Quaternion.FromToRotation(Vector3.up, normal);
                Quaternion local = Quaternion.Euler(Random.Range(-randomTilt, randomTilt), yaw,
                                                    Random.Range(-randomTilt, randomTilt));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(container, false);
                go.transform.position = new Vector3(wx, h + tp.y - sink * s, wz);
                go.transform.rotation = align * local;
                go.transform.localScale = Vector3.one * s;
                Undo.RegisterCreatedObjectUndo(go, "Tree Brush object");
            }
            else
            {
                t.AddTreeInstance(new TreeInstance
                {
                    prototypeIndex = GetIndex(td, prefab),
                    position = new Vector3(nx, h / td.size.y, nz),
                    widthScale = s,
                    heightScale = s,
                    rotation = yaw * Mathf.Deg2Rad,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
            placed++;
        }
        EditorUtility.SetDirty(td);
        t.Flush();
    }

    void Erase(Terrain t, Vector3 center)
    {
        var td = t.terrainData;
        Vector3 tp = t.transform.position;
        float r2 = radius * radius;

        // Terrain-дерева, префаби яких є у списку
        var protos = td.treePrototypes;
        var mine = new HashSet<int>();
        for (int i = 0; i < protos.Length; i++)
            if (prefabs.Contains(protos[i].prefab)) mine.Add(i);

        var keep = new List<TreeInstance>();
        foreach (var ti in td.treeInstances)
        {
            var w = Vector3.Scale(ti.position, td.size) + tp;
            bool inside = new Vector2(w.x - center.x, w.z - center.z).sqrMagnitude <= r2;
            if (!(inside && mine.Contains(ti.prototypeIndex))) keep.Add(ti);
        }
        td.treeInstances = keep.ToArray();

        // об'єкти в сцені, створені цим пензлем
        var container = GameObject.Find(ContainerName);
        if (container != null)
        {
            for (int i = container.transform.childCount - 1; i >= 0; i--)
            {
                var c = container.transform.GetChild(i);
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                if (src == null || !prefabs.Contains(src)) continue;
                if (new Vector2(c.position.x - center.x, c.position.z - center.z).sqrMagnitude <= r2)
                    Undo.DestroyObjectImmediate(c.gameObject);
            }
        }
        EditorUtility.SetDirty(td);
        t.Flush();
    }
}