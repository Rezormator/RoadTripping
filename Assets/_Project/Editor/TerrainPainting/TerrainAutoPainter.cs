using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RoadTripping.EditorTools.TerrainPainting
{
    // Paints terrain splatmaps from rules: road shoulder, slopes, height tint, noise patches, needles under trees.
    public static class TerrainAutoPainter
    {
        public const string BackupFolder = "Assets/_Project/World/Terrain/Backups";
        private const float FallbackRoadHalfWidth = 4f;
        private const float RoadSampleStep = 2f;
        private const float MaxRoadHalfWidth = 20f;

        // One backup per editor session is enough; reapplying presets should not pile up copies.
        private static bool backedUpThisSession;

        public class Options
        {
            public bool selectedOnly;
            public bool backup = true;
        }

        public static void Apply(TerrainPaintPreset preset, Options options)
        {
            List<Terrain> terrains = CollectTerrains(options.selectedOnly);
            if (terrains.Count == 0)
            {
                EditorUtility.DisplayDialog("Terrain Auto Paint",
                    options.selectedOnly ? "Select one or more terrains first." : "No active terrains in the open scenes.", "OK");
                return;
            }

            var timer = Stopwatch.StartNew();
            try
            {
                EditorUtility.DisplayProgressBar("Terrain Auto Paint", "Collecting road and trees", 0f);
                float roadReach = preset.shoulderWidth + preset.edgeJitter + 2f + MaxRoadHalfWidth;
                RoadField road = RoadField.FromOpenScenes(roadReach, FallbackRoadHalfWidth, RoadSampleStep);
                TreeField trees = preset.useNeedles
                    ? TreeField.Collect(terrains, preset.treeNameFilter, preset.needleRadius * 2f + 2f)
                    : null;

                List<PaintLayer> order = ActiveLayers(preset, road.SegmentCount > 0, trees != null && trees.Count > 0);

                EditorUtility.DisplayProgressBar("Terrain Auto Paint", "Generating textures and layers", 0.05f);
                Dictionary<PaintLayer, TerrainLayer> assets = TerrainLayerGenerator.Generate(preset, order);
                TerrainLayer[] layerArray = order.Select(l => assets[l]).ToArray();

                if (options.backup && !backedUpThisSession)
                {
                    BackupTerrainData(terrains);
                    backedUpThisSession = true;
                }

                GetHeightRange(terrains, out float minH, out float maxH);

                for (int i = 0; i < terrains.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Terrain Auto Paint",
                        $"Painting {terrains[i].name} ({i + 1}/{terrains.Count})", 0.1f + 0.9f * i / terrains.Count);
                    PaintTerrain(terrains[i], preset, order, layerArray, road, trees, minH, maxH);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[TerrainPaint] Preset '{preset.name}' painted {terrains.Count} terrain(s) in {timer.Elapsed.TotalSeconds:F1}s. " +
                          $"Layers: {string.Join(", ", order)}. Road: {road.SegmentCount} segments, {road.TotalLength:F0} m. " +
                          $"Trees: {(trees != null ? trees.Count : 0)}." +
                          (road.SegmentCount == 0 ? " No road found: add a SplineExtrude or a SplineContainer with 'Road' in its name." : ""));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<Terrain> CollectTerrains(bool selectedOnly)
        {
            IEnumerable<Terrain> source = selectedOnly
                ? Selection.gameObjects.Select(g => g.GetComponent<Terrain>())
                : Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            return source.Where(t => t != null && t.terrainData != null).Distinct().ToList();
        }

        private static List<PaintLayer> ActiveLayers(TerrainPaintPreset p, bool hasRoad, bool hasTrees)
        {
            var list = new List<PaintLayer> { PaintLayer.GrassMain };
            if (p.useHeight || p.usePatches)
            {
                list.Add(PaintLayer.GrassLight);
                list.Add(PaintLayer.GrassDark);
            }
            if (hasRoad || p.useSlopes)
                list.Add(PaintLayer.Dirt);
            if (hasRoad)
                list.Add(PaintLayer.Gravel);
            if (p.useSlopes)
                list.Add(PaintLayer.Rock);
            if (p.useNeedles && hasTrees)
                list.Add(PaintLayer.Needles);
            return list;
        }

        private static void BackupTerrainData(List<Terrain> terrains)
        {
            string folder = BackupFolder + "/" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool created = false;
            foreach (Terrain terrain in terrains)
            {
                string path = AssetDatabase.GetAssetPath(terrain.terrainData);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset"))
                    continue;
                if (!created)
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                    created = true;
                }
                AssetDatabase.CopyAsset(path, folder + "/" + Path.GetFileName(path));
            }
            if (created)
                Debug.Log("[TerrainPaint] TerrainData backup: " + folder);
        }

        private static void GetHeightRange(List<Terrain> terrains, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (Terrain t in terrains)
            {
                Bounds b = t.terrainData.bounds;
                float y = t.GetPosition().y;
                min = Mathf.Min(min, y + b.min.y);
                max = Mathf.Max(max, y + b.max.y);
            }
            if (max - min < 1f)
                max = min + 1f;
        }

        private static void PaintTerrain(Terrain terrain, TerrainPaintPreset p, List<PaintLayer> order,
            TerrainLayer[] layers, RoadField road, TreeField trees, float minH, float maxH)
        {
            TerrainData data = terrain.terrainData;
            var undoObjects = new List<Object> { data };
            undoObjects.AddRange(data.alphamapTextures);
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "Terrain Auto Paint");

            data.terrainLayers = layers;

            int res = data.alphamapResolution;
            int hRes = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, hRes, hRes);
            Vector3 size = data.size;
            Vector3 pos = terrain.GetPosition();

            int layerCount = order.Count;
            int[] slot = new int[System.Enum.GetValues(typeof(PaintLayer)).Length];
            for (int i = 0; i < slot.Length; i++)
                slot[i] = -1;
            for (int i = 0; i < layerCount; i++)
                slot[(int)order[i]] = i;

            var map = new float[res, res, layerCount];
            var ctx = new Context
            {
                preset = p, road = road, trees = trees, heights = heights, hRes = hRes,
                size = size, pos = pos, minH = minH, maxH = maxH
            };

            Parallel.For(0, res, z =>
            {
                var w = new float[slot.Length];
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                    ComputeWeights(ctx, u, v, w);

                    float sum = 0f;
                    for (int l = 0; l < w.Length; l++)
                    {
                        if (slot[l] < 0)
                            continue;
                        float value = Mathf.Pow(w[l], p.sharpness);
                        w[l] = value;
                        sum += value;
                    }

                    for (int l = 0; l < w.Length; l++)
                    {
                        if (slot[l] >= 0)
                            map[z, x, slot[l]] = sum > 1e-5f ? w[l] / sum : 0f;
                    }
                    if (sum <= 1e-5f)
                        map[z, x, 0] = 1f;
                }
            });

            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
        }

        private class Context
        {
            public TerrainPaintPreset preset;
            public RoadField road;
            public TreeField trees;
            public float[,] heights;
            public int hRes;
            public Vector3 size, pos;
            public float minH, maxH;
        }

        // Fills w (indexed by PaintLayer) with unnormalized weights for one splatmap texel.
        private static void ComputeWeights(Context c, float u, float v, float[] w)
        {
            TerrainPaintPreset p = c.preset;
            System.Array.Clear(w, 0, w.Length);

            float wx = c.pos.x + u * c.size.x;
            float wz = c.pos.z + v * c.size.z;

            // Height and slope from the heightmap.
            float hx = u * (c.hRes - 1), hz = v * (c.hRes - 1);
            float worldH = c.pos.y + SampleHeight(c, hx, hz) * c.size.y;
            float cellX = c.size.x / (c.hRes - 1), cellZ = c.size.z / (c.hRes - 1);
            float dhx = (SampleHeight(c, hx + 1f, hz) - SampleHeight(c, hx - 1f, hz)) * c.size.y / (2f * cellX);
            float dhz = (SampleHeight(c, hx, hz + 1f) - SampleHeight(c, hx, hz - 1f)) * c.size.y / (2f * cellZ);
            float slope = Mathf.Atan(Mathf.Sqrt(dhx * dhx + dhz * dhz)) * Mathf.Rad2Deg;

            int seed = p.seed;
            float edgeScale = Mathf.Max(0.5f, p.edgeNoiseScale);
            float nEdge = PaintNoise.Fbm(wx / edgeScale, wz / edgeScale, seed + 11, 2);
            float patchScale = Mathf.Max(1f, p.patchScale);
            // 2 octaves: big calm patches without small speckly edges.
            float nPatchA = PaintNoise.Fbm(wx / patchScale, wz / patchScale, seed + 23, 2);
            float nPatchB = PaintNoise.Fbm(wx / (patchScale * 0.85f) + 31.7f, wz / (patchScale * 0.85f) - 17.3f, seed + 37, 2);

            // 1. Grass: main + light (high/dry) + dark (low/lush).
            float light = 0f, dark = 0f;
            if (p.useHeight)
            {
                float hN = Mathf.InverseLerp(c.minH, c.maxH, worldH) + (nPatchA - 0.5f) * 2f * p.heightBlendNoise;
                light = p.highlandLight * SmoothStep(0.55f, 0.9f, hN);
                dark = p.lowlandDark * (1f - SmoothStep(0.1f, 0.45f, hN));
            }
            if (p.usePatches)
            {
                float threshold = 1f - p.patchCoverage;
                dark = Mathf.Max(dark, SmoothStep(threshold - p.patchSoftness, threshold + p.patchSoftness, nPatchA));
                light = Mathf.Max(light, 0.8f * SmoothStep(threshold - p.patchSoftness, threshold + p.patchSoftness, nPatchB));
            }
            float grassSum = light + dark;
            if (grassSum > 1f)
            {
                light /= grassSum;
                dark /= grassSum;
                grassSum = 1f;
            }
            w[(int)PaintLayer.GrassMain] = 1f - grassSum;
            w[(int)PaintLayer.GrassLight] = light;
            w[(int)PaintLayer.GrassDark] = dark;

            // 2. Needles under trees.
            if (p.useNeedles && c.trees != null && c.trees.Count > 0)
            {
                float d = c.trees.NearestDistance(wx, wz) + (nEdge - 0.5f) * p.needleRadius;
                Overlay(w, PaintLayer.Needles, 0.9f * (1f - SmoothStep(p.needleRadius * 0.5f, p.needleRadius, d)));
            }

            // 3. Dirt on medium slopes.
            float slopeJitter = (nEdge - 0.5f) * 2f * p.slopeSoftness;
            if (p.useSlopes)
                Overlay(w, PaintLayer.Dirt, SmoothStep(p.dirtSlope - p.slopeSoftness, p.dirtSlope + p.slopeSoftness, slope + slopeJitter));

            // 4. Road shoulder: gravel next to the asphalt, then dirt fading into grass.
            float gravel = 0f;
            float roadDist = c.road.DistanceToEdge(wx, wz);
            if (!float.IsPositiveInfinity(roadDist))
            {
                float de = Mathf.Max(0f, roadDist) + (nEdge - 0.5f) * 2f * p.edgeJitter;
                float fadeStart = Mathf.Max(p.gravelWidth, p.shoulderWidth - p.shoulderFade);
                float dirt = 1f - SmoothStep(fadeStart, p.shoulderWidth, de);
                // Break up the fading part with patches so it does not look like a ruler line.
                float fadePart = SmoothStep(fadeStart, p.shoulderWidth, de);
                dirt *= 1f - fadePart * (1f - nPatchB);
                Overlay(w, PaintLayer.Dirt, dirt);

                float g = Mathf.Max(0.3f, p.edgeJitter * 0.5f);
                gravel = 1f - SmoothStep(p.gravelWidth - g, p.gravelWidth + g, de);
                Overlay(w, PaintLayer.Gravel, gravel);
            }

            // 5. Rock on steep slopes (kept off the gravel strip).
            if (p.useSlopes)
            {
                float rock = SmoothStep(p.rockSlope - p.slopeSoftness, p.rockSlope + p.slopeSoftness, slope + slopeJitter);
                Overlay(w, PaintLayer.Rock, rock * (1f - gravel));
            }
        }

        private static float SampleHeight(Context c, float x, float z)
        {
            int max = c.hRes - 1;
            x = Mathf.Clamp(x, 0f, max);
            z = Mathf.Clamp(z, 0f, max);
            int x0 = Mathf.Min((int)x, max - 1), z0 = Mathf.Min((int)z, max - 1);
            float tx = x - x0, tz = z - z0;
            float[,] h = c.heights;
            float a = h[z0, x0] + (h[z0, x0 + 1] - h[z0, x0]) * tx;
            float b = h[z0 + 1, x0] + (h[z0 + 1, x0 + 1] - h[z0 + 1, x0]) * tx;
            return a + (b - a) * tz;
        }

        private static void Overlay(float[] w, PaintLayer layer, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha <= 0f)
                return;
            for (int i = 0; i < w.Length; i++)
                w[i] *= 1f - alpha;
            w[(int)layer] += alpha;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            if (b - a < 1e-5f)
                return x < a ? 0f : 1f;
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
