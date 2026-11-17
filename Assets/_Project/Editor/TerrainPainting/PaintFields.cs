using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace RoadTripping.EditorTools.TerrainPainting
{
    // Uniform grid over the XZ plane. Every item is stored in all cells it can influence,
    // so a query only has to look into one cell. Read-only after building (thread-safe queries).
    public abstract class GridField
    {
        protected readonly float CellSize;
        protected readonly float MaxDistance;
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();

        protected GridField(float maxDistance)
        {
            MaxDistance = Mathf.Max(0.1f, maxDistance);
            CellSize = Mathf.Max(8f, MaxDistance);
        }

        protected void Insert(int item, float minX, float minZ, float maxX, float maxZ)
        {
            int x0 = Mathf.FloorToInt((minX - MaxDistance) / CellSize);
            int x1 = Mathf.FloorToInt((maxX + MaxDistance) / CellSize);
            int z0 = Mathf.FloorToInt((minZ - MaxDistance) / CellSize);
            int z1 = Mathf.FloorToInt((maxZ + MaxDistance) / CellSize);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                long key = Key(x, z);
                if (!cells.TryGetValue(key, out List<int> list))
                    cells[key] = list = new List<int>();
                list.Add(item);
            }
        }

        protected List<int> Query(float x, float z)
        {
            cells.TryGetValue(Key(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)), out List<int> list);
            return list;
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    // Distance from a point to the nearest road edge (negative = on the asphalt).
    public class RoadField : GridField
    {
        private readonly List<Vector4> segments = new List<Vector4>(); // ax, az, bx, bz
        private readonly List<float> halfWidths = new List<float>();

        public int SegmentCount => segments.Count;
        public float TotalLength { get; private set; }

        public RoadField(float maxDistance) : base(maxDistance) { }

        // Every enabled SplineExtrude is treated as a road; plain SplineContainers whose
        // name contains "road" are added too, with fallbackHalfWidth.
        public static RoadField FromOpenScenes(float maxDistance, float fallbackHalfWidth, float step = 1f)
        {
            var field = new RoadField(maxDistance);
            var used = new HashSet<SplineContainer>();

            foreach (SplineExtrude extrude in Object.FindObjectsByType<SplineExtrude>(FindObjectsInactive.Exclude))
            {
                if (!extrude.isActiveAndEnabled || extrude.Container == null)
                    continue;
                // Road shape top surface spans -0.5..0.5 of the radius.
                float half = 0.5f * extrude.Radius * Mathf.Abs(extrude.transform.lossyScale.x);
                Vector2 range = extrude.Range;
                field.AddContainer(extrude.Container, half, range.x, range.y, step);
                used.Add(extrude.Container);
            }

            foreach (SplineContainer container in Object.FindObjectsByType<SplineContainer>(FindObjectsInactive.Exclude))
            {
                if (used.Contains(container) || !container.isActiveAndEnabled)
                    continue;
                if (container.name.ToLowerInvariant().Contains("road"))
                    field.AddContainer(container, fallbackHalfWidth, 0f, 1f, step);
            }

            return field;
        }

        private void AddContainer(SplineContainer container, float halfWidth, float from, float to, float step)
        {
            for (int s = 0; s < container.Splines.Count; s++)
            {
                if (container.Splines[s].Count < 2)
                    continue;
                float length = container.CalculateLength(s) * Mathf.Max(0f, to - from);
                int count = Mathf.Max(2, Mathf.CeilToInt(length / step));
                float3 prev = container.EvaluatePosition(s, from);
                for (int i = 1; i <= count; i++)
                {
                    float3 p = container.EvaluatePosition(s, Mathf.Lerp(from, to, (float)i / count));
                    AddSegment(new Vector2(prev.x, prev.z), new Vector2(p.x, p.z), halfWidth);
                    prev = p;
                }
                TotalLength += length;
            }
        }

        private void AddSegment(Vector2 a, Vector2 b, float halfWidth)
        {
            int id = segments.Count;
            segments.Add(new Vector4(a.x, a.y, b.x, b.y));
            halfWidths.Add(halfWidth);
            Insert(id, Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public float DistanceToEdge(float x, float z)
        {
            List<int> list = Query(x, z);
            if (list == null)
                return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            foreach (int id in list)
            {
                Vector4 s = segments[id];
                float dx = s.z - s.x, dz = s.w - s.y;
                float lenSq = dx * dx + dz * dz;
                float t = lenSq > 1e-6f ? ((x - s.x) * dx + (z - s.y) * dz) / lenSq : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float px = s.x + dx * t - x, pz = s.y + dz * t - z;
                float d = Mathf.Sqrt(px * px + pz * pz) - halfWidths[id];
                if (d < best)
                    best = d;
            }
            return best;
        }
    }

    // Nearest-tree distance for the "needles under trees" rule.
    public class TreeField : GridField
    {
        private readonly List<Vector2> points = new List<Vector2>();
        public int Count => points.Count;

        public TreeField(float maxDistance) : base(maxDistance) { }

        public static TreeField Collect(IEnumerable<Terrain> terrains, string nameFilter, float maxDistance)
        {
            var field = new TreeField(maxDistance);

            foreach (Terrain terrain in terrains)
            {
                TerrainData data = terrain.terrainData;
                Vector3 pos = terrain.GetPosition();
                foreach (TreeInstance tree in data.treeInstances)
                    field.Add(pos.x + tree.position.x * data.size.x, pos.z + tree.position.z * data.size.z);
            }

            Regex regex = null;
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                try { regex = new Regex(nameFilter, RegexOptions.IgnoreCase); }
                catch (System.ArgumentException) { Debug.LogWarning("[TerrainPaint] Invalid tree name filter: " + nameFilter); }
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name != "TreeBrush_Objects")
                    continue;
                foreach (Transform child in t)
                {
                    if (regex != null && !regex.IsMatch(child.name))
                        continue;
                    field.Add(child.position.x, child.position.z);
                }
            }

            return field;
        }

        private void Add(float x, float z)
        {
            points.Add(new Vector2(x, z));
            Insert(points.Count - 1, x, z, x, z);
        }

        public float NearestDistance(float x, float z)
        {
            List<int> list = Query(x, z);
            if (list == null)
                return float.PositiveInfinity;
            float best = float.PositiveInfinity;
            foreach (int id in list)
            {
                Vector2 p = points[id];
                float d = (p.x - x) * (p.x - x) + (p.y - z) * (p.y - z);
                if (d < best)
                    best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}
