using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using PFound.UISystem.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Chip component.
    ///
    /// Variants:
    ///   Assist     — icon + label, no toggle
    ///   Filter     — toggleable selected state, leading check icon when selected
    ///   Input      — label + trailing X (remove) button
    ///   Suggestion — label only, no toggle
    ///
    /// M3 Spec:
    ///   Height: 32dp, corners: 8dp (shape-small)
    ///   Border: 1dp outline
    ///   Horizontal padding: 12dp (16dp with leading/trailing icons)
    ///
    /// USS: chip.uss
    /// </summary>
    [UxmlElement]
    public partial class M3Chip : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass          = "m3-chip";
        private const string ContainerClass     = "m3-chip__container";
        private const string LeadingIconClass   = "m3-chip__leading-icon";
        private const string LabelClass         = "m3-chip__label";
        private const string TrailingIconClass  = "m3-chip__trailing-icon";
        private const string SelectedClass      = "m3-chip--selected";
        private const string UnselectedClass    = "m3-chip--unselected";

        // Resolved theme colors (read from ThemeData via ThemeManager)
        private Color _themeOutline;
        private Color _themeOnSurface;
        private Color _themeSurface;
        private Color _themeSecondaryContainer;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Surface      _container;
        private readonly Label          _leadingIcon;
        private readonly Label          _label;
        private readonly Label          _trailingIcon;
        private readonly RippleElement  _ripple;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string      _text;
        private ChipVariant _variant;
        private bool        _selected;
        private ChipIcon    _icon;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the chip is clicked (all variants).</summary>
        public event Action OnClick;

        /// <summary>Fired when the trailing X is pressed (Input variant only).</summary>
        public event Action OnRemove;

        /// <summary>Chip label text.</summary>
        [UxmlAttribute("text")]
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                _label.text = value ?? string.Empty;
            }
        }

        /// <summary>Chip variant (Assist, Filter, Input, Suggestion).</summary>
        [UxmlAttribute("variant")]
        public ChipVariant Variant
        {
            get => _variant;
            set
            {
                _variant = value;
                ApplyVariant();
            }
        }

        /// <summary>Selected state (Filter variant only).</summary>
        [UxmlAttribute("selected")]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_variant != ChipVariant.Filter) return;
                _selected = value;
                ApplySelectedState();
            }
        }

        /// <summary>When true, dims the chip and ignores input.</summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set => base.Disabled = value;
        }

        /// <summary>Leading icon for Assist variant. Ignored for other variants.</summary>
        [UxmlAttribute("icon")]
        public ChipIcon Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                ApplyVariant();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Chip()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;

            // --- Container (the visible chip body) ---
            _container = new M3Surface
            {
                CornerRadius = 8f,
                pickingMode  = PickingMode.Position,
            };
            _container.AddToClassList(ContainerClass);
            _container.style.borderTopLeftRadius     = 8f;
            _container.style.borderTopRightRadius    = 8f;
            _container.style.borderBottomLeftRadius  = 8f;
            _container.style.borderBottomRightRadius = 8f;
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.alignItems    = Align.Center;
            _container.style.height        = 32f;
            _container.style.paddingLeft   = 12f;
            _container.style.paddingRight  = 12f;

            // --- Ripple ---
            _ripple = new RippleElement();
            _container.Add(_ripple);

            // --- Leading icon (Material Symbols font) ---
            _leadingIcon = new M3Label(string.Empty);
            _leadingIcon.AddToClassList("m3-icon");
            _leadingIcon.AddToClassList(LeadingIconClass);
            _leadingIcon.style.fontSize = 18f;
            _leadingIcon.style.display  = DisplayStyle.None;
            _leadingIcon.pickingMode    = PickingMode.Ignore;
            _container.Add(_leadingIcon);

            // --- Label ---
            _label = new M3Label(string.Empty);
            _label.AddToClassList("m3-label");
            _label.AddToClassList(LabelClass);
            _label.pickingMode = PickingMode.Ignore;
            _container.Add(_label);

            // --- Trailing icon (Material Symbols close for input chip) ---
            _trailingIcon = new M3Label(MaterialSymbols.Close);
            _trailingIcon.AddToClassList("m3-icon");
            _trailingIcon.AddToClassList(TrailingIconClass);
            _trailingIcon.style.fontSize = 18f;
            _trailingIcon.style.display  = DisplayStyle.None;
            _trailingIcon.pickingMode    = PickingMode.Position;
            _container.Add(_trailingIcon);

            // --- State layer ---
            InitStateLayer(_container, _ripple);

            // --- Events ---
            _container.RegisterCallback<ClickEvent>(OnContainerClicked);
            _trailingIcon.RegisterCallback<ClickEvent>(OnTrailingIconClicked);

            Add(_container);
            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Visual State                                                        //
        // ------------------------------------------------------------------ //

        private void ApplyVariant()
        {
            // Show/hide trailing X
            _trailingIcon.style.display = _variant == ChipVariant.Input
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Update padding based on whether we have trailing icon
            _container.style.paddingRight = _variant == ChipVariant.Input ? 8f : 12f;

            // Leading icon: show for Assist (if icon set) or selected Filter
            bool showLeading = (_variant == ChipVariant.Assist && _icon != ChipIcon.None)
                            || (_variant == ChipVariant.Filter && _selected);
            _leadingIcon.style.display = showLeading ? DisplayStyle.Flex : DisplayStyle.None;
            _container.style.paddingLeft = showLeading ? 8f : 12f;

            if (showLeading)
                _leadingIcon.text = GetLeadingIconText();

            // Reset selected state when not filter
            if (_variant != ChipVariant.Filter)
                _selected = false;

            ApplySelectedState();
            ApplyColors();
        }

        private void ApplySelectedState()
        {
            _container.RemoveFromClassList(SelectedClass);
            _container.RemoveFromClassList(UnselectedClass);

            if (_variant == ChipVariant.Filter && _selected)
            {
                _container.AddToClassList(SelectedClass);
                _leadingIcon.text = MaterialSymbols.Check;
                _leadingIcon.style.display = DisplayStyle.Flex;
                _container.style.paddingLeft = 8f;
                _container.OutlineThickness = 0f;
                _container.FillColorOverride = _themeSecondaryContainer;
            }
            else
            {
                _container.AddToClassList(UnselectedClass);
                bool hasAssistIcon = _variant == ChipVariant.Assist && _icon != ChipIcon.None;
                _leadingIcon.style.display = hasAssistIcon ? DisplayStyle.Flex : DisplayStyle.None;
                _container.style.paddingLeft = hasAssistIcon ? 8f : 12f;
                _container.OutlineThickness = 1f;
                _container.OutlineColor = _themeOutline;
                _container.FillColorOverride = _themeSurface;
            }
        }

        private void ApplyColors()
        {
            StateLayer.OverlayColor = _themeOnSurface;
            if (!(_variant == ChipVariant.Filter && _selected))
                _container.OutlineColor = _themeOutline;
            _container.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themeOutline            = theme.GetColor(ColorRole.Outline);
            _themeOnSurface          = theme.GetColor(ColorRole.OnSurface);
            _themeSurface            = theme.GetColor(ColorRole.Surface);
            _themeSecondaryContainer = theme.GetColor(ColorRole.SecondaryContainer);

            ApplySelectedState();
            ApplyColors();
        }

        // ------------------------------------------------------------------ //
        //  Icon Text Mapping                                                   //
        // ------------------------------------------------------------------ //

        private string GetLeadingIconText()
        {
            if (_variant == ChipVariant.Filter && _selected)
                return MaterialSymbols.Check;
            return _icon switch
            {
                ChipIcon.Calendar => "\ue935", // calendar_today
                ChipIcon.Search   => MaterialSymbols.Search,
                ChipIcon.Add      => MaterialSymbols.Add,
                ChipIcon.Star     => MaterialSymbols.Star,
                ChipIcon.Person   => MaterialSymbols.Person,
                ChipIcon.Location => "\ue0c8", // location_on
                _                 => string.Empty,
            };
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                      //
        // ------------------------------------------------------------------ //

        private void OnContainerClicked(ClickEvent evt)
        {
            if (base.Disabled) return;

            if (_variant == ChipVariant.Filter)
                Selected = !_selected;

            OnClick?.Invoke();
        }

        private void OnTrailingIconClicked(ClickEvent evt)
        {
            if (base.Disabled) return;
            evt.StopPropagation();
            OnRemove?.Invoke();
        }
    }
}
