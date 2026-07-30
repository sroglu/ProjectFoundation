using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Shader property name + integer-id catalogue for the <c>UISystem/Shape</c> shader.
    /// Single source of truth shared by the custom Material Inspector + consumer code that
    /// toggles parameters programmatically.
    /// </summary>
    /// <remarks>
    /// String constants are the property names as they appear in the shader Properties block.
    /// Integer ids are pre-cached via <see cref="Shader.PropertyToID(string)"/> to avoid the
    /// per-call name-hash lookup on hot paths (animated materials).
    /// </remarks>
    public static class UIShapeMaterialProperties
    {
        // ─── Shape (always-on) ──────────────────────────────────────────

        /// <summary>Shape type selector (int, 0..3). Maps to <see cref="ShapeType"/>.</summary>
        public const string ShapeType = "_ShapeType";

        /// <summary>Per-corner radii in pixels (Vector4: top-left, top-right, bottom-right, bottom-left). RoundedRect only.</summary>
        public const string CornerRadii = "_CornerRadii";

        /// <summary>Shape size in pixels (Vector4: width, height, unused, unused). The SDF shape's actual dimensions.</summary>
        public const string RectSize = "_RectSize";

        /// <summary>
        /// Quad size in pixels (Vector4: width, height, unused, unused). The UI quad
        /// dimensions — used for UV mapping so the shader can render beyond the shape
        /// edge (drop shadow, glow margin). When <c>(0,0,0,0)</c> the shader falls back
        /// to <see cref="RectSize"/> (shape fills quad). Required &gt; <see cref="RectSize"/>
        /// for visible <see cref="EffectMask.Shadow"/>.
        /// </summary>
        public const string QuadSize = "_QuadSize";

        /// <summary>
        /// AA band width multiplier (Range 0.5..3.0, default 1.0). Scales the
        /// per-pixel anti-aliasing band of the SDF edge.
        /// </summary>
        public const string AAWidth = "_AAWidth";

        // ─── Fill (always-on) ────────────────────────────────────────────

        /// <summary>Base fill color of the shape interior.</summary>
        public const string FillColor = "_FillColor";

        // ─── Gradient effect ─────────────────────────────────────────────

        public const string GradientEnable = "_GradientEnable";
        public const string GradientMode = "_GradientMode";
        public const string GradientAngle = "_GradientAngle";
        public const string GradientFalloff = "_GradientFalloff";
        public const string GradientColorA = "_GradientColorA";
        public const string GradientColorB = "_GradientColorB";

        // ─── Outline effect ──────────────────────────────────────────────

        public const string OutlineEnable = "_OutlineEnable";
        public const string OutlineThickness = "_OutlineThickness";
        public const string OutlineColor = "_OutlineColor";

        // ─── Banding effect ──────────────────────────────────────────────

        public const string BandingEnable = "_BandingEnable";
        public const string BandingSpacing = "_BandingSpacing";
        public const string BandingColorA = "_BandingColorA";
        public const string BandingColorB = "_BandingColorB";

        // ─── Noise effect ────────────────────────────────────────────────

        public const string NoiseEnable = "_NoiseEnable";
        public const string NoiseMode = "_NoiseMode";
        public const string NoiseScale = "_NoiseScale";
        public const string NoiseAmplitude = "_NoiseAmplitude";
        public const string NoiseColor = "_NoiseColor";

        // ─── Dots effect ─────────────────────────────────────────────────

        public const string DotsEnable = "_DotsEnable";
        public const string DotsRadius = "_DotsRadius";
        public const string DotsSpacing = "_DotsSpacing";
        public const string DotsColor = "_DotsColor";

        // ─── Shadow effect ───────────────────────────────────────────────

        public const string ShadowEnable = "_ShadowEnable";
        public const string ShadowOffset = "_ShadowOffset";
        public const string ShadowBlur = "_ShadowBlur";
        public const string ShadowColor = "_ShadowColor";

        // ─── Pre-cached integer ids (hot-path access) ───────────────────

        public static readonly int ShapeTypeId = Shader.PropertyToID(ShapeType);
        public static readonly int CornerRadiiId = Shader.PropertyToID(CornerRadii);
        public static readonly int RectSizeId = Shader.PropertyToID(RectSize);
        public static readonly int QuadSizeId = Shader.PropertyToID(QuadSize);
        public static readonly int AAWidthId = Shader.PropertyToID(AAWidth);
        public static readonly int FillColorId = Shader.PropertyToID(FillColor);

        public static readonly int GradientEnableId = Shader.PropertyToID(GradientEnable);
        public static readonly int GradientModeId = Shader.PropertyToID(GradientMode);
        public static readonly int GradientAngleId = Shader.PropertyToID(GradientAngle);
        public static readonly int GradientFalloffId = Shader.PropertyToID(GradientFalloff);
        public static readonly int GradientColorAId = Shader.PropertyToID(GradientColorA);
        public static readonly int GradientColorBId = Shader.PropertyToID(GradientColorB);

        public static readonly int OutlineEnableId = Shader.PropertyToID(OutlineEnable);
        public static readonly int OutlineThicknessId = Shader.PropertyToID(OutlineThickness);
        public static readonly int OutlineColorId = Shader.PropertyToID(OutlineColor);

        public static readonly int BandingEnableId = Shader.PropertyToID(BandingEnable);
        public static readonly int BandingSpacingId = Shader.PropertyToID(BandingSpacing);
        public static readonly int BandingColorAId = Shader.PropertyToID(BandingColorA);
        public static readonly int BandingColorBId = Shader.PropertyToID(BandingColorB);

        public static readonly int NoiseEnableId = Shader.PropertyToID(NoiseEnable);
        public static readonly int NoiseModeId = Shader.PropertyToID(NoiseMode);
        public static readonly int NoiseScaleId = Shader.PropertyToID(NoiseScale);
        public static readonly int NoiseAmplitudeId = Shader.PropertyToID(NoiseAmplitude);
        public static readonly int NoiseColorId = Shader.PropertyToID(NoiseColor);

        public static readonly int DotsEnableId = Shader.PropertyToID(DotsEnable);
        public static readonly int DotsRadiusId = Shader.PropertyToID(DotsRadius);
        public static readonly int DotsSpacingId = Shader.PropertyToID(DotsSpacing);
        public static readonly int DotsColorId = Shader.PropertyToID(DotsColor);

        public static readonly int ShadowEnableId = Shader.PropertyToID(ShadowEnable);
        public static readonly int ShadowOffsetId = Shader.PropertyToID(ShadowOffset);
        public static readonly int ShadowBlurId = Shader.PropertyToID(ShadowBlur);
        public static readonly int ShadowColorId = Shader.PropertyToID(ShadowColor);
    }
}
