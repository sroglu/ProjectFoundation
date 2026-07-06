using System;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Menu Item — a single entry within an M3Menu.
    ///
    /// Composition:
    ///   VisualElement (this) — menu item row (bg + ripple host)
    ///   RippleElement (_ripple) — click feedback, clipped to item bounds
    ///   Label (_leadingIcon) — optional leading icon
    ///   Label (_label) — item text
    ///   Label (_trailingIcon) — optional trailing icon / shortcut hint
    ///
    /// Width comes from M3Menu, which pads its content area to silhouette
    /// width — so the item itself maps 1:1 to the visible menu interior,
    /// and bg / ripple are clipped at the silhouette edges via overflow:hidden.
    ///
    /// USS: menu.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3MenuItem : VisualElement
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        internal const string BaseClass         = "m3-menu-item";
        internal const string DisabledClass     = "m3-menu-item--disabled";
        internal const string LeadingIconClass  = "m3-menu-item__leading";
        internal const string LabelClass        = "m3-menu-item__label";
        internal const string TrailingIconClass = "m3-menu-item__trailing";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly Label         _leadingIcon;
        private readonly Label         _label;
        private readonly Label         _trailingIcon;
        private readonly VisualElement _rippleHost;
        private readonly RippleElement _ripple;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string _labelText = string.Empty;
        private bool   _disabled;

        // Resolved per active theme — overlay tints use OnSurface with M3 state
        // opacities. Fallbacks are baseline-light OnSurface in case the theme is
        // not yet active when the item is interacted with.
        private Color _hoverOverlay = new Color(0.11f, 0.106f, 0.122f, 0.08f);
        private Color _pressOverlay = new Color(0.11f, 0.106f, 0.122f, 0.12f);

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the menu item is clicked (not fired when disabled).</summary>
        public event Action OnClick;

        /// <summary>Menu item display label.</summary>
        [UxmlAttribute("label")]
        public string LabelText
        {
            get => _labelText;
            set
            {
                _labelText  = value ?? string.Empty;
                _label.text = _labelText;
            }
        }

        /// <summary>Optional leading icon (MaterialSymbols codepoint).</summary>
        [UxmlAttribute("leading-icon")]
        public string LeadingIcon
        {
            get => _leadingIcon.text;
            set
            {
                _leadingIcon.text         = value ?? string.Empty;
                _leadingIcon.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Optional trailing icon or shortcut label.</summary>
        [UxmlAttribute("trailing-icon")]
        public string TrailingIcon
        {
            get => _trailingIcon.text;
            set
            {
                _trailingIcon.text         = value ?? string.Empty;
                _trailingIcon.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Whether this item is non-interactive.</summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => _disabled;
            set
            {
                _disabled = value;
                EnableInClassList(DisabledClass, _disabled);
                pickingMode = _disabled ? PickingMode.Ignore : PickingMode.Position;
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3MenuItem()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;
            focusable   = true;

            // Inline layout — the m3-menu-item USS rule doesn't reliably reach
            // items inside a popped menu, same style-scope quirk as the icon
            // font and the menu row layout. Pin the row structure in C#.
            style.flexDirection = FlexDirection.Row;
            style.alignItems    = Align.Center;
            style.height        = 48f;
            style.paddingLeft   = 12f;
            style.paddingRight  = 12f;

            // Item bg renders directly on the item; the menu's M3Surface clips
            // the visible background to the silhouette, so the bg state matches
            // the menu interior without any tricks here.
            //
            // Ripple is a separate concern. RippleElement's painter2D draws a
            // growing circle that, when added directly to the item, can spill
            // past the menu silhouette on the side where the circle reaches the
            // item-layout edge before the silhouette edge (item layout is
            // ShadowPadding-inflated past the silhouette). Host the ripple in a
            // dedicated child that is absolutely positioned to match the
            // silhouette (= 6dp inset from item layout on each horizontal side)
            // and has overflow:hidden + border-radius so painter2D drawing is
            // clipped at the silhouette edges.
            _rippleHost = new VisualElement();
            _rippleHost.style.position    = Position.Absolute;
            _rippleHost.style.left        = 6f;
            _rippleHost.style.right       = 6f;
            _rippleHost.style.top         = 0f;
            _rippleHost.style.bottom      = 0f;
            _rippleHost.style.overflow    = Overflow.Hidden;
            _rippleHost.style.borderTopLeftRadius     = 4f;
            _rippleHost.style.borderTopRightRadius    = 4f;
            _rippleHost.style.borderBottomLeftRadius  = 4f;
            _rippleHost.style.borderBottomRightRadius = 4f;
            _rippleHost.pickingMode = PickingMode.Ignore;
            Add(_rippleHost);

            _ripple = new RippleElement();
            _ripple.RippleColor = new Color(0.11f, 0.106f, 0.122f, 1f); // on-surface
            _ripple.PeakOpacity = 0.20f;
            _rippleHost.Add(_ripple);

            _leadingIcon = new M3Label();
            _leadingIcon.AddToClassList("m3-icon");
            _leadingIcon.AddToClassList(LeadingIconClass);
            _leadingIcon.style.display      = DisplayStyle.None;
            _leadingIcon.style.fontSize     = 24f;
            _leadingIcon.style.width        = 24f;
            _leadingIcon.style.height       = 24f;
            _leadingIcon.style.marginRight  = 12f;
            _leadingIcon.style.flexShrink   = 0f;
            _leadingIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            M3Label.ApplyMaterialSymbolsFont(_leadingIcon);

            _label = new M3Label(_labelText);
            _label.AddToClassList(LabelClass);
            _label.AddToClassList("m3-body-large");
            _label.style.flexGrow        = 1f;
            _label.style.unityTextAlign  = TextAnchor.MiddleLeft;

            _trailingIcon = new M3Label();
            _trailingIcon.AddToClassList("m3-icon");
            _trailingIcon.AddToClassList(TrailingIconClass);
            _trailingIcon.style.display      = DisplayStyle.None;
            _trailingIcon.style.fontSize     = 20f;
            _trailingIcon.style.width        = 24f;
            _trailingIcon.style.height       = 24f;
            _trailingIcon.style.marginLeft   = 8f;
            _trailingIcon.style.flexShrink   = 0f;
            _trailingIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            M3Label.ApplyMaterialSymbolsFont(_trailingIcon);

            Add(_leadingIcon);
            Add(_label);
            Add(_trailingIcon);

            RegisterCallback<ClickEvent>(_ =>
            {
                if (!_disabled) OnClick?.Invoke();
            });

            // Hover / press feedback. USS :hover and :active rules don't
            // reliably reach a menu popped to panel.visualTree, so drive the
            // overlay directly from pointer callbacks.
            RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!_disabled) style.backgroundColor = _hoverOverlay;
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                style.backgroundColor = StyleKeyword.Initial;
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_disabled) return;
                style.backgroundColor = _pressOverlay;
                // evt.localPosition is in item-local coords (item layout origin).
                // _rippleHost sits at left:6 inside the item layout, so the
                // equivalent point in host-local coords is (x - 6, y).
                _ripple.StartRipple(new Vector2(evt.localPosition.x - 6f, evt.localPosition.y));
            });
            RegisterCallback<PointerUpEvent>(_ =>
            {
                if (!_disabled) style.backgroundColor = _hoverOverlay;
            });

            // Theme routing — USS rules don't reliably reach a menu popped to
            // panel.visualTree, so foreground colours (icon, label, ripple,
            // state-layer overlays) are wired through ThemeManager here.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged += OnItemThemeChanged;
                RefreshItemTheme();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged -= OnItemThemeChanged;
            });
        }

        // ------------------------------------------------------------------ //
        //  Theme routing                                                       //
        // ------------------------------------------------------------------ //

        private void OnItemThemeChanged(ThemeData _) => RefreshItemTheme();

        private void RefreshItemTheme()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            var onSurface        = theme.GetColor(ColorRole.OnSurface);
            var onSurfaceVariant = theme.GetColor(ColorRole.OnSurfaceVariant);

            _leadingIcon.style.color  = onSurfaceVariant;
            _trailingIcon.style.color = onSurfaceVariant;
            _label.style.color        = onSurface;

            // M3 state-layer opacities (on-surface @ 0.08 hover / 0.12 press).
            _hoverOverlay = new Color(onSurface.r, onSurface.g, onSurface.b, 0.08f);
            _pressOverlay = new Color(onSurface.r, onSurface.g, onSurface.b, 0.12f);

            // Ripple tint = on-surface (PeakOpacity controls visible alpha).
            _ripple.RippleColor = new Color(onSurface.r, onSurface.g, onSurface.b, 1f);
        }
    }
}
