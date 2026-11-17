using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadTripping.EditorTools.TerrainPainting
{
    // The built-in presets. Created in Assets/_Project/World/Terrain/Presets when none exist.
    public static class TerrainPaintDefaults
    {
        private static readonly (string name, Func<TerrainPaintPreset> create)[] All =
        {
            ("LowPoly_Dusk", LowPolyDusk)
        };

        public static void EnsureDefaults()
        {
            if (AssetDatabase.FindAssets("t:" + nameof(TerrainPaintPreset)).Length == 0)
                CreateAll(overwrite: false);
        }

        public static void CreateAll(bool overwrite)
        {
            Directory.CreateDirectory(TerrainPaintWindow.PresetFolder);
            AssetDatabase.Refresh();
            foreach ((string name, Func<TerrainPaintPreset> create) in All)
            {
                string path = TerrainPaintWindow.PresetFolder + "/" + name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<TerrainPaintPreset>(path);
                TerrainPaintPreset fresh = create();
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(fresh, path);
                }
                else if (overwrite)
                {
                    EditorUtility.CopySerialized(fresh, existing);
                    existing.name = name;
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(fresh);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(fresh);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // One grass colour; the light/dark grass layers are only small shifts of it.
        private const string Grass = "#4C6B3C";
        private const string GrassAccent = "#506F40";
        private const string GrassLight = "#677C42";
        private const string GrassLightAccent = "#71864C";
        private const string GrassDark = "#3C5934";
        private const string GrassDarkAccent = "#426039";

        private static void SetCalmGrass(TerrainPaintPreset p, float tileSize, float speckles)
        {
            p.grassMain = new LayerLook(Grass, GrassAccent, 0.6f, speckles, tileSize);
            p.grassLight = new LayerLook(GrassLight, GrassLightAccent, 0.6f, speckles, tileSize);
            p.grassDark = new LayerLook(GrassDark, GrassDarkAccent, 0.6f, speckles, tileSize);

            // Few, large and soft patches.
            p.usePatches = true;
            p.patchScale = 70f;
            p.patchCoverage = 0.25f;
            p.patchSoftness = 0.25f;
            p.useHeight = true;
            p.lowlandDark = 0.5f;
            p.highlandLight = 0.5f;
            p.heightBlendNoise = 0.1f;
        }

        // Soft blotchy low-poly look in the dark greens of the current scene.
        public static TerrainPaintPreset LowPolyDusk()
        {
            var p = ScriptableObject.CreateInstance<TerrainPaintPreset>();
            p.description = "Flat low-poly: one calm grass colour with large soft patches, gravel + dirt shoulder.";
            p.style = TerrainTextureStyle.FlatLowPoly;
            p.textureSize = 64;
            p.colorSteps = 3;
            p.noiseCells = 2;
            SetCalmGrass(p, 24f, 0f);
            p.dirt = new LayerLook("#6A5238", "#7A6045", 0.7f, 0.06f, 8f);
            p.gravel = new LayerLook("#77736B", "#908C84", 0.5f, 0.3f, 4f);
            p.rock = new LayerLook("#64656A", "#76777C", 0.6f, 0.08f, 10f);
            p.needles = new LayerLook("#4E3A2A", "#5E4632", 0.6f, 0.12f, 6f);
            p.sharpness = 1.5f;
            p.seed = 1234;
            return p;
        }
    }
}
