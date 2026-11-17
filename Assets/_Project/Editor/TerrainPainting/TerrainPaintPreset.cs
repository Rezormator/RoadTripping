using System;
using UnityEngine;

namespace RoadTripping.EditorTools.TerrainPainting
{
    public enum TerrainTextureStyle
    {
        // Big soft blotches, few colour steps, bilinear filtering.
        FlatLowPoly,
        // Small low-res pixels, ordered dithering, point filtering.
        PS1
    }

    public enum PaintLayer
    {
        GrassMain,
        GrassLight,
        GrassDark,
        Dirt,
        Gravel,
        Rock,
        Needles
    }

    [Serializable]
    public class LayerLook
    {
        public Color baseColor = new Color(0.25f, 0.36f, 0.2f);
        public Color accentColor = new Color(0.3f, 0.42f, 0.23f);
        [Tooltip("How far the texture blends from base to accent colour")]
        [Range(0f, 1f)] public float variation = 0.6f;
        [Tooltip("Share of single pixels painted as dots (stones, blades)")]
        [Range(0f, 0.6f)] public float speckles = 0.04f;
        [Tooltip("Size of one texture tile in metres")]
        [Min(0.5f)] public float tileSize = 8f;

        public LayerLook() { }

        public LayerLook(string baseHex, string accentHex, float variation, float speckles, float tileSize)
        {
            ColorUtility.TryParseHtmlString(baseHex, out baseColor);
            ColorUtility.TryParseHtmlString(accentHex, out accentColor);
            this.variation = variation;
            this.speckles = speckles;
            this.tileSize = tileSize;
        }
    }

    [CreateAssetMenu(menuName = "RoadTripping/Terrain Paint Preset", fileName = "TerrainPaintPreset")]
    public class TerrainPaintPreset : ScriptableObject
    {
        [TextArea(2, 4)] public string description;

        [Header("Texture style")]
        public TerrainTextureStyle style = TerrainTextureStyle.FlatLowPoly;
        [Tooltip("Generated texture size in pixels")]
        public int textureSize = 64;
        [Tooltip("Number of colour steps (posterization)")]
        [Range(2, 8)] public int colorSteps = 3;
        [Tooltip("How many colour blotches fit across one tile")]
        [Min(1)] public int noiseCells = 3;

        [Header("Layers")]
        public LayerLook grassMain = new LayerLook();
        public LayerLook grassLight = new LayerLook();
        public LayerLook grassDark = new LayerLook();
        public LayerLook dirt = new LayerLook();
        public LayerLook gravel = new LayerLook();
        public LayerLook rock = new LayerLook();
        public LayerLook needles = new LayerLook();

        [Header("Road shoulder (metres from the asphalt edge)")]
        [Tooltip("Gravel strip right next to the asphalt")]
        public float gravelWidth = 2f;
        [Tooltip("Total width of the shoulder (gravel + dirt) before it is fully grass")]
        public float shoulderWidth = 10f;
        [Tooltip("Outer part of the shoulder where dirt fades into grass")]
        public float shoulderFade = 5f;
        [Tooltip("Random wobble of the shoulder edges")]
        public float edgeJitter = 1.5f;
        [Tooltip("Size of the wobble features in metres")]
        public float edgeNoiseScale = 6f;

        [Header("Slopes")]
        public bool useSlopes = true;
        [Tooltip("Above this angle grass turns into dirt")]
        public float dirtSlope = 30f;
        [Tooltip("Above this angle dirt turns into rock")]
        public float rockSlope = 42f;
        public float slopeSoftness = 4f;

        [Header("Height tint")]
        public bool useHeight = true;
        [Tooltip("Dark grass in the lowest areas")]
        [Range(0f, 1f)] public float lowlandDark = 0.6f;
        [Tooltip("Light (dry) grass on the highest areas")]
        [Range(0f, 1f)] public float highlandLight = 0.6f;
        [Range(0f, 0.5f)] public float heightBlendNoise = 0.15f;

        [Header("Noise patches")]
        public bool usePatches = true;
        [Tooltip("Size of grass patches in metres")]
        public float patchScale = 25f;
        [Range(0f, 1f)] public float patchCoverage = 0.3f;
        [Range(0.01f, 0.5f)] public float patchSoftness = 0.08f;

        [Header("Under trees (terrain trees and TreeBrush_Objects)")]
        public bool useNeedles = true;
        public float needleRadius = 3f;
        [Tooltip("Regex for TreeBrush object names that count as trees")]
        public string treeNameFilter = "tree|pine|fir|spruce|conifer";

        [Header("Blending")]
        [Tooltip("1 = soft blends, higher = crisp low-poly edges")]
        [Range(1f, 6f)] public float sharpness = 2.5f;
        public int seed = 1234;

        public LayerLook GetLook(PaintLayer layer)
        {
            switch (layer)
            {
                case PaintLayer.GrassMain: return grassMain;
                case PaintLayer.GrassLight: return grassLight;
                case PaintLayer.GrassDark: return grassDark;
                case PaintLayer.Dirt: return dirt;
                case PaintLayer.Gravel: return gravel;
                case PaintLayer.Rock: return rock;
                default: return needles;
            }
        }
    }
}
