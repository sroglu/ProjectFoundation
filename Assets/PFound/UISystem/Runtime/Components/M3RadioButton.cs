using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Radio Button component.
    ///
    /// Composition:
    ///   VisualElement (this)    — 48dp touch target wrapper
    ///   M3Surface (_outer) — 20×20dp circle, 2dp border
    ///   M3Surface (_inner) — 10×10dp filled circle (hidden when unselected)
    ///   RippleElement (_ripple)
    ///   StateLayerController (_stateLayer)
    ///
    /// M3 Spec:
    ///   Outer: 20×20dp circle (radius=10), 2dp border
    ///   Inner: 10×10dp circle (radius=5) when selected
    ///   Unselected: outline border = --m3-outline
    ///   Selected: border + inner fill = --m3-primary
    ///   Touch target: 48dp wrapper
    ///   Disabled: 0.38 opacity
    ///
    /// USS: radio.uss
    ///
    /// Usage (UXML):
    ///   &lt;components:M3RadioButton selected="true" /&gt;
    /// </summary>
    [UxmlElement]
    public partial class M3RadioButton : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass         = "m3-radio";
        private const string OuterClass        = "m3-radio__outer";
        private const string InnerClass        = "m3-radio__inner";
        private const string LabelClass        = "m3-radio__label";
        private const string SelectedClass     = "m3-radio--selected";
        private const string UnselectedClass   = "m3-radio--unselected";

        // ------------------------------------------------------------------ //
        //  Dimensions (M3 spec)                                                //
        // ------------------------------------------------------------------ //
        private const float OuterSize   = 20f;
        private const float OuterRadius = 10f;
        private const float InnerSize   = 10f;
        private const float InnerRadius = 5f;
        private const float BorderWidth = 2f;

        // Resolved theme colors
        private Color _themePrimary;
        private Color _themeOutline;
        private Color _themeOnSurface;
        private Color _themeSurface;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Surface       _outer;
        private readonly M3Surface       _inner;
        private readonly RippleElement        _ripple;
        private readonly Label               _label;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private bool   _selected;
        private string _text = string.Empty;
        private string _groupName;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when selection changes.</summary>
        public event Action<bool> OnSelectionChanged;

        /// <summary>Internal group changed event (used by M3RadioGroup).</summary>
        internal event Action<M3RadioButton> GroupSelectionRequested;

        /// <summary>Whether this radio button is selected.</summary>
        [UxmlAttribute("selected")]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                ApplyVisualState();
                OnSelectionChanged?.Invoke(_selected);
            }
        }

        /// <summary>When true, dims the radio button and ignores input.</summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set => base.Disabled = value;
        }

        /// <summary>Label text displayed next to the radio button. Clicking the label also selects the radio button (WCAG).</summary>
        [UxmlAttribute("text")]
        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                _label.text = _text;
                _label.style.display = string.IsNullOrEmpty(_text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>Group name for mutual exclusion (optional; use M3RadioGroup for code-based grouping).</summary>
        [UxmlAttribute("group-name")]
        public string GroupName
        {
            get => _groupName;
            set => _groupName = value;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3RadioButton()
        {
            AddToClassList(BaseClass);
            style.flexDirection  = FlexDirection.Row;
            style.alignItems     = Align.Center;
            style.flexShrink     = 0;
            style.marginRight    = 16f;
            style.minHeight      = 48f; // M3 touch target
            pickingMode          = PickingMode.Position;
            focusable            = true;

            // --- Outer circle ---
            _outer = new M3Surface
            {
                CornerRadius = OuterRadius,
                pickingMode  = PickingMode.Position,
            };
            _outer.AddToClassList(OuterClass);
            _outer.style.width      = OuterSize;
            _outer.style.height     = OuterSize;
            _outer.style.flexShrink = 0;
            _outer.style.borderTopLeftRadius     = OuterRadius;
            _outer.style.borderTopRightRadius    = OuterRadius;
            _outer.style.borderBottomLeftRadius  = OuterRadius;
            _outer.style.borderBottomRightRadius = OuterRadius;
            _outer.style.justifyContent = Justify.Center;
            _outer.style.alignItems     = Align.Center;

            // --- Ripple ---
            _ripple = new RippleElement();
            _outer.Add(_ripple);

            // --- Inner circle (shown when selected) ---
            _inner = new M3Surface
            {
                CornerRadius = InnerRadius,
                pickingMode  = PickingMode.Ignore,
            };
            _inner.AddToClassList(InnerClass);
            _inner.style.width  = InnerSize;
            _inner.style.height = InnerSize;
            _inner.style.borderTopLeftRadius     = InnerRadius;
            _inner.style.borderTopRightRadius    = InnerRadius;
            _inner.style.borderBottomLeftRadius  = InnerRadius;
            _inner.style.borderBottomRightRadius = InnerRadius;
            _inner.style.position = Position.Absolute;
            // Absolute position without explicit offsets lets UIR rely on the
            // flex-computed position from the parent, which can land on
            // sub-pixel boundaries and round to a 1-px offset visible as an
            // off-centre dot. Snap to the integer pixel-centred offset
            // explicitly so the dot is always perfectly centred in the ring.
            const float innerOffset = (OuterSize - InnerSize) * 0.5f;
            _inner.style.left = innerOffset;
            _inner.style.top  = innerOffset;
            _inner.style.display  = DisplayStyle.None;
            _outer.Add(_inner);

            // --- Label (hidden by default, shown when Text is set) ---
            _label = new M3Label();
            _label.AddToClassList(LabelClass);
            _label.AddToClassList("m3-body-large");
            _label.style.display    = DisplayStyle.None;
            _label.style.marginLeft = 8f;
            _label.style.whiteSpace = WhiteSpace.NoWrap;
            _label.pickingMode      = PickingMode.Position;

            // --- State layer ---
            InitStateLayer(_outer, _ripple);

            // --- Events: click on entire component (outer + label) selects ---
            RegisterCallback<ClickEvent>(OnClicked);

            Add(_outer);
            Add(_label);
            ApplyVisualState();
        }

        // ------------------------------------------------------------------ //
        //  Visual State                                                        //
        // ------------------------------------------------------------------ //

        private void ApplyVisualState()
        {
            _outer.RemoveFromClassList(SelectedClass);
            _outer.RemoveFromClassList(UnselectedClass);

            // Outer ring's fill is transparent so the SDF circle's interior shows the page
            // background — only the outline draws the affordance. Color.clear lands in
            // palette slot 0 alpha-zero; the EFFECT_OUTLINE_ON path still composites the
            // ring on top, so the affordance survives even though the fragment alpha is 0
            // inside the ring (vertex tint alpha stays 255 thanks to EncodeTint).
            _outer.FillColorOverride = Color.clear;

            if (_selected)
            {
                _outer.AddToClassList(SelectedClass);
                _outer.OutlineThickness = BorderWidth;
                _outer.OutlineColor     = _themePrimary;
                _inner.FillColorOverride = _themePrimary;
                _inner.style.display    = DisplayStyle.Flex;
            }
            else
            {
                _outer.AddToClassList(UnselectedClass);
                _outer.OutlineThickness = BorderWidth;
                _outer.OutlineColor     = _themeOutline;
                _inner.style.display    = DisplayStyle.None;
            }

            StateLayer.OverlayColor = _themeOnSurface;
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimary   = theme.GetColor(ColorRole.Primary);
            _themeOutline   = theme.GetColor(ColorRole.Outline);
            _themeOnSurface = theme.GetColor(ColorRole.OnSurface);
            _themeSurface   = theme.GetColor(ColorRole.Surface);

            ApplyVisualState();
        }

        // ------------------------------------------------------------------ //
        //  Internal: select without firing GroupSelectionRequested             //
        //  (used by M3RadioGroup to avoid re-entrant loops)                   //
        // ------------------------------------------------------------------ //
        internal void SelectSilently()
        {
            if (_selected) return;
            _selected = true;
            ApplyVisualState();
            OnSelectionChanged?.Invoke(_selected);
        }

        internal void DeselectSilently()
        {
            if (!_selected) return;
            _selected = false;
            ApplyVisualState();
            OnSelectionChanged?.Invoke(_selected);
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                      //
        // ------------------------------------------------------------------ //

        private void OnClicked(ClickEvent evt)
        {
            if (base.Disabled || _selected) return;
            GroupSelectionRequested?.Invoke(this);
            if (GroupSelectionRequested == null)
            {
                // Standalone (no group): just select
                Selected = true;
            }
        }
    }
}
