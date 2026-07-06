using System;
using System.Collections.Generic;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Menu — a popup list of actions shown at an anchor position.
    ///
    /// Composition:
    ///   M3Surface (this) — elevated menu surface (elevation 2)
    ///   [M3MenuItem children] — menu items added via AddItem
    ///
    /// Usage:
    ///   var menu = new M3Menu();
    ///   menu.AddItem("Copy", MaterialSymbols.ContentCopy, () => DoCopy());
    ///   menu.AddItem("Delete", MaterialSymbols.Delete, () => DoDelete());
    ///   menu.Show(anchorElement);
    ///   // dismiss on outside click handled automatically
    ///
    /// M3 spec:
    ///   Min width: 112dp, Max width: 280dp
    ///   Background: --m3-surface-container
    ///   Elevation: 2 (shadow)
    ///   Corner radius: 4dp
    ///
    /// USS: menu.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    public class M3Menu : M3Surface
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass = "m3-menu";

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private readonly List<M3MenuItem>  _items    = new();
        private readonly List<VisualElement> _dividers = new();
        private VisualElement              _scrimOverlay;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public M3Menu()
        {
            AddToClassList(BaseClass);
            CornerRadius  = 4f;
            style.display = DisplayStyle.None;
            style.position = Position.Absolute;
            // Inline layout — the .m3-menu USS rule (min/max width, padding,
            // flex-direction column, border-radius) doesn't reliably reach
            // a menu popped to panel.visualTree (style-scope quirk, same
            // issue that hid .m3-icon font and .m3-menu-item row layout).
            // Pin the structural layout in C# so the menu always sizes
            // correctly regardless of where it lives.
            style.minWidth        = 112f;
            style.maxWidth        = 280f;
            style.paddingTop      = 8f;
            style.paddingBottom   = 8f;
            // No horizontal padding — items take the full M3Menu layout width.
            // M3Surface's SDF silhouette already clips child rendering at the
            // silhouette edges, so the item's hover bg and ripple are both
            // visually bounded by the silhouette even though the item's layout
            // box extends slightly past it for ShadowPadding.
            style.flexDirection   = FlexDirection.Column;
            style.borderTopLeftRadius     = 4f;
            style.borderTopRightRadius    = 4f;
            style.borderBottomLeftRadius  = 4f;
            style.borderBottomRightRadius = 4f;

            // M3 elevation 2 — shadow is what visually lifts the menu off the page.
            ShadowBlur    = 8f;
            ShadowOffsetY = 2f;
            ShadowColor   = new Color(0f, 0f, 0f, 0.30f);
            ShadowPadding = 6f;

            // Theme routing — without this the SDF surface emits palette slot 0 (white)
            // and the menu blends into the page surface. M3Menu does not inherit
            // M3ComponentBase (it extends M3Surface directly to be its own scaffold),
            // so the theme subscription is wired inline here.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged += OnMenuThemeChanged;
                RefreshMenuFill();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged -= OnMenuThemeChanged;
            });
        }

        private void OnMenuThemeChanged(ThemeData _) => RefreshMenuFill();

        private void RefreshMenuFill()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;
            FillColorOverride = theme.GetColor(ColorRole.SurfaceContainer);
            RefreshDividerColors(theme);
        }

        private void RefreshDividerColors(ThemeData theme)
        {
            var outline = theme.GetColor(ColorRole.Outline);
            for (int i = 0; i < _dividers.Count; i++)
                _dividers[i].style.backgroundColor = new StyleColor(outline);
        }

        /// <summary>
        /// Adds a menu item. Returns the created item so callers can further customize it.
        /// </summary>
        public M3MenuItem AddItem(string label, string leadingIconCodepoint = null, Action onClick = null)
        {
            var item = new M3MenuItem
            {
                LabelText   = label,
                LeadingIcon = leadingIconCodepoint,
            };
            if (onClick != null)
                item.OnClick += onClick;

            item.OnClick += Hide; // auto-dismiss on item click
            _items.Add(item);
            Add(item);
            return item;
        }

        /// <summary>Adds a separator line between items, coloured with the
        /// theme's Outline role (stronger contrast than outline-variant —
        /// 1dp height matches M3 spec, the colour change is what makes the
        /// divider visually distinct).</summary>
        public void AddDivider()
        {
            var div = new VisualElement();
            div.AddToClassList("m3-menu__divider");
            div.style.height       = 1f;
            div.style.marginTop    = 4f;
            div.style.marginBottom = 4f;

            // Initial colour from active theme if available, else neutral fallback.
            // RefreshDividerColors will refresh this on every theme change.
            var theme = ThemeManager.ActiveTheme;
            var initial = theme != null
                ? theme.GetColor(ColorRole.Outline)
                : new Color(0.49f, 0.46f, 0.50f, 1f); // M3 baseline light: outline
            div.style.backgroundColor = new StyleColor(initial);

            _dividers.Add(div);
            Add(div);
        }

        /// <summary>
        /// Shows the menu positioned below (or above) the anchor element.
        /// Adds a transparent scrim to the root panel to capture outside clicks.
        /// </summary>
        public void Show(VisualElement anchor)
        {
            var root = anchor.panel?.visualTree;
            if (root == null) return;

            // Scrim to capture outside clicks
            _scrimOverlay?.RemoveFromHierarchy();
            _scrimOverlay = new VisualElement();
            _scrimOverlay.style.position = Position.Absolute;
            _scrimOverlay.style.left   = 0;
            _scrimOverlay.style.top    = 0;
            _scrimOverlay.style.right  = 0;
            _scrimOverlay.style.bottom = 0;
            _scrimOverlay.pickingMode  = PickingMode.Position;
            _scrimOverlay.RegisterCallback<ClickEvent>(_ => Hide());

            root.Add(_scrimOverlay);
            root.Add(this);

            // Position relative to anchor
            var anchorBounds = anchor.worldBound;
            style.left = anchorBounds.x;
            style.top  = anchorBounds.yMax + 4f; // 4dp gap below anchor

            style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides and removes the menu and its scrim.</summary>
        public void Hide()
        {
            style.display = DisplayStyle.None;
            _scrimOverlay?.RemoveFromHierarchy();
            _scrimOverlay = null;
            RemoveFromHierarchy();
        }

        /// <summary>Removes all menu items.</summary>
        public void ClearItems()
        {
            _items.Clear();
            _dividers.Clear();
            Clear();
        }
    }
}
