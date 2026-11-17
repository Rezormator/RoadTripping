using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadTripping.EditorTools.TerrainPainting
{
    // Generates a small stylized texture and a TerrainLayer asset for every layer of a preset.
    public static class TerrainLayerGenerator
    {
        public const string RootFolder = "Assets/_Project/World/Terrain/Layers";

        private static readonly float[] Bayer4 =
        {
            0f, 8f, 2f, 10f,
            12f, 4f, 14f, 6f,
            3f, 11f, 1f, 9f,
            15f, 7f, 13f, 5f
        };

        public static Dictionary<PaintLayer, TerrainLayer> Generate(TerrainPaintPreset preset, IList<PaintLayer> layers)
        {
            string folder = RootFolder + "/" + preset.name;
            Directory.CreateDirectory(folder);

            // Write all PNGs first, then import once.
            foreach (PaintLayer layer in layers)
            {
                Texture2D tex = BuildTexture(preset, preset.GetLook(layer), preset.seed + (int)layer * 101);
                File.WriteAllBytes(TexturePath(folder, layer), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();

            var result = new Dictionary<PaintLayer, TerrainLayer>();
            foreach (PaintLayer layer in layers)
            {
                string texPath = TexturePath(folder, layer);
                ConfigureImporter(texPath, preset);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                LayerLook look = preset.GetLook(layer);

                string layerPath = folder + "/TL_" + layer + ".terrainlayer";
                var terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                bool isNew = terrainLayer == null;
                if (isNew)
                    terrainLayer = new TerrainLayer();

                terrainLayer.diffuseTexture = texture;
                terrainLayer.normalMapTexture = null;
                terrainLayer.tileSize = new Vector2(look.tileSize, look.tileSize);
                terrainLayer.tileOffset = Vector2.zero;
                terrainLayer.smoothness = 0f;
                terrainLayer.metallic = 0f;
                terrainLayer.specular = Color.black;

                if (isNew)
                    AssetDatabase.CreateAsset(terrainLayer, layerPath);
                else
                    EditorUtility.SetDirty(terrainLayer);
                result[layer] = terrainLayer;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static string TexturePath(string folder, PaintLayer layer) => folder + "/T_" + layer + ".png";

        private static void ConfigureImporter(string path, TerrainPaintPreset preset)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = preset.style == TerrainTextureStyle.PS1 ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(preset.textureSize));
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Texture2D BuildTexture(TerrainPaintPreset preset, LayerLook look, int seed)
        {
            int size = Mathf.Clamp(Mathf.NextPowerOfTwo(preset.textureSize), 16, 512);
            int cells = Mathf.Max(1, preset.noiseCells);
            int steps = Mathf.Max(2, preset.colorSteps);
            bool ps1 = preset.style == TerrainTextureStyle.PS1;
            var rng = new System.Random(seed);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size * cells, fy = (float)y / size * cells;
                    float n = 0.65f * PaintNoise.Tiled(fx, fy, cells, seed)
                              + 0.35f * PaintNoise.Tiled(fx * 2f, fy * 2f, cells * 2, seed + 7);
                    n = Mathf.Clamp01((n - 0.5f) * 1.8f + 0.5f);

                    if (ps1)
                        n += (Bayer4[(y & 3) * 4 + (x & 3)] / 16f - 0.5f) / steps;

                    float t = Mathf.Round(Mathf.Clamp01(n) * (steps - 1)) / (steps - 1);
                    Color c = Color.Lerp(look.baseColor, look.accentColor, t * look.variation);

                    if (rng.NextDouble() < look.speckles)
                        c = rng.Next(2) == 0 ? look.accentColor * 1.15f : look.baseColor * 0.75f;

                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
