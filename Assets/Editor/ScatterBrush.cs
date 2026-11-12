using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ScatterBrush : EditorWindow
{
    [Serializable]
    public class Entry
    {
        public GameObject prefab;
        public float weight = 1f;
        [Tooltip("ON: звичайний префаб у сцені (підтримує нахил під поверхню). OFF: Terrain-дерево (лише поворот по Y)")]
        public bool asSceneObject = false;
        [Tooltip("Приблизний радіус основи/крони в метрах, для розрахунку відстаней")]
        public float footprint = 1.5f;
        public Vector2 scaleRange = new Vector2(0.85f, 1.2f);
        public Vector2 yawRange = new Vector2(0f, 360f);
        [Range(0f, 1f)] public float alignToNormal = 0f;   // лише Scene Object
        [Range(0f, 45f)] public float randomTilt = 0f;     // лише Scene Object
        public float sink = 0f;                            // зануреня в землю (м), лише Scene Object
    }

    public List<Entry> entries = new List<Entry>();
    public float radius = 8f;
    public int perDab = 6;
    [Range(0.3f, 1.5f)] public float spacing = 1f; // 1 = без перекриття, менше = крони можуть перекриватись
    public float maxSlope = 35f;
    public float defaultFootprint = 1.5f;

    bool active;
    SerializedObject so;
    Vector3 lastDab;
    const string ContainerName = "Scatter_Objects";

    [MenuItem("Tools/Scatter Brush")]
    static void Open() { GetWindow<ScatterBrush>("Scatter Brush"); }

    void OnEnable() { so = new SerializedObject(this); SceneView.duringSceneGui += OnScene; }
    void OnDisable() { SceneView.duringSceneGui -= OnScene; }

    void OnGUI()
    {
        so.Update();
        foreach (var n in new[] { "entries", "radius", "perDab", "spacing", "maxSlope", "defaultFootprint" })
            EditorGUILayout.PropertyField(so.FindProperty(n), true);
        so.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("ЛКМ: малювати. Shift + ЛКМ: стирати (дерева і Scene Object'и в радіусі). Alt: камера.", MessageType.Info);
        active = GUILayout.Toggle(active, active ? "Painting: ON" : "Painting: OFF", "Button", GUILayout.Height(30));
    }

    void OnScene(SceneView sv)
    {
        if (!active || entries.Count == 0) return;
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        if (!Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) return;
        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain == null) return;

        Handles.color = e.shift ? Color.red : Color.green;
        Handles.DrawWireDisc(hit.point, hit.normal, radius);
        if (e.type == EventType.MouseMove) sv.Repaint();

        if (e.alt || e.button != 0) return;
        if (e.type == EventType.MouseDown)
        {
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Scatter Brush");
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
        if (go == null) { go = new GameObject(ContainerName); Undo.RegisterCreatedObjectUndo(go, "Scatter container"); }
        return go.transform;
    }

    int GetIndex(TerrainData td, GameObject prefab)
    {
        var protos = new List<TreePrototype>(td.treePrototypes);
        for (int i = 0; i < protos.Count; i++) if (protos[i].prefab == prefab) return i;
        protos.Add(new TreePrototype { prefab = prefab }); // у кінець, старі індекси не ламаються
        td.treePrototypes = protos.ToArray();
        return protos.Count - 1;
    }

    struct Pt { public Vector2 pos; public float r; }

    void Dab(Terrain t, Vector3 center)
    {
        var td = t.terrainData;
        Vector3 tp = t.transform.position;
        var container = GetContainer();

        var byPrefab = new Dictionary<GameObject, Entry>();
        float total = 0, maxFp = defaultFootprint;
        foreach (var en in entries)
        {
            if (en.prefab == null) continue;
            byPrefab[en.prefab] = en;
            total += en.weight;
            maxFp = Mathf.Max(maxFp, en.footprint * en.scaleRange.y);
        }
        if (total <= 0) return;

        // уже існуючі об'єкти поблизу: Terrain-дерева і Scene Object'и
        float reach = radius + maxFp * 2f + 1f;
        var nearby = new List<Pt>();
        var protos = td.treePrototypes;
        foreach (var ti in td.treeInstances)
        {
            var w = Vector3.Scale(ti.position, td.size) + tp;
            if ((new Vector2(w.x - center.x, w.z - center.z)).sqrMagnitude > reach * reach) continue;
            float fp = defaultFootprint;
            if (ti.prototypeIndex < protos.Length && byPrefab.TryGetValue(protos[ti.prototypeIndex].prefab, out var pe)) fp = pe.footprint;
            nearby.Add(new Pt { pos = new Vector2(w.x, w.z), r = fp * ti.widthScale });
        }
        foreach (Transform c in container)
        {
            if ((new Vector2(c.position.x - center.x, c.position.z - center.z)).sqrMagnitude > reach * reach) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
            float fp = defaultFootprint;
            if (src != null && byPrefab.TryGetValue(src, out var pe)) fp = pe.footprint;
            nearby.Add(new Pt { pos = new Vector2(c.position.x, c.position.z), r = fp * c.localScale.x });
        }

        int placed = 0, attempts = 0;
        while (placed < perDab && attempts < perDab * 4)
        {
            attempts++;
            Vector2 r2 = UnityEngine.Random.insideUnitCircle * radius;
            float wx = center.x + r2.x, wz = center.z + r2.y;
            float nx = (wx - tp.x) / td.size.x, nz = (wz - tp.z) / td.size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            if (td.GetSteepness(nx, nz) > maxSlope) continue;

            // вибір запису за вагою
            float pick = UnityEngine.Random.value * total, acc = 0;
            Entry en = null;
            foreach (var cand in entries)
            {
                if (cand.prefab == null) continue;
                acc += cand.weight; en = cand;
                if (pick <= acc) break;
            }

            float s = UnityEngine.Random.Range(en.scaleRange.x, en.scaleRange.y);
            float myR = en.footprint * s;

            bool tooClose = false;
            var p = new Vector2(wx, wz);
            foreach (var q in nearby)
            {
                float need = (q.r + myR) * spacing;
                if ((q.pos - p).sqrMagnitude < need * need) { tooClose = true; break; }
            }
            if (tooClose) continue;
            nearby.Add(new Pt { pos = p, r = myR });

            float wy = t.SampleHeight(new Vector3(wx, 0, wz)) + tp.y;
            float yaw = UnityEngine.Random.Range(en.yawRange.x, en.yawRange.y);

            if (en.asSceneObject)
            {
                Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                Quaternion align = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(Vector3.up, normal), en.alignToNormal);
                Quaternion local = Quaternion.Euler(
                    UnityEngine.Random.Range(-en.randomTilt, en.randomTilt), yaw,
                    UnityEngine.Random.Range(-en.randomTilt, en.randomTilt));

                var go = (GameObject)PrefabUtility.InstantiatePrefab(en.prefab);
                go.transform.SetParent(container, false);
                go.transform.position = new Vector3(wx, wy - en.sink * s, wz);
                go.transform.rotation = align * local;
                go.transform.localScale = Vector3.one * s;
                Undo.RegisterCreatedObjectUndo(go, "Scatter object");
            }
            else
            {
                t.AddTreeInstance(new TreeInstance
                {
                    prototypeIndex = GetIndex(td, en.prefab),
                    position = new Vector3(nx, (wy - tp.y) / td.size.y, nz),
                    widthScale = s, heightScale = s,
                    rotation = yaw * Mathf.Deg2Rad,
                    color = Color.white, lightmapColor = Color.white
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

        var keep = new List<TreeInstance>();
        foreach (var ti in td.treeInstances)
        {
            var w = Vector3.Scale(ti.position, td.size) + tp;
            if ((new Vector2(w.x - center.x, w.z - center.z)).sqrMagnitude > r2) keep.Add(ti);
        }
        td.treeInstances = keep.ToArray();

        var container = GameObject.Find(ContainerName);
        if (container != null)
        {
            for (int i = container.transform.childCount - 1; i >= 0; i--)
            {
                var c = container.transform.GetChild(i);
                if ((new Vector2(c.position.x - center.x, c.position.z - center.z)).sqrMagnitude <= r2)
                    Undo.DestroyObjectImmediate(c.gameObject);
            }
        }
        EditorUtility.SetDirty(td);
        t.Flush();
    }
}