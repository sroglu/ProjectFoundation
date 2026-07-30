using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// UI Toolkit <see cref="VisualElement"/> that renders via a <c>UISystem/Shape</c> material
    /// (or any UITK-compatible custom shader). Designed for shared-material theming: assign the
    /// same <see cref="Material"/> to N instances and UI Toolkit's UIR pipeline batches them into
    /// a single Draw Mesh call (validated empirically by feasibility 008 R&D-2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two responsibilities:
    /// <list type="number">
    ///   <item><b>Mesh trigger</b> — UI Toolkit doesn't emit a renderable mesh for a
    ///         <c>VisualElement</c> with no <c>backgroundColor</c> / <c>backgroundImage</c> /
    ///         <c>Painter2D</c>. We set <c>backgroundColor = white</c> on construction as the
    ///         neutral trigger; the shader's <c>_FillColor * IN.color</c> multiply means the
    ///         white background is identity-tinted (no visible change to output).</item>
    ///   <item><b>Material assignment</b> — propagates the inspector-set <see cref="Material"/>
    ///         to <c>style.unityMaterial</c> via <see cref="StyleMaterialDefinition"/>, the
    ///         Unity 6.3 wrapper for UI Toolkit material binding.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Authoring contract: the assigned material's <c>_RectSize</c> property should match the
    /// element's intended size. Runtime resize sync is **deliberately not implemented** here
    /// — doing so requires per-instance state delivery (MaterialPropertyBlock on UIR vs.
    /// per-element material instances vs. vertex-color encoding), which is the same
    /// architectural decision blocking the M3-component SDF unification (spec
    /// <c>009-sdf-unification</c>, AD-001). Both problems will be resolved together once
    /// that spec's Investigation phase picks a delivery mechanism. Until then, callers MUST
    /// size elements to match their material's authored <c>_RectSize</c>, or accept the
    /// aspect distortion documented in spec 008 §13 / Phase 4.5 closure notes.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class GpuSdfElement : VisualElement
    {
        /// <summary>USS class added to every instance for theme targeting.</summary>
        public static readonly string ussClassName = "gpu-sdf-element";

        private Material _material;

        /// <summary>
        /// The shader material applied to this element via <c>IStyle.unityMaterial</c>.
        /// Setting the same Material on multiple <see cref="GpuSdfElement"/> instances yields
        /// a single batched draw call (per UIR pipeline architecture, validated R&amp;D-2).
        /// </summary>
        [UxmlAttribute("material")]
        public Material Material
        {
            get => _material;
            set
            {
                if (_material == value) return;
                _material = value;
                ApplyMaterial();
            }
        }

        public GpuSdfElement()
        {
            AddToClassList(ussClassName);
            // Mesh trigger — UIR emits a renderable quad only when an element has
            // visible content (backgroundColor / backgroundImage / generateVisualContent).
            // White is neutral against `_FillColor * IN.color` shader multiply.
            style.backgroundColor = new StyleColor(Color.white);
        }

        private void ApplyMaterial()
        {
            if (_material != null)
            {
                style.unityMaterial = new StyleMaterialDefinition(_material);
            }
            else
            {
                // Clear the inline override. StyleKeyword.Null is the idiomatic "unset inline
                // style" directive and reads back deterministically as Null; StyleKeyword.Initial
                // is resolved away to Null on readback, so it cannot be observed as Initial.
                style.unityMaterial = new StyleMaterialDefinition(StyleKeyword.Null);
            }
        }
    }
}
