namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Catalogue of shader keyword names for the <c>UISystem/Shape</c> shader.
    /// Single source of truth shared by the custom Material Inspector + consumer code that
    /// toggles effects programmatically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shape-type keywords use <c>multi_compile_local</c> — all four variants ship in every
    /// build because changing the shape at runtime is a supported workflow.
    /// </para>
    /// <para>
    /// Effect keywords use <c>shader_feature_local</c> — Unity strips unused variants at
    /// build time based on which keywords any <c>Material</c> asset in the project enables.
    /// A project that never enables noise doesn't ship the noise variant.
    /// </para>
    /// <para>
    /// Consumer rule: when toggling an effect programmatically, ALWAYS set BOTH the enable
    /// property AND the keyword. The custom Inspector handles this automatically; programmatic
    /// code must do both explicitly. Setting only the property without disabling the keyword
    /// leaves the effect's ALU path enabled (just multiplied by zero — wastes ~30 ALU).
    /// </para>
    /// </remarks>
    public static class UIShapeShaderKeywords
    {
        // ─── Shape-type keywords (multi_compile_local, exclusive) ────────

        public const string ShapeTypeRect = "SHAPE_TYPE_RECT";
        public const string ShapeTypeRoundedRect = "SHAPE_TYPE_ROUNDEDRECT";
        public const string ShapeTypeCapsule = "SHAPE_TYPE_CAPSULE";
        public const string ShapeTypeEllipse = "SHAPE_TYPE_ELLIPSE";

        // ─── Effect-enable keywords (shader_feature_local, independent) ─

        public const string EffectGradientOn = "EFFECT_GRADIENT_ON";
        public const string EffectOutlineOn = "EFFECT_OUTLINE_ON";
        public const string EffectBandingOn = "EFFECT_BANDING_ON";
        public const string EffectNoiseOn = "EFFECT_NOISE_ON";
        public const string EffectDotsOn = "EFFECT_DOTS_ON";
        public const string EffectShadowOn = "EFFECT_SHADOW_ON";

        // ─── Sub-mode keywords (shader_feature_local, only matter when parent effect on) ─

        public const string GradientModeRadial = "GRADIENT_MODE_RADIAL";
        public const string NoiseModeWorley = "NOISE_MODE_WORLEY";
    }
}
