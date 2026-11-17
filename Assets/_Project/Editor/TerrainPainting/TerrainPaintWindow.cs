using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoadTripping.EditorTools.TerrainPainting
{
    // Tools/RoadTripping/Terrain Auto Paint: pick a preset and paint all terrains of the open scenes.
    public class TerrainPaintWindow : EditorWindow
    {
        public const string PresetFolder = "Assets/_Project/World/Terrain/Presets";

        private readonly TerrainAutoPainter.Options options = new TerrainAutoPainter.Options();
        private List<TerrainPaintPreset> presets = new List<TerrainPaintPreset>();
        private Vector2 scroll;

        [MenuItem("Tools/RoadTripping/Terrain Auto Paint")]
        private static void Open()
        {
            GetWindow<TerrainPaintWindow>("Terrain Auto Paint");
        }

        private void OnEnable()
        {
            RefreshPresets();
            // Creating assets during a domain reload is not allowed, so defaults are made a bit later.
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;
                TerrainPaintDefaults.EnsureDefaults();
                RefreshPresets();
                Repaint();
            };
        }

        private void OnFocus()
        {
            TerrainPaintDefaults.EnsureDefaults();
            RefreshPresets();
        }

        private void RefreshPresets()
        {
            presets = AssetDatabase.FindAssets("t:" + nameof(TerrainPaintPreset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<TerrainPaintPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null)
                .OrderBy(p => p.name)
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Paints the splatmaps of terrains from rules: gravel + dirt shoulder along the road " +
                "(every SplineExtrude, or a SplineContainer with 'Road' in its name), rock/dirt on slopes, " +
                "grass tint by height, noise patches and needles under trees.\n" +
                "Each preset generates its own textures and Terrain Layers. Tweak a preset in the Inspector and press Apply again.",
                MessageType.Info);

            options.selectedOnly = EditorGUILayout.ToggleLeft("Only selected terrains", options.selectedOnly);
            options.backup = EditorGUILayout.ToggleLeft("Back up TerrainData before painting", options.backup);

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (TerrainPaintPreset preset in presets)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(preset.name, EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(preset.description))
                        EditorGUILayout.LabelField(preset.description, EditorStyles.wordWrappedMiniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Apply"))
                        {
                            TerrainAutoPainter.Apply(preset, options);
                            GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("Edit", GUILayout.Width(60)))
                        {
                            Selection.activeObject = preset;
                            EditorGUIUtility.PingObject(preset);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New preset"))
                    CreatePreset();
                if (GUILayout.Button("Restore default presets"))
                {
                    if (EditorUtility.DisplayDialog("Terrain Auto Paint",
                            "Overwrite the default presets with their original values?", "Overwrite", "Cancel"))
                    {
                        TerrainPaintDefaults.CreateAll(overwrite: true);
                        RefreshPresets();
                    }
                }
            }
        }

        private void CreatePreset()
        {
            Directory.CreateDirectory(PresetFolder);
            var preset = TerrainPaintDefaults.LowPolyDusk();
            string path = AssetDatabase.GenerateUniqueAssetPath(PresetFolder + "/MyPreset.asset");
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            RefreshPresets();
            Selection.activeObject = preset;
        }
    }
}
