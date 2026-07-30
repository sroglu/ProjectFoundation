using System;
using UnityEngine;

namespace PFound.Render.UIShapes.Editor
{
    /// <summary>
    /// Importer target type for a baked <c>UIShape</c> texture asset.
    /// </summary>
    public enum BakeTargetType
    {
        /// <summary>Imports as <see cref="UnityEngine.Sprite"/> (default — UI consumer path).</summary>
        Sprite = 0,

        /// <summary>Imports as a plain <see cref="UnityEngine.Texture2D"/> (advanced — Image.material consumer path).</summary>
        Texture2D = 1,
    }

    /// <summary>
    /// Color-space the bake output will be authored in. Should match the project's
    /// <see cref="UnityEngine.PlayerSettings.colorSpace"/> to avoid gamma-inversion artifacts.
    /// </summary>
    public enum BakeColorSpace
    {
        /// <summary>sRGB-tagged, gamma-encoded PNG. Default — matches most UI workflows.</summary>
        SRgb = 0,

        /// <summary>Linear-tagged PNG. Use when project is Linear-space AND the bake is consumed by linear-aware shaders.</summary>
        Linear = 1,
    }

    /// <summary>
    /// Configuration for a single bake operation produced by <see cref="UIShapeBakeWindow"/>
    /// and consumed by <see cref="UIShapeBakeService"/>. Serializable so the window can
    /// persist settings across sessions via EditorPrefs.
    /// </summary>
    [Serializable]
    public struct BakeSettings
    {
        /// <summary>Output texture width in pixels. Clamped to <c>[16, 8192]</c>.</summary>
        public int Width;

        /// <summary>Output texture height in pixels. Clamped to <c>[16, 8192]</c>.</summary>
        public int Height;

        /// <summary>When <c>true</c>, height tracks width on edit (square output).</summary>
        public bool LinkAspect;

        /// <summary>Color space the PNG is authored in.</summary>
        public BakeColorSpace ColorSpace;

        /// <summary>Texture filter mode applied to the imported asset.</summary>
        public FilterMode FilterMode;

        /// <summary>Texture wrap mode applied to the imported asset.</summary>
        public TextureWrapMode WrapMode;

        /// <summary>When <c>true</c> and <see cref="TargetType"/> is <see cref="BakeTargetType.Sprite"/>,
        /// the importer generates a tight sprite mesh.</summary>
        public bool GenerateSpriteMesh;

        /// <summary>Importer target type.</summary>
        public BakeTargetType TargetType;

        /// <summary>Filename WITHOUT extension. Validated by <see cref="UIShapeFilenameValidator"/>.</summary>
        public string FileName;

        /// <summary>
        /// Sensible defaults for the first time the bake window opens: 256×256 sRGB
        /// bilinear-clamped Sprite with sprite-mesh generation on, filename
        /// <c>"UIShape_Bake"</c>.
        /// </summary>
        public static BakeSettings Default => new BakeSettings
        {
            Width = 256,
            Height = 256,
            LinkAspect = true,
            ColorSpace = BakeColorSpace.SRgb,
            FilterMode = FilterMode.Bilinear,
            WrapMode = TextureWrapMode.Clamp,
            GenerateSpriteMesh = true,
            TargetType = BakeTargetType.Sprite,
            FileName = "UIShape_Bake",
        };
    }
}
