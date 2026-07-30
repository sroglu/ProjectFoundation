using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// Single navigation item for M3NavigationBar.
    ///
    /// Structure:
    ///   VisualElement (this) — flex column, equal flex
    ///   VisualElement (_indicatorWrap) — 64×32dp pill container
    ///   M3Surface (_indicator)   — 64×32dp pill (secondary-container when active)
    ///   Label (_iconLabel)             — Material Symbols icon
    ///   Label (_labelEl)               — nav item text label
    ///
    /// M3 Spec:
    ///   Indicator: 64×32dp, 16dp radius (pill)
    ///   Icon: 24px
    ///   Active: secondary-container indicator, on-secondary-container icon, on-surface label
    ///   Inactive: transparent indicator, on-surface-variant icon, on-surface-variant label
    /// </summary>
    [UxmlElement]
    public partial class M3NavigationItem : M3ComponentBase
    {
        private const string BaseClass         = "m3-nav-item";
        private const string IndicatorWrapClass = "m3-nav-item__indicator-wrap";
        private const string IndicatorClass    = "m3-nav-item__indicator";
        private const string IconClass         = "m3-nav-item__icon";
        private const string LabelClass        = "m3-nav-item__label";
        private const string ActiveClass       = "m3-nav-item--active";

        private const float IndicatorWidth  = 64f;
        private const float IndicatorHeight = 32f;
        private const float IndicatorRadius = 16f;

        // Resolved theme colors
        private Color _themeSecondaryContainer;

        private readonly VisualElement  _indicatorWrap;
        private readonly M3Surface _indicator;
        private readonly Label          _iconLabel;
        private readonly Label          _labelEl;
        private readonly RippleElement  _ripple;

        private string _label   = string.Empty;
        private string _icon    = string.Empty;
        private bool   _active  = false;

        internal event Action<M3NavigationItem> Clicked;

        [UxmlAttribute("label")]
        public string Label
        {
            get => _label;
            set { _label = value; _labelEl.text = value ?? string.Empty; }
        }

        [UxmlAttribute("icon")]
        public string Icon
        {
            get => _icon;
            set { _icon = value; _iconLabel.text = value ?? string.Empty; }
        }

        [UxmlAttribute("active")]
        public bool Active
        {
            get => _active;
            set { _active = value; ApplyActiveState(); }
        }

        public M3NavigationItem()
        {
            AddToClassList(BaseClass);
            pickingMode  = PickingMode.Position;
            style.flexGrow      = 1;
            style.alignItems    = Align.Center;
            style.flexDirection = FlexDirection.Column;
            style.paddingTop    = 12f;
            style.paddingBottom = 16f;

            // --- Indicator wrap ---
            _indicatorWrap = new VisualElement();
            _indicatorWrap.AddToClassList(IndicatorWrapClass);
            _indicatorWrap.style.width          = IndicatorWidth;
            _indicatorWrap.style.height         = IndicatorHeight;
            _indicatorWrap.style.alignItems     = Align.Center;
            _indicatorWrap.style.justifyContent = Justify.Center;
            _indicatorWrap.style.position       = Position.Relative;
            _indicatorWrap.pickingMode          = PickingMode.Ignore;

            // --- Indicator pill ---
            _indicator = new M3Surface { CornerRadius = IndicatorRadius, pickingMode = PickingMode.Ignore };
            _indicator.AddToClassList(IndicatorClass);
            _indicator.style.width  = IndicatorWidth;
            _indicator.style.height = IndicatorHeight;
            _indicator.style.borderTopLeftRadius     = IndicatorRadius;
            _indicator.style.borderTopRightRadius    = IndicatorRadius;
            _indicator.style.borderBottomLeftRadius  = IndicatorRadius;
            _indicator.style.borderBottomRightRadius = IndicatorRadius;
            _indicator.style.position = Position.Absolute;

            // --- Ripple on indicator ---
            _ripple = new RippleElement();
            _indicator.Add(_ripple);

            // --- Icon ---
            _iconLabel = new M3Label(string.Empty);
            _iconLabel.AddToClassList("m3-icon");
            _iconLabel.AddToClassList(IconClass);
            _iconLabel.style.fontSize  = 24f;
            _iconLabel.style.position  = Position.Absolute;
            _iconLabel.pickingMode     = PickingMode.Ignore;

            _indicatorWrap.Add(_indicator);
            _indicatorWrap.Add(_iconLabel);

            // --- Text label ---
            _labelEl = new M3Label(string.Empty);
            _labelEl.AddToClassList("m3-label");
            _labelEl.AddToClassList(LabelClass);
            _labelEl.style.marginTop = 4f;
            _labelEl.pickingMode     = PickingMode.Ignore;

            Add(_indicatorWrap);
            Add(_labelEl);

            RegisterCallback<ClickEvent>(OnItemClicked);
            ApplyActiveState();
        }

        private void ApplyActiveState()
        {
            EnableInClassList(ActiveClass, _active);

            if (_active)
            {
                _indicator.FillColorOverride = _themeSecondaryContainer;
                _labelEl.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                _indicator.FillColorOverride = Color.clear;
                _labelEl.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themeSecondaryContainer = theme.GetColor(ColorRole.SecondaryContainer);

            ApplyActiveState();
        }

        private void OnItemClicked(ClickEvent evt)
        {
            Clicked?.Invoke(this);
        }
    }
}
