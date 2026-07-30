using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Shapes;

namespace PFound.UISystem.Components.M3
{
    /// <summary>
    /// Material-3 surface element — extends <see cref="SdfShape"/> with M3-specific
    /// decoration: persistent tonal-elevation overlay (dark-mode primary tint),
    /// transient state-layer overlay (hover/focus/press), and ripple animation.
    /// Built for use as the scaffolding primitive by the 14 M3 components
    /// (M3Card, M3Button, M3Toggle, etc.); non-M3 code should use <c>SdfShape</c> directly.
    /// </summary>
    /// <remarks>
    /// <b>State delivery</b>: opacities pack into <c>Vertex.tint</c> per the SPEC 026 anti-corruption
    /// encoding (a non-zero byte 1 corrupts the UIR tint path, so state opacity was moved out of G):
    /// <list type="bullet">
    ///   <item>byte 0 (R) = (fill palette idx &lt;&lt; 4) | state overlay opacity quantized to 4 bits (0-15)</item>
    ///   <item>byte 1 (G) = 0 (reserved — must stay zero)</item>
    ///   <item>byte 2 (B) = tonal overlay opacity 0-255</item>
    ///   <item>byte 3 (A) = forced to 255 so UIR emits a quad regardless of overlay state</item>
    /// </list>
    /// Overlay colors (<see cref="StateOverlayColor"/>, <see cref="TonalOverlayColor"/>) are
    /// material-level — set at category creation by the M3 theme. Runtime mutation affects
    /// all instances sharing the category (theme swap behavior).
    /// <para>
    /// <b>Ripple lifecycle</b>: while <see cref="RippleAlpha"/> &gt; 0, the element switches
    /// to a per-event material instance (with <c>_M3RippleCenter</c> / <c>_M3RippleRadius</c>
    /// / <c>_M3RippleAlpha</c> set as material uniforms). When <see cref="RippleAlpha"/>
    /// returns to 0, the element reverts to the shared category material — batching
    /// preserved post-ripple.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class M3Surface : SdfShape
    {
        /// <summary>USS class added to every instance for M3-specific theme targeting.</summary>
        public new static readonly string ussClassName = "m3-surface";

        // ───── M3 overlay state ──────────────────────────────────────────
        private float _stateOverlayOpacity;
        private Color _stateOverlayColor = new(0.18f, 0.16f, 0.20f, 1f); // M3 on-surface

        private float _tonalOverlayOpacity;
        private Color _tonalOverlayColor = new(0.40f, 0.32f, 0.65f, 1f); // M3 primary

        // ───── Ripple (per-event material switch) ────────────────────────
        private Vector2 _rippleCenter;
        private float   _rippleRadius;
        private float   _rippleAlpha;
        private Material _rippleMaterial;        // per-event clone, null when ripple inactive
        private Material _baseMaterialDuringRipple;  // remember shared material to revert

        public M3Surface()
        {
            AddToClassList(ussClassName);
        }

        // ─────────────────────────────────────────────────────────────────
        // M3 overlay properties (7 — opacities × 2 + colors × 2 + ripple × 3)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Tonal overlay opacity (0-1) — persistent M3 dark-mode elevation tint.</summary>
        public float TonalOverlayOpacity
        {
            get => _tonalOverlayOpacity;
            set { _tonalOverlayOpacity = Mathf.Clamp01(value); RepackTint(); }
        }

        /// <summary>Tonal overlay color (category-level on shared material). Default = M3 primary.</summary>
        public Color TonalOverlayColor
        {
            get => _tonalOverlayColor;
            set { _tonalOverlayColor = value; PushM3ColorsToMaterial(); }
        }

        /// <summary>State overlay opacity (0-1) — set by StateLayerController for hover/focus/press.</summary>
        public float StateOverlayOpacity
        {
            get => _stateOverlayOpacity;
            set { _stateOverlayOpacity = Mathf.Clamp01(value); RepackTint(); }
        }

        /// <summary>State overlay color (category-level on shared material). Default = M3 on-surface.</summary>
        public Color StateOverlayColor
        {
            get => _stateOverlayColor;
            set { _stateOverlayColor = value; PushM3ColorsToMaterial(); }
        }

        /// <summary>Ripple center in normalized UV (0-1 within element bounds).</summary>
        public Vector2 RippleCenter
        {
            get => _rippleCenter;
            set { _rippleCenter = value; PushRippleToMaterial(); }
        }

        /// <summary>Ripple radius in pixels (animated 0 → max during click).</summary>
        public float RippleRadius
        {
            get => _rippleRadius;
            set { _rippleRadius = Mathf.Max(0f, value); PushRippleToMaterial(); }
        }

        /// <summary>Ripple opacity (0-1, animated). When &gt; 0, switches to per-event material; when 0, reverts to shared.</summary>
        public float RippleAlpha
        {
            get => _rippleAlpha;
            set
            {
                var clamped = Mathf.Clamp01(value);
                bool wasActive = _rippleAlpha > 0f;
                bool isActive = clamped > 0f;
                _rippleAlpha = clamped;
                if (!wasActive && isActive) EnterRippleMode();
                else if (wasActive && !isActive) ExitRippleMode();
                PushRippleToMaterial();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Tint encoding override
        // ─────────────────────────────────────────────────────────────────

        protected override Color EncodeTint(int fillIdx)
        {
            // SPEC 026 ENCODING — fillIdx + state opacity packed into byte 0 so byte 1
            // can stay 0. UIR's Linear-space pipeline corrupts the entire backgroundColor
            // when byte 1 (G) becomes non-zero, which would normally hold state opacity:
            // the rendered fill goes pure white on every hover/press. Packing into a
            // single byte avoids the corrupting code path entirely.
            //
            // byte 0 = (fillIdx << 4) | stateOp4 — 4 bits fillIdx (0-15), 4 bits state
            //          opacity quantized to 16 levels.
            // byte 1 = 0 (reserved — DO NOT use for opacity).
            // byte 2 = tonal opacity (8 bits, same gotcha applies; left as-is for now
            //          because the tonal overlay isn't driven by hover/press today).
            // byte 3 = alpha, forced to 255.
            int clampedFill = Mathf.Clamp(fillIdx, 0, SdfShapePalette.MaxSlots - 1);
            int stateOp4 = Mathf.Clamp(Mathf.RoundToInt(_stateOverlayOpacity * 15f), 0, 15);
            byte r = (byte)((clampedFill << 4) | stateOp4);
            byte b = (byte)Mathf.RoundToInt(_tonalOverlayOpacity * 255f);
            return new Color32(r, 0, b, 255);
        }

        /// <summary>
        /// Repack the tint Color32 with the current state/tonal opacities (preserving
        /// the existing fill palette idx). Decodes byte 0 with the spec-026 layout —
        /// raw byte = (fillIdx &lt;&lt; 4) | stateOp4, so the high nibble carries the
        /// palette idx. Reading the raw byte value as fillIdx would clamp anything &gt;15
        /// to 15 (palette slot 15 = transparent) and silently make the FAB / Card go
        /// invisible whenever state opacity flips above 0.
        /// </summary>
        private void RepackTint()
        {
            var current = resolvedStyle.backgroundColor;
            int rawR = Mathf.RoundToInt(current.r * 255f);
            int fillIdx = rawR >> 4;
            style.backgroundColor = new StyleColor(EncodeTint(fillIdx));
        }

        // ─────────────────────────────────────────────────────────────────
        // Material plumbing
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Push the M3 overlay colors to the currently-bound material's uniforms. These
        /// are category-level — affects all M3Surface instances sharing this category.
        /// </summary>
        private void PushM3ColorsToMaterial()
        {
            var mat = ActiveMaterial();
            if (mat == null) return;
            mat.EnableKeyword("EFFECT_M3_OVERLAYS_ON");
            mat.SetColor("_M3StateOverlayColor", _stateOverlayColor);
            mat.SetColor("_M3TonalOverlayColor", _tonalOverlayColor);
        }

        /// <summary>
        /// Hooks into the SdfShape material-bind callback so the EFFECT_M3_OVERLAYS_ON
        /// keyword is enabled even when the consumer never explicitly assigns
        /// <see cref="StateOverlayColor"/> or <see cref="TonalOverlayColor"/>. Without
        /// this, the M3 tint encoding (which routes tonal opacity into Vertex.tint.a)
        /// would let UIR's standard alpha-multiply clip the element to invisible.
        /// </summary>
        protected override void ApplyCategoryColors()
        {
            base.ApplyCategoryColors();
            PushM3ColorsToMaterial();
        }

        private void PushRippleToMaterial()
        {
            var mat = _rippleMaterial;  // ripple-only on per-event material
            if (mat == null) return;
            mat.SetVector("_M3RippleCenter", new Vector4(_rippleCenter.x, _rippleCenter.y, 0, 0));
            mat.SetFloat("_M3RippleRadius", _rippleRadius);
            mat.SetFloat("_M3RippleAlpha", _rippleAlpha);
        }

        private void EnterRippleMode()
        {
            var shared = ActiveMaterial();
            if (shared == null) return;
            _baseMaterialDuringRipple = shared;
            _rippleMaterial = new Material(shared) { name = shared.name + "_RippleEvent", hideFlags = HideFlags.DontSave };
            _rippleMaterial.EnableKeyword("EFFECT_M3_OVERLAYS_ON");
            style.unityMaterial = new StyleMaterialDefinition(_rippleMaterial);
        }

        private void ExitRippleMode()
        {
            if (_baseMaterialDuringRipple != null)
            {
                style.unityMaterial = new StyleMaterialDefinition(_baseMaterialDuringRipple);
            }
            if (_rippleMaterial != null) Object.DestroyImmediate(_rippleMaterial);
            _rippleMaterial = null;
            _baseMaterialDuringRipple = null;
        }

        private Material ActiveMaterial()
        {
            return _rippleMaterial != null ? _rippleMaterial : resolvedStyle.unityMaterial.material;
        }
    }
}
