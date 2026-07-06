using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style interactive Button with 4 style variants: Filled, Outlined, Text, Tonal.
    /// Supports 5 size variants (XS–XL), 2 shape variants (Round/Square), and compact padding.
    ///
    /// Composition:
    ///   M3Surface (_root) — rounded pill container, receives pointer events
    ///   RippleElement  (_ripple) — M3 press ripple, child of _root
    ///   Label          (_label) — button text, child of _root
    ///   VisualElement  (_iconSlot) — optional leading icon, hidden by default
    ///   StateLayerController (_stateLayer) — hover/press/focused/disabled feedback
    ///
    /// USS: button.uss for variant/size/shape styles, state-layer.uss for disabled state.
    /// All colors via var(--m3-*) tokens — no hardcoded colors.
    ///
    /// Usage (C#):
    ///   var btn = new M3Button { Text = "Click", Variant = ButtonVariant.Filled, Size = ButtonSize.Large };
    ///   btn.OnClick += () => Debug.Log("clicked");
    ///   root.Add(btn);
    ///
    /// Usage (UXML):
    ///   &lt;PFound.UISystem.Components.M3Button variant="Filled" size="Large" shape="Square" text="Button" /&gt;
    /// </summary>
    [UxmlElement]
    public partial class M3Button : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                  //
        // ------------------------------------------------------------------ //
        private const string BaseClass  = "m3-button";
        private const string LabelClass = "m3-button__label";

        private static readonly string[] VariantClasses =
        {
            "m3-button--filled",
            "m3-button--outlined",
            "m3-button--text",
            "m3-button--tonal",
        };

        private static readonly string[] LabelVariantClasses =
        {
            "m3-button__label--filled",
            "m3-button__label--outlined",
            "m3-button__label--text",
            "m3-button__label--tonal",
        };

        private static readonly string[] SizeClasses =
        {
            "m3-button--xs",
            "m3-button--sm",
            "",                // Medium = base class, no extra class needed
            "m3-button--lg",
            "m3-button--xl",
        };

        private static readonly string[] LabelSizeClasses =
        {
            "m3-button__label--xs",
            "m3-button__label--sm",
            "",
            "m3-button__label--lg",
            "m3-button__label--xl",
        };

        private const string ShapeSquareClass  = "m3-button--square";
        private const string CompactClass      = "m3-button--compact";

        // M3 elevation-2 values (Filled only)
        private const float FilledShadowBlur    = 6f;
        private const float FilledShadowOffsetY = 2f;

        // Corner radii
        private const float CornerRadiusRound  = 9999f;
        private const float CornerRadiusSquare = 8f;

        // Height per size (used to compute CSS border-radius = height/2 for circular pill clips)
        // Unity applies border-radius: 9999px as elliptical (x-radius=width/2, y-radius=height/2),
        // which creates an oval. Setting border-radius explicitly to height/2 forces circular corners.
        private static readonly float[] SizeHeights = { 24f, 32f, 40f, 56f, 96f };
        private static readonly float[] SizePillRadii = { 12f, 16f, 20f, 28f, 28f };

        // Resolved theme colors
        private Color _themePrimary;
        private Color _themeOnPrimary;
        private Color _themeOutline;
        private Color _themeSecondaryContainer;
        private Color _themeOnSecondaryContainer;

        // ------------------------------------------------------------------ //
        //  Children                                                             //
        // ------------------------------------------------------------------ //
        private readonly M3Surface            _root;
        private readonly RippleElement        _ripple;
        private readonly Label                _label;
        private readonly VisualElement        _iconSlot;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                       //
        // ------------------------------------------------------------------ //
        private string        _text    = "Button";
        private ButtonVariant _variant = ButtonVariant.Filled;
        private ButtonSize    _size    = ButtonSize.Medium;
        private ButtonShape   _shape   = ButtonShape.Round;
        private bool          _compact;

        // ------------------------------------------------------------------ //
        //  Public API                                                           //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the button is clicked and not disabled.</summary>
        public event Action OnClick;

        /// <summary>Button label text.</summary>
        [UxmlAttribute("text")]
        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                _label.text = _text;
            }
        }

        /// <summary>Visual variant — Filled, Outlined, Text, or Tonal.</summary>
        [UxmlAttribute("variant")]
        public ButtonVariant Variant
        {
            get => _variant;
            set { _variant = value; ApplyVariant(_variant); }
        }

        /// <summary>Size scale — ExtraSmall through ExtraLarge. Default: Medium.</summary>
        [UxmlAttribute("size")]
        public ButtonSize Size
        {
            get => _size;
            set { _size = value; ApplySize(_size); }
        }

        /// <summary>Corner shape — Round (pill) or Square (8dp radius). Default: Round.</summary>
        [UxmlAttribute("shape")]
        public ButtonShape Shape
        {
            get => _shape;
            set { _shape = value; ApplyShape(_shape); }
        }

        /// <summary>
        /// When true, reduces horizontal padding from 24dp to 16dp (M3 recommended compact).
        /// </summary>
        [UxmlAttribute("compact")]
        public bool Compact
        {
            get => _compact;
            set
            {
                _compact = value;
                if (_compact)
                    _root.AddToClassList(CompactClass);
                else
                    _root.RemoveFromClassList(CompactClass);
            }
        }

        /// <summary>
        /// When true the button dims to 38% opacity and ignores all input.
        /// Delegates to <see cref="StateLayerController.Disabled"/>.
        /// </summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set => base.Disabled = value;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                          //
        // ------------------------------------------------------------------ //

        public M3Button()
        {
            // --- ensure this wrapper sizes to its content, not stretched by parent ---
            style.alignSelf  = Align.FlexStart;
            // --- prevent _root from stretching to fill wrapper width (column default is stretch) ---
            style.alignItems = Align.FlexStart;

            // --- root container (pill-shaped SDFRect) ---
            _root = new M3Surface
            {
                CornerRadius = CornerRadiusRound,
                pickingMode  = PickingMode.Position,
            };
            _root.AddToClassList(BaseClass);

            // --- ripple overlay ---
            _ripple = new RippleElement();
            _root.Add(_ripple);

            // --- icon slot (hidden by default) ---
            _iconSlot = new VisualElement();
            _iconSlot.AddToClassList("m3-button__icon");
            _iconSlot.style.display = DisplayStyle.None;
            _root.Add(_iconSlot);

            // --- label ---
            _label = new M3Label(_text);
            _label.AddToClassList(LabelClass);
            // Reset inherited unityMaterial: SdfShape parent sets style.unityMaterial to
            // the SDF shape material; UIR propagates that to descendants. A Label rendered
            // through the SDF shader produces corrupted glyphs (the "/_  /_" pattern).
            // StyleKeyword.Initial reverts the label to the default UI Toolkit text material.
            _label.style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
            // Optical centering: Roboto's ascender > descender causes the cap-height to sit
            // above the mathematical center. A small top-padding compensates, pushing the
            // visible text down to the visual center of the pill.
            _label.style.paddingTop = 1f;
            _root.Add(_label);

            // --- state layer ---
            InitStateLayer(_root, _ripple);

            // --- click ---
            _root.RegisterCallback<ClickEvent>(OnRootClicked);

            // --- add root as sole child of this element ---
            Add(_root);

            // --- apply defaults ---
            ApplyVariant(ButtonVariant.Filled);

            // Default: Medium (40px) + Round → CSS border-radius = 20px (= height/2).
            // Must be set explicitly because Unity renders border-radius: 9999px as an ellipse
            // (x-radius = width/2, y-radius = height/2), clipping the shape to an oval.
            // Explicit height/2 forces circular corner arcs → proper pill shape.
            const float defaultPillRadius = 20f;
            _root.style.borderTopLeftRadius     = defaultPillRadius;
            _root.style.borderTopRightRadius    = defaultPillRadius;
            _root.style.borderBottomLeftRadius  = defaultPillRadius;
            _root.style.borderBottomRightRadius = defaultPillRadius;
            _ripple.SetClipRadius(defaultPillRadius);
        }

        // ------------------------------------------------------------------ //
        //  Variant                                                              //
        // ------------------------------------------------------------------ //

        private void ApplyVariant(ButtonVariant v)
        {
            foreach (var cls in VariantClasses)
                _root.RemoveFromClassList(cls);
            foreach (var cls in LabelVariantClasses)
                _label.RemoveFromClassList(cls);

            int idx = (int)v;
            if (idx < 0 || idx >= VariantClasses.Length) return;

            _root.AddToClassList(VariantClasses[idx]);
            _label.AddToClassList(LabelVariantClasses[idx]);

            // Elevation shadow: Filled only (M3 elevation-2)
            if (v == ButtonVariant.Filled)
            {
                _root.ShadowBlur    = FilledShadowBlur;
                _root.ShadowOffsetY = FilledShadowOffsetY;
            }
            else
            {
                _root.ShadowBlur    = 0f;
                _root.ShadowOffsetY = 0f;
            }

            // SDF outline: Outlined variant only
            _root.OutlineThickness = (v == ButtonVariant.Outlined) ? 1f : 0f;

            // Inline colors (USS var() doesn't resolve dynamically on theme change)
            switch (v)
            {
                case ButtonVariant.Filled:
                    _root.FillColorOverride = _themePrimary;
                    _root.OutlineColor      = Color.clear;
                    break;
                case ButtonVariant.Outlined:
                    _root.FillColorOverride = Color.clear;
                    _root.OutlineColor      = _themeOutline;
                    break;
                case ButtonVariant.Text:
                    _root.FillColorOverride = Color.clear;
                    _root.OutlineColor      = Color.clear;
                    break;
                case ButtonVariant.Tonal:
                    _root.FillColorOverride = _themeSecondaryContainer;
                    _root.OutlineColor      = Color.clear;
                    break;
            }

            // State + ripple tints. Both must contrast against the variant's resting
            // background so the hover overlay AND the click ripple are visible.
            // Filled/Tonal have a coloured fill → use on-primary (white) for contrast.
            // Outlined/Text have a transparent (white) fill → a white ripple is
            // invisible; tint the ripple with primary so the click feedback shows.
            // Opacities bumped above the M3 baseline so the press feedback reads
            // clearly without the system-cursor / click-sound that web M3 has.
            if (v == ButtonVariant.Filled || v == ButtonVariant.Tonal)
            {
                StateLayer.OverlayColor = _themeOnPrimary;
                _ripple.RippleColor     = _themeOnPrimary;
                _ripple.PeakOpacity     = 0.25f;
            }
            else
            {
                StateLayer.OverlayColor = _themePrimary;
                _ripple.RippleColor     = _themePrimary;
                _ripple.PeakOpacity     = 0.35f;
            }
        }

        // ------------------------------------------------------------------ //
        //  Size                                                                 //
        // ------------------------------------------------------------------ //

        private void ApplySize(ButtonSize s)
        {
            foreach (var cls in SizeClasses)
                if (!string.IsNullOrEmpty(cls)) _root.RemoveFromClassList(cls);
            foreach (var cls in LabelSizeClasses)
                if (!string.IsNullOrEmpty(cls)) _label.RemoveFromClassList(cls);

            int idx = (int)s;
            if (idx < 0 || idx >= SizeClasses.Length) return;

            if (!string.IsNullOrEmpty(SizeClasses[idx]))
                _root.AddToClassList(SizeClasses[idx]);
            if (!string.IsNullOrEmpty(LabelSizeClasses[idx]))
                _label.AddToClassList(LabelSizeClasses[idx]);

            // SDF corner radius: 9999 for pill (clamped internally), 28 for XL
            _root.CornerRadius = (s == ButtonSize.ExtraLarge) ? 28f : CornerRadiusRound;

            // CSS border-radius: must be height/2 (not 9999!) to get circular corners.
            // Unity renders border-radius: 9999px elliptically (x=width/2, y=height/2),
            // clipping the shape to an oval. Using explicit height/2 gives circular arcs.
            float br = _shape == ButtonShape.Square ? CornerRadiusSquare : SizePillRadii[idx];
            _root.style.borderTopLeftRadius     = br;
            _root.style.borderTopRightRadius    = br;
            _root.style.borderBottomLeftRadius  = br;
            _root.style.borderBottomRightRadius = br;
            _ripple.SetClipRadius(br);
        }

        // ------------------------------------------------------------------ //
        //  Shape                                                                //
        // ------------------------------------------------------------------ //

        private void ApplyShape(ButtonShape sh)
        {
            if (sh == ButtonShape.Square)
            {
                _root.AddToClassList(ShapeSquareClass);
                _root.CornerRadius = CornerRadiusSquare;
                _root.style.borderTopLeftRadius     = CornerRadiusSquare;
                _root.style.borderTopRightRadius    = CornerRadiusSquare;
                _root.style.borderBottomLeftRadius  = CornerRadiusSquare;
                _root.style.borderBottomRightRadius = CornerRadiusSquare;
                _ripple.SetClipRadius(CornerRadiusSquare);
            }
            else
            {
                _root.RemoveFromClassList(ShapeSquareClass);
                // SDF: 9999 (clamped to height/2 internally), 28 for XL
                _root.CornerRadius = (_size == ButtonSize.ExtraLarge) ? 28f : CornerRadiusRound;
                // CSS: use height/2 for circular corners (not 9999, which creates an ellipse in Unity)
                float cr = SizePillRadii[(int)_size];
                _root.style.borderTopLeftRadius     = cr;
                _root.style.borderTopRightRadius    = cr;
                _root.style.borderBottomLeftRadius  = cr;
                _root.style.borderBottomRightRadius = cr;
                _ripple.SetClipRadius(cr);
            }
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                         //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimary              = theme.GetColor(ColorRole.Primary);
            _themeOnPrimary            = theme.GetColor(ColorRole.OnPrimary);
            _themeOutline              = theme.GetColor(ColorRole.Outline);
            _themeSecondaryContainer   = theme.GetColor(ColorRole.SecondaryContainer);
            _themeOnSecondaryContainer = theme.GetColor(ColorRole.OnSecondaryContainer);

            ApplyVariant(_variant);
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                       //
        // ------------------------------------------------------------------ //

        private void OnRootClicked(ClickEvent evt)
        {
            if (base.Disabled) return;
            OnClick?.Invoke();
        }
    }
}
