using System.Collections.Generic;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Category-aware resolver for shared materials used by all <see cref="SdfShape"/> /
    /// <c>M3Surface</c> instances. Each unique <see cref="SdfShapeConfig"/> (quantized
    /// shape geometry — corner radii, outline, shadow) maps to one cached `Material`
    /// instance derived from the two base assets:
    /// <list type="bullet">
    ///   <item><c>SdfShape_NoShadow.mat</c> — base, shadow keyword OFF (FR-009 short-circuit)</item>
    ///   <item><c>SdfShape_WithShadow.mat</c> — base, shadow keyword ON</item>
    /// </list>
    /// On first request for a given config, the appropriate base is cloned and uniforms
    /// (corner radii, outline, shadow params) are baked in. The clone is cached forever
    /// (or until <see cref="ClearCache"/>). All instances sharing one config + same fill
    /// palette indices batch to a single UIR draw call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Material count grows as O(unique configs). Empirical M3 usage: 14 component types
    /// × ~2 states each ≈ 28-40 cached materials. Memory ≈ 28-40 KB. Acceptable.
    /// </para>
    /// <para>
    /// <b>Palette flush</b>: <see cref="SdfShape.OnGenerateVisualContent"/> calls
    /// <see cref="SdfShapePalette.FlushToMaterials"/> with the resolved material before
    /// emitting vertices each frame. Flush is dirty-flag-coalesced — no per-frame cost
    /// when the palette is stable.
    /// </para>
    /// </remarks>
    public static class SdfShapeMaterials
    {
        private const string NoShadowAssetPath = "UISystem/SdfShape_NoShadow";
        private const string WithShadowAssetPath = "UISystem/SdfShape_WithShadow";
        private const string PaletteKeyword = "EFFECT_VERTEX_TINT_PALETTE_ON";
        private const string ShadowKeyword  = "EFFECT_SHADOW_ON";
        private const string OutlineKeyword = "EFFECT_OUTLINE_ON";

        private static Material _baseNoShadow;
        private static Material _baseWithShadow;
        private static readonly Dictionary<SdfShapeConfig, Material> _categoryCache = new();

        /// <summary>
        /// Resolve the cached shared material for the given shape <paramref name="config"/>.
        /// On first call per config, clones the base material and bakes the static uniforms
        /// (corner radii, outline, shadow). On subsequent calls, returns the cached clone.
        /// </summary>
        public static Material GetMaterial(SdfShapeConfig config)
        {
            if (_categoryCache.TryGetValue(config, out var mat)) return mat;

            var baseMat = config.HasShadow ? EnsureBaseWithShadow() : EnsureBaseNoShadow();
            if (baseMat == null) return null;

            mat = new Material(baseMat) { name = $"SdfShape_Cat_{config.GetHashCode():X8}", hideFlags = HideFlags.DontSave };
            // Bake the static uniforms for this category
            mat.SetVector("_CornerRadii", config.CornerRadiiVector);
            // RectSize is the SDF body in px (visible card area). QuadSize is the actual
            // UIR quad which equals RectSize for shadowless variants and RectSize + 2 ×
            // ShadowPadding when shadows are enabled (the padding is the room the SDF
            // shadow tail needs beyond the silhouette). Both come from SdfShapeConfig so
            // every unique element size gets its own cached material — same-size cards
            // still share a single material and still batch.
            var rectSize = config.RectSizeVector;
            float padDeq = config.ShadowPadding * 0.5f;
            var quadSize = new Vector2(rectSize.x + padDeq * 2f, rectSize.y + padDeq * 2f);
            mat.SetVector("_RectSize", new Vector4(rectSize.x, rectSize.y, 0, 0));
            mat.SetVector("_QuadSize", new Vector4(quadSize.x, quadSize.y, 0, 0));
            mat.SetFloat("_OutlineThickness", config.OutlineThickness * 0.5f);  // dequantize
            mat.SetFloat("_OutlineEnable", config.OutlineThickness > 0 ? 1f : 0f);
            // The shader uses #pragma shader_feature_local for EFFECT_OUTLINE_ON; the [Toggle]
            // attribute on _OutlineEnable only flips the keyword in the Editor inspector, not
            // at runtime SetFloat time, so we must enable it explicitly here whenever a
            // category requests an outline. Without this the outline path in
            // UIShape_Composite is dead and outlined components render unbordered.
            if (config.OutlineThickness > 0f)
                mat.EnableKeyword(OutlineKeyword);
            else
                mat.DisableKeyword(OutlineKeyword);
            // Same story for EFFECT_SHADOW_ON: the per-category clone may not inherit the
            // keyword from the base material (Unity's clone copies shader_feature_local
            // keywords inconsistently for shipped .mat assets). Enable it here whenever the
            // config carries any shadow parameter. Without this Elevated cards render
            // bodies but no drop shadow even though _ShadowBlur / _ShadowOffset are set.
            if (config.HasShadow)
                mat.EnableKeyword(ShadowKeyword);
            else
                mat.DisableKeyword(ShadowKeyword);
            mat.SetColor("_OutlineColor", config.OutlineColor);  // per-category outline color
            mat.SetFloat("_ShadowEnable", config.HasShadow ? 1f : 0f);
            mat.SetFloat("_ShadowBlur", config.ShadowBlur * 0.5f);
            // UIR's UV coordinate space has y INCREASING DOWNWARD, but the SDF shader
            // treats the post-center uv as a Cartesian plane (positive y = up). To make
            // M3's "shadow offset y = +4" mean "shadow visually drops 4px below the
            // card" we flip the y axis of _ShadowOffset before handing it to the shader.
            // Without this flip the shadow rises above the silhouette and looks like a
            // halo on the top edge of every elevated card.
            mat.SetVector("_ShadowOffset", new Vector4(config.ShadowOffsetX * 0.5f, -config.ShadowOffsetY * 0.5f, 0, 0));
            // Outline/Shadow colors come from palette; default white in slot 0 until SdfShape sets them
            // Palette uniform will be populated by SdfShapePalette.FlushToMaterials() per frame

            _categoryCache[config] = mat;
            // A freshly cloned category material has _ColorPalette set to the base
            // material's frozen default (slot 0 = white). The next normal
            // SdfShapePalette.FlushToActiveMaterials uploads only when its dirty flag is
            // set, so elements that bound to this material first paint with the wrong
            // (empty) palette and rely on the consumer re-touching FillColorOverride —
            // which is exactly the "Slider blank on first open, fine after a re-open"
            // symptom. Push the live palette now so the material is correct from frame 1.
            SdfShapePalette.MarkDirty();
            SdfShapePalette.FlushToActiveMaterials();
            return mat;
        }

        /// <summary>
        /// Force a fresh resolve on the next <see cref="GetMaterial"/> call. Used by tests
        /// to isolate state and by editor tooling after asset reimports.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var mat in _categoryCache.Values) {
                if (mat != null) Object.DestroyImmediate(mat);
            }
            _categoryCache.Clear();
            _baseNoShadow = null;
            _baseWithShadow = null;
            SdfShapePalette.ClearAll();
        }

        /// <summary>Diagnostic: how many unique categories currently have a cached material.</summary>
        public static int CategoryCount => _categoryCache.Count;

        /// <summary>Enumerate all live cached materials — used by <see cref="SdfShapePalette"/> for flush.</summary>
        public static IEnumerable<Material> ActiveMaterials => _categoryCache.Values;

        private static Material EnsureBaseNoShadow()
        {
            if (_baseNoShadow == null) _baseNoShadow = ResolveOrCreate(NoShadowAssetPath, hasShadow: false);
            return _baseNoShadow;
        }

        private static Material EnsureBaseWithShadow()
        {
            if (_baseWithShadow == null) _baseWithShadow = ResolveOrCreate(WithShadowAssetPath, hasShadow: true);
            return _baseWithShadow;
        }

        private static Material ResolveOrCreate(string resourcePath, bool hasShadow)
        {
            var mat = Resources.Load<Material>(resourcePath);
            if (mat != null) return mat;

            var shader = Shader.Find("UISystem/Shape");
            if (shader == null)
            {
                Debug.LogError("[SdfShapeMaterials] UISystem/Shape shader not found — cannot create fallback material.");
                return null;
            }
            mat = new Material(shader) { name = $"SdfShape_{(hasShadow ? "WithShadow" : "NoShadow")}_Runtime", hideFlags = HideFlags.DontSave };
            mat.EnableKeyword(PaletteKeyword);
            if (hasShadow) mat.EnableKeyword(ShadowKeyword);
            Debug.LogWarning($"[SdfShapeMaterials] Resources/{resourcePath}.mat missing — using runtime fallback. Ship-blocking issue for production: create the asset via T008.");
            return mat;
        }
    }
}
