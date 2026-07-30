using System;
using PFound.UISystem.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Theme-agnostic core container built around <see cref="GpuSdfElement"/>. Composes the
    /// shape primitive with a content area and optional ripple/click handling, leaving all
    /// look-and-feel decisions (shape, fill, gradient, outline, shadow) to the assigned
    /// <see cref="Material"/>. This is the M3 felsefesi distilled to primitive form —
    /// elevation/surface/state as composition, not as M3-specific classes.
    /// </summary>
    /// <remarks>
    /// <para><b>Composition:</b></para>
    /// <code>
    /// SdfPanel (this, transparent host VisualElement)
    /// ├── _visual    : GpuSdfElement      — absolute-positioned background layer
    /// └── _clipArea  : VisualElement      — overflow hidden, clips ripple + content
    ///     ├── _ripple : RippleElement     — added only when Clickable = true
    ///     └── _content : VisualElement    — children added via this.Add() land here
    /// </code>
    /// <para><b>Theme integration philosophy:</b></para>
    /// <list type="bullet">
    ///   <item>Shape + elevation + colors → live entirely in the assigned <see cref="Material"/>.</item>
    ///   <item>Each theme (M3 default, Papercut, Candypop, …) authors its own material set; the
    ///         SdfPanel doesn't know which theme it's used in.</item>
    ///   <item>State feedback (ripple/hover/press) is composition-time, not material-time.</item>
    /// </list>
    /// <para><b>Authoring:</b></para>
    /// <code language="xml">
    /// &lt;ui:UXML xmlns:shapes="PFound.UISystem.Shapes"&gt;
    ///   &lt;shapes:SdfPanel material="project://database/.../Card_M3_Elevated.mat" clickable="true"&gt;
    ///     &lt;ui:Label text="Title" /&gt;
    ///   &lt;/shapes:SdfPanel&gt;
    /// &lt;/ui:UXML&gt;
    /// </code>
    /// </remarks>
    [UxmlElement]
    public partial class SdfPanel : VisualElement
    {
        /// <summary>USS class added to every SdfPanel instance.</summary>
        public static readonly string ussClassName = "sdf-panel";

        /// <summary>USS class added to the inner content area (designer's children land here).</summary>
        public static readonly string contentUssClassName = "sdf-panel__content";

        private readonly GpuSdfElement _visual;
        private readonly VisualElement _clipArea;
        private readonly VisualElement _content;
        private RippleElement _ripple;

        private bool _clickable;

        /// <summary>Routes <c>panel.Add(child)</c> into the inner content area.</summary>
        public override VisualElement contentContainer => _content;

        /// <summary>
        /// The shader material that defines this panel's visual identity. All look-and-feel
        /// (shape, fill, gradient, outline, shadow, dots, banding, noise) lives here.
        /// </summary>
        [UxmlAttribute("material")]
        public Material Material
        {
            get => _visual.Material;
            set => _visual.Material = value;
        }

        /// <summary>
        /// When <c>true</c>, attaches a <see cref="RippleElement"/> and listens for clicks.
        /// Hooks no state-layer overlay — that's a higher-level composition (theme-specific).
        /// </summary>
        [UxmlAttribute("clickable")]
        public bool Clickable
        {
            get => _clickable;
            set
            {
                if (_clickable == value) return;
                _clickable = value;
                ApplyClickable();
            }
        }

        /// <summary>Fired when the panel is clicked and <see cref="Clickable"/> is <c>true</c>.</summary>
        public event Action OnClick;

        public SdfPanel()
        {
            AddToClassList(ussClassName);

            // Background visual layer: GpuSdfElement absolute-positioned to fill the panel.
            // pickingMode=Ignore so pointer events pass through to _clipArea (or _content).
            _visual = new GpuSdfElement
            {
                pickingMode = PickingMode.Ignore
            };
            _visual.style.position = Position.Absolute;
            _visual.style.left = 0;
            _visual.style.right = 0;
            _visual.style.top = 0;
            _visual.style.bottom = 0;

            // Clip area: ensures ripple + content don't bleed past the visual layer's silhouette
            // (especially important when the material configures shadow padding via _QuadSize).
            _clipArea = new VisualElement();
            _clipArea.style.flexGrow = 1;
            _clipArea.style.flexDirection = FlexDirection.Column;
            _clipArea.style.overflow = Overflow.Hidden;

            // Content: where designer's children land via contentContainer override.
            _content = new VisualElement();
            _content.AddToClassList(contentUssClassName);
            _content.style.flexGrow = 1;
            _content.style.flexDirection = FlexDirection.Column;

            _clipArea.Add(_content);

            // Z-order: visual rendered first (behind), clipArea second (on top).
            hierarchy.Add(_visual);
            hierarchy.Add(_clipArea);
        }

        private void ApplyClickable()
        {
            if (_clickable && _ripple == null)
            {
                _ripple = new RippleElement();
                _ripple.style.position = Position.Absolute;
                _ripple.style.left = 0;
                _ripple.style.right = 0;
                _ripple.style.top = 0;
                _ripple.style.bottom = 0;
                _ripple.pickingMode = PickingMode.Ignore;
                _clipArea.Insert(0, _ripple); // behind content but inside clip
                _clipArea.RegisterCallback<ClickEvent>(OnClicked);
                pickingMode = PickingMode.Position;
            }
            else if (!_clickable && _ripple != null)
            {
                _clipArea.UnregisterCallback<ClickEvent>(OnClicked);
                _clipArea.Remove(_ripple);
                _ripple = null;
                pickingMode = PickingMode.Ignore;
            }
        }

        private void OnClicked(ClickEvent evt)
        {
            OnClick?.Invoke();
        }
    }
}
