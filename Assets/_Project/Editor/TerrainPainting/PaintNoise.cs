namespace RoadTripping.EditorTools.TerrainPainting
{
    // Small thread-safe value noise (Mathf.PerlinNoise is not used so painting can run in parallel).
    public static class PaintNoise
    {
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static int Floor(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        // Value noise in 0..1.
        public static float Value(float x, float y, int seed)
        {
            int x0 = Floor(x), y0 = Floor(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            float a = Hash01(x0, y0, seed), b = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed), d = Hash01(x0 + 1, y0 + 1, seed);
            float ab = a + (b - a) * tx, cd = c + (d - c) * tx;
            return ab + (cd - ab) * ty;
        }

        // Value noise that repeats every `period` cells (for tiling textures).
        public static float Tiled(float x, float y, int period, int seed)
        {
            int x0 = Floor(x), y0 = Floor(y);
            float tx = Smooth(x - x0), ty = Smooth(y - y0);
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period);
            int ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            float a = Hash01(xa, ya, seed), b = Hash01(xb, ya, seed);
            float c = Hash01(xa, yb, seed), d = Hash01(xb, yb, seed);
            float ab = a + (b - a) * tx, cd = c + (d - c) * tx;
            return ab + (cd - ab) * ty;
        }

        // Fractal value noise in 0..1, contrast-stretched so values reach both ends more often.
        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f - 9.7f;
            }
            float n = sum / norm;
            n = (n - 0.5f) * 2.2f + 0.5f;
            return n < 0f ? 0f : n > 1f ? 1f : n;
        }

        private static int Mod(int v, int m)
        {
            int r = v % m;
            return r < 0 ? r + m : r;
        }
    }
}
