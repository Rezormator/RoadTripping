using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Imports heightmap tiles produced by Heightmaps/generate_heightmaps.py.
// Also builds the road centre line as a spline (Terrain_RoadTripping/Road) from road.csv,
// one knot every 20 m on the flattened road surface, lifted by RoadSplineLift.
//
// Tools/RoadTripping/Import Terrain Map - every sector into Assets/_Project/Scenes/TerrainMap.unity
//
// The scene and all TerrainData in Assets/_Project/World/Terrain/Data are rebuilt
// from scratch, so anything painted or planted on the terrains is lost; the menu
// asks for confirmation first. The scene opens additively, so the scene you are
// working in is left alone. Hierarchy:
//   Terrain_RoadTripping / Sector_01..Sector_10 / Tiles / tile_xNNN_zNNN
//                                               / Loop / LoopFirst, LoopLast   (sectors with a loop)
// LoopFirst and LoopLast sit on the road in the middle of the first and last
// segment and look along the road; teleport from LoopLast to LoopFirst keeps
// the player's offset relative to the marker.
public static class HeightmapTileImporter
{
    private const string AssetFolder = "Assets/_Project/World/Terrain/Data";
    private const string ScenePath = "Assets/_Project/Scenes/TerrainMap.unity";
    private const string OldPreviewScenePath = "Assets/_Project/Scenes/HeightmapPreview.unity";

    // Memory settings for a low-poly look: small control maps, instanced drawing.
    private const int BaseMapResolution = 64;
    private const int AlphamapResolution = 128;
    private const int DetailResolution = 128;
    private const int DetailResolutionPerPatch = 16;

    // Spline sits this much above the flattened terrain under the road.
    private const float RoadSplineLift = 0.05f;

    [System.Serializable]
    private class Settings
    {
        public float tileSize = 200f;
        public int resolution = 65;
        public float terrainHeight = 250f;
    }

    private static string OutputFolder =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Heightmaps", "output");

    [MenuItem("Tools/RoadTripping/Import Terrain Map")]
    private static void ImportAllMenu()
    {
        bool rebuild = EditorUtility.DisplayDialog(
            "Import Terrain Map",
            "TerrainMap.unity and every TerrainData in " + AssetFolder + " will be rebuilt from Heightmaps/output.\n\n" +
            "Anything painted, planted or placed on these terrains will be lost.",
            "Rebuild", "Cancel");
        if (rebuild)
            BuildScene(null);
    }

    private static void BuildScene(HashSet<string> sectors)
    {
        string manifestPath = Path.Combine(OutputFolder, "manifest.csv");
        string settingsPath = Path.Combine(OutputFolder, "settings.json");
        if (!File.Exists(manifestPath) || !File.Exists(settingsPath))
        {
            Debug.LogError($"[Heightmaps] manifest.csv or settings.json not found in {OutputFolder}");
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        CloseIfLoaded(ScenePath);
        CloseIfLoaded(OldPreviewScenePath);
        if (File.Exists(OldPreviewScenePath))
            AssetDatabase.DeleteAsset(OldPreviewScenePath);
        DeleteOldTerrainData();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject root = ImportTiles(manifestPath, settingsPath, sectors);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (previous.IsValid() && previous != scene)
            SceneManager.SetActiveScene(previous);

        if (root == null)
            return;
        Selection.activeGameObject = root;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
            view.FrameSelected();
        string what = sectors == null ? "all sectors" : "sectors " + string.Join(", ", sectors);
        Debug.Log($"[Heightmaps] Imported {what} into {ScenePath}");
    }

    private static void CloseIfLoaded(string path)
    {
        Scene s = SceneManager.GetSceneByPath(path);
        if (s.IsValid() && s.isLoaded && SceneManager.sceneCount > 1)
            EditorSceneManager.CloseScene(s, true);
    }

    private static void DeleteOldTerrainData()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            return;
        string[] old = AssetDatabase.FindAssets("t:TerrainData", new[] { AssetFolder })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var failed = new List<string>();
        AssetDatabase.DeleteAssets(old, failed);
    }

    private static GameObject ImportTiles(string manifestPath, string settingsPath, HashSet<string> sectors)
    {
        Settings settings = JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath));
        string[] lines = File.ReadAllLines(manifestPath);
        EnsureFolder(AssetFolder);

        var root = new GameObject("Terrain_RoadTripping");
        var sectorRoots = new Dictionary<string, Transform>();
        int res = settings.resolution;

        Transform SectorTiles(string sector)
        {
            if (!sectorRoots.TryGetValue(sector, out Transform tiles))
            {
                var sectorGo = new GameObject($"Sector_{int.Parse(sector):00}");
                sectorGo.transform.SetParent(root.transform, false);
                tiles = new GameObject("Tiles").transform;
                tiles.SetParent(sectorGo.transform, false);
                sectorRoots[sector] = tiles;
            }
            return tiles;
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(',');
                if (f.Length < 7)
                    continue;
                string sector = f[6];
                if (sectors != null && !sectors.Contains(sector))
                    continue;
                string tileName = f[1];
                float worldX = float.Parse(f[4], CultureInfo.InvariantCulture);
                float worldZ = float.Parse(f[5], CultureInfo.InvariantCulture);

                if (EditorUtility.DisplayCancelableProgressBar("Terrain import", tileName, i / (float)lines.Length))
                    break;

                byte[] bytes = File.ReadAllBytes(Path.Combine(OutputFolder, "tiles", tileName + ".raw"));
                var heights = new float[res, res];
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int k = (y * res + x) * 2;
                    heights[y, x] = (bytes[k] | (bytes[k + 1] << 8)) / 65535f;
                }

                var data = new TerrainData { heightmapResolution = res };
                data.size = new Vector3(settings.tileSize, settings.terrainHeight, settings.tileSize);
                data.baseMapResolution = BaseMapResolution;
                data.alphamapResolution = AlphamapResolution;
                data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);
                data.SetHeights(0, 0, heights);
                AssetDatabase.CreateAsset(data, $"{AssetFolder}/{tileName}.asset");

                GameObject go = Terrain.CreateTerrainGameObject(data);
                go.name = tileName;
                go.transform.SetParent(SectorTiles(sector), false);
                go.transform.position = new Vector3(worldX, 0f, worldZ);
                Terrain terrain = go.GetComponent<Terrain>();
                terrain.drawInstanced = true;
                terrain.allowAutoConnect = true;
                terrain.groupingID = 0;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AddLoopMarkers(sectors, sectorRoots);
        SortSectors(root.transform);
        AddRoadSpline(root.transform, sectors);
        AssetDatabase.SaveAssets();
        Terrain.SetConnectivityDirty();
        return root;
    }

    private static void AddLoopMarkers(HashSet<string> sectors, Dictionary<string, Transform> sectorRoots)
    {
        string loopsPath = Path.Combine(OutputFolder, "loops.csv");
        if (!File.Exists(loopsPath))
            return;
        string[] lines = File.ReadAllLines(loopsPath);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            if (f.Length < 10)
                continue;
            string sector = f[0];
            if ((sectors != null && !sectors.Contains(sector)) || !sectorRoots.TryGetValue(sector, out Transform tiles))
                continue;
            float P(int idx) => float.Parse(f[idx], CultureInfo.InvariantCulture);

            var loop = new GameObject("Loop").transform;
            loop.SetParent(tiles.parent, false);
            var first = new GameObject("LoopFirst").transform;
            first.SetParent(loop, false);
            first.SetPositionAndRotation(new Vector3(P(1), P(2), P(3)), Quaternion.Euler(0f, P(4), 0f));
            var last = new GameObject("LoopLast").transform;
            last.SetParent(loop, false);
            last.SetPositionAndRotation(new Vector3(P(5), P(6), P(7)), Quaternion.Euler(0f, P(8), 0f));
        }
    }

    private static void AddRoadSpline(Transform root, HashSet<string> sectors)
    {
        string roadPath = Path.Combine(OutputFolder, "road.csv");
        if (!File.Exists(roadPath))
            return;

        var go = new GameObject("Road");
        go.transform.SetParent(root, false);
        go.transform.SetAsFirstSibling();
        var container = go.AddComponent<SplineContainer>();
        Spline spline = container.Spline;
        spline.Clear();

        string[] lines = File.ReadAllLines(roadPath);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            if (f.Length < 5 || (sectors != null && !sectors.Contains(f[4])))
                continue;
            float x = float.Parse(f[1], CultureInfo.InvariantCulture);
            float y = float.Parse(f[2], CultureInfo.InvariantCulture) + RoadSplineLift;
            float z = float.Parse(f[3], CultureInfo.InvariantCulture);
            spline.Add(new BezierKnot(new float3(x, y, z)), TangentMode.AutoSmooth);
        }
    }

    private static void SortSectors(Transform root)
    {
        var children = root.Cast<Transform>().OrderBy(t => t.name).ToList();
        for (int i = 0; i < children.Count; i++)
            children[i].SetSiblingIndex(i);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
