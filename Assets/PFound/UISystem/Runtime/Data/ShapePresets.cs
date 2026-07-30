using System;

namespace PFound.UISystem.Data
{
    /// <summary>
    /// Corner radius presets in reference-resolution pixels (1080x1920).
    /// Based on M3 shape scale: None, ExtraSmall, Small, Medium, Large, ExtraLarge, Full.
    /// </summary>
    [Serializable]
    public struct ShapePresets
    {
        /// <summary>0 dp — no rounding.</summary>
        public float None;

        /// <summary>4 dp ≈ 10.5 ref px.</summary>
        public float ExtraSmall;

        /// <summary>8 dp ≈ 21 ref px.</summary>
        public float Small;

        /// <summary>12 dp ≈ 31.5 ref px — used by cards.</summary>
        public float Medium;

        /// <summary>16 dp ≈ 42 ref px — used by large cards.</summary>
        public float Large;

        /// <summary>28 dp ≈ 73.5 ref px — used by dialogs.</summary>
        public float ExtraLarge;

        /// <summary>9999 — fully rounded / pill shape — used by buttons.</summary>
        public float Full;

        public static ShapePresets Default => new ShapePresets
        {
            None       = 0f,
            ExtraSmall = 10.5f,
            Small      = 21f,
            Medium     = 31.5f,
            Large      = 42f,
            ExtraLarge = 73.5f,
            Full       = 9999f
        };
    }
}
