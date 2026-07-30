using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// General GPU-SDF-backed UI Toolkit element — rounded rect / capsule / circle / pill
    /// (via corner-radius manipulation) with optional shadow + outline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Render path: assigns a shared <see cref="Material"/> per <see cref="SdfShapeConfig"/>
    /// category via <see cref="SdfShapeMaterials.GetMaterial"/>. Per-instance fill color is
    /// resolved through <see cref="SdfShapePalette"/> and encoded into the element's
    /// <c>style.backgroundColor</c> (which UIR passes to the shader as <c>Vertex.tint</c>).
    /// </para>
    /// <para>
    /// Subclassed by <c>M3Surface</c> in <c>UISystem.Components.M3</c> to add tonal /
    /// state / ripple overlays. Non-M3 consumers should use <c>SdfShape</c> directly.
    /// </para>
    /// <para><b>Per-instance behavior notes:</b></para>
    /// <list type="bullet">
    ///   <item><b>FillColorOverride</b> is fully per-instance — fast, palette-resolved, no batching loss.</item>
    ///   <item><b>CornerRadii / OutlineThickness / Shadow params</b> select the material category at construction time. Runtime changes trigger a category re-resolve (creates a new cached material if no existing match). Acceptable for theme-driven changes but expensive if mutated per-frame.</item>
    ///   <item><b>OutlineColor / ShadowColor</b> are category-level (set on the shared material). Runtime mutation affects all instances sharing the category — used for theme swap, not per-element runtime override. M3 sets these once at construction.</item>
    /// </list>
    /// </remarks>
    [UxmlElement]
    public partial class SdfShape : VisualElement
    {
        /// <summary>USS class added to every instance for theme targeting.</summary>
        public static readonly string ussClassName = "sdf-shape";

        // ───── Shape (drives material category) ──────────────────────────
        private float _cornerRadius = 12f;
        private float _cornerRadiusTL = -1f;   // -1 = inherit from _cornerRadius
        private float _cornerRadiusTR = -1f;
        private float _cornerRadiusBR = -1f;
        private float _cornerRadiusBL = -1f;

        // ───── Shadow (drives material category) ─────────────────────────
        private float _shadowBlur;
        private float _shadowOffsetX;
        private float _shadowOffsetY;
        private Color _shadowColor = new(0, 0, 0, 0.20f);
        private float _shadowPadding;

        // ───── Outline (drives material category for thickness; color category-level) ─
        private float _outlineThickness;
        private Color _outlineColor = new(0.47f, 0.46f, 0.49f, 1f); // M3 outline

        // ───── Fill (per-instance via palette) ───────────────────────────
        private Color? _fillColorOverride;

        // Internal state
        private SdfShapeConfig _activeConfig;
        private bool _materialDirty = true;

        public SdfShape()
        {
            AddToClassList(ussClassName);
            // Mesh trigger — UIR emits a quad only when an element has visible content
            // (backgroundColor / backgroundImage / generateVisualContent). The tint byte 0
            // carries the palette fill index (default = slot 0 = white).
            style.backgroundColor = new StyleColor(EncodeTint(fillIdx: 0));
            // Bind material on attach. Property setters that run pre-attach can't bind
            // (panel is null) — the attach handler picks up whatever the final config is.
            RegisterCallback<AttachToPanelEvent>(_ => RebindMaterialNow());
            // Layout changes feed into the SDF rect size (spec 026 — material is now
            // chosen per element size so the rounded corners render with the correct
            // aspect). The handler is cheap: when the new layout still hashes to the
            // same SdfShapeConfig (same quantized size), RebindMaterialNow is a no-op.
            RegisterCallback<GeometryChangedEvent>(_ => ScheduleMaterialRebind());
            ScheduleMaterialRebind();
        }

        // ─────────────────────────────────────────────────────────────────
        // Corner radii (5 props — uniform + 4 per-corner)
        // ─────────────────────────────────────────────────────────────────

        [UxmlAttribute("corner-radius")]
        public float CornerRadius { get => _cornerRadius; set { _cornerRadius = value; ScheduleMaterialRebind(); } }

        [UxmlAttribute("corner-radius-tl")]
        public float CornerRadiusTL { get => _cornerRadiusTL; set { _cornerRadiusTL = value; ScheduleMaterialRebind(); } }

        [UxmlAttribute("corner-radius-tr")]
        public float CornerRadiusTR { get => _cornerRadiusTR; set { _cornerRadiusTR = value; ScheduleMaterialRebind(); } }

        [UxmlAttribute("corner-radius-br")]
        public float CornerRadiusBR { get => _cornerRadiusBR; set { _cornerRadiusBR = value; ScheduleMaterialRebind(); } }

        [UxmlAttribute("corner-radius-bl")]
        public float CornerRadiusBL { get => _cornerRadiusBL; set { _cornerRadiusBL = value; ScheduleMaterialRebind(); } }

        // ─────────────────────────────────────────────────────────────────
        // Shadow (8 surfaces — blur, offset×2, padding, color + 4 UXML R/G/B/A)
        // ─────────────────────────────────────────────────────────────────

        [UxmlAttribute("shadow-blur")]
        public float ShadowBlur { get => _shadowBlur; set { _shadowBlur = Mathf.Max(0f, value); ScheduleMaterialRebind(); } }

        [UxmlAttribute("shadow-offset-x")]
        public float ShadowOffsetX { get => _shadowOffsetX; set { _shadowOffsetX = value; ScheduleMaterialRebind(); } }

        [UxmlAttribute("shadow-offset-y")]
        public float ShadowOffsetY { get => _shadowOffsetY; set { _shadowOffsetY = value; ScheduleMaterialRebind(); } }

        public float ShadowPadding { get => _shadowPadding; set { _shadowPadding = Mathf.Max(0f, value); ScheduleMaterialRebind(); } }

        /// <summary>Shadow color (category-level on the shared material). Set at construction; runtime mutation affects all instances in the same category.</summary>
        public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; ApplyCategoryColors(); } }

        [UxmlAttribute("shadow-color-r")]
        public float ShadowColorR { get => _shadowColor.r; set { _shadowColor.r = value; ApplyCategoryColors(); } }

        [UxmlAttribute("shadow-color-g")]
        public float ShadowColorG { get => _shadowColor.g; set { _shadowColor.g = value; ApplyCategoryColors(); } }

        [UxmlAttribute("shadow-color-b")]
        public float ShadowColorB { get => _shadowColor.b; set { _shadowColor.b = value; ApplyCategoryColors(); } }

        [UxmlAttribute("shadow-color-a")]
        public float ShadowColorA { get => _shadowColor.a; set { _shadowColor.a = value; ApplyCategoryColors(); } }

        // ─────────────────────────────────────────────────────────────────
        // Outline (2 surfaces — thickness drives category; color category-level)
        // ─────────────────────────────────────────────────────────────────

        [UxmlAttribute("outline-thickness")]
        public float OutlineThickness { get => _outlineThickness; set { _outlineThickness = Mathf.Max(0f, value); ScheduleMaterialRebind(); } }

        /// <summary>
        /// Outline color — drives material category selection (Color32-quantized into
        /// <see cref="SdfShapeConfig"/>). M3 components mutate this per variant (Filled,
        /// Outlined, Tonal, etc.) so each variant gets its own cached material; instances
        /// within the same variant still batch. Use <see cref="SdfShapeMaterials.CategoryCount"/>
        /// to monitor proliferation.
        /// </summary>
        public Color OutlineColor { get => _outlineColor; set { _outlineColor = value; ScheduleMaterialRebind(); } }

        // ─────────────────────────────────────────────────────────────────
        // Fill (1 surface — per-instance via palette)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Per-instance fill color. When set, palette-resolved and written into the element's
        /// tint byte 0 — fully batching-preserving. When <c>null</c>, falls back to the
        /// design-system default (palette slot 0).
        /// </summary>
        public Color? FillColorOverride
        {
            get => _fillColorOverride;
            set
            {
                _fillColorOverride = value;
                UpdateFillPalette();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Internal mechanics
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Override in subclasses (e.g., M3Surface) to extend tint encoding with M3-specific
        /// state (state overlay opacity, ripple alpha, etc.) in bytes 1-3.
        /// </summary>
        protected virtual Color EncodeTint(int fillIdx)
        {
            // Pure SdfShape: only byte 0 carries fill palette idx. Bytes 1-3 stay zero.
            return new Color32((byte)Mathf.Clamp(fillIdx, 0, SdfShapePalette.MaxSlots - 1), 0, 0, 255);
        }

        /// <summary>Build the current SdfShapeConfig from this element's state.</summary>
        protected virtual SdfShapeConfig BuildConfig()
        {
            float tl = _cornerRadiusTL >= 0 ? _cornerRadiusTL : _cornerRadius;
            float tr = _cornerRadiusTR >= 0 ? _cornerRadiusTR : _cornerRadius;
            float br = _cornerRadiusBR >= 0 ? _cornerRadiusBR : _cornerRadius;
            float bl = _cornerRadiusBL >= 0 ? _cornerRadiusBL : _cornerRadius;
            // The rect is the VISIBLE card area = quad size minus 2× shadow padding (the
            // padding holds room for the drop shadow to bleed beyond the silhouette).
            // Fall back to 100×100 when layout has not resolved yet — that matches the
            // legacy default and gets refined as soon as the first GeometryChanged fires.
            float quadW = layout.width  > 0 ? layout.width  : 100f;
            float quadH = layout.height > 0 ? layout.height : 100f;
            float rectW = Mathf.Max(1f, quadW - _shadowPadding * 2f);
            float rectH = Mathf.Max(1f, quadH - _shadowPadding * 2f);
            return new SdfShapeConfig(
                cornerRadii: new Vector4(tl, tr, br, bl),
                outlineThickness: _outlineThickness,
                shadowBlur: _shadowBlur,
                shadowOffset: new Vector2(_shadowOffsetX, _shadowOffsetY),
                shadowPadding: _shadowPadding,
                outlineColor: _outlineColor,
                rectSize: new Vector2(rectW, rectH));
        }

        private void ScheduleMaterialRebind()
        {
            _materialDirty = true;
            // For an attached element, rebind immediately; otherwise wait until first paint.
            if (panel != null) RebindMaterialNow();
        }

        private void RebindMaterialNow()
        {
            if (!_materialDirty) return;
            var config = BuildConfig();
            if (config.Equals(_activeConfig) && resolvedStyle.unityMaterial.material != null) {
                _materialDirty = false;
                return;
            }
            var mat = SdfShapeMaterials.GetMaterial(config);
            if (mat != null) {
                style.unityMaterial = new StyleMaterialDefinition(mat);
                _activeConfig = config;
                ApplyCategoryColors();   // outline + shadow colors on this material instance
            }
            _materialDirty = false;
        }

        /// <summary>
        /// Push the category-level shadow color to the currently-bound material. OutlineColor
        /// is baked into <see cref="SdfShapeMaterials.GetMaterial"/> from <see cref="SdfShapeConfig"/>
        /// at category creation. ShadowColor stays out of the config hash (rare-mutation, single
        /// theme value across the app); it's updated here in case of theme swap.
        ///
        /// Virtual so subclasses (e.g. M3Surface) can attach extra material setup that must
        /// run every time the material is (re)bound — shader keyword toggling, M3 overlay
        /// uniform priming, etc.
        /// </summary>
        protected virtual void ApplyCategoryColors()
        {
            var matDef = resolvedStyle.unityMaterial;
            var mat = matDef.material;
            if (mat == null) return;
            mat.SetColor("_ShadowColor", _shadowColor);
        }

        private void UpdateFillPalette()
        {
            var color = _fillColorOverride ?? Color.white;
            int idx = SdfShapePalette.Resolve(color);
            // Re-encode tint: byte 0 = new fill idx; preserve bytes 1-3 (subclass M3Surface state)
            var current = resolvedStyle.backgroundColor;
            var tint = EncodeTint(idx);
            // EncodeTint base impl returns (idx, 0, 0, 255). Subclasses can pack other bytes.
            // If subclass uses bytes 1-3, they must call EncodeTint themselves and update.
            style.backgroundColor = new StyleColor(tint);
            SdfShapePalette.FlushToActiveMaterials();
        }
    }
}
