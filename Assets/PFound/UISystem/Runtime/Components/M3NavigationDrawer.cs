using System;
using System.Collections.Generic;
using PFound.UISystem.Core;
using PFound.UISystem.Utils;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Navigation Drawer — side navigation panel.
    ///
    /// Modes:
    ///   Standard: always visible (persistent side panel)
    ///   Modal: slides over content with scrim overlay
    ///
    /// Composition:
    ///   VisualElement (_scrim) — dimming overlay for modal mode
    ///   VisualElement (_drawer) — drawer surface panel
    ///     VisualElement (_header) — optional app name / account header
    ///     VisualElement (_navList) — list of NavigationDrawerItem children
    ///
    /// M3 spec:
    ///   Width: 360dp (modal), fills available space (standard)
    ///   Background: --m3-surface-container-low
    ///   Active item: --m3-secondary-container background
    ///
    /// USS: navigation-drawer.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3NavigationDrawer : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass     = "m3-nav-drawer";
        private const string ModalClass    = "m3-nav-drawer--modal";
        private const string StandardClass = "m3-nav-drawer--standard";
        private const string OpenClass     = "m3-nav-drawer--open";
        private const string ScrimClass    = "m3-nav-drawer__scrim";
        private const string DrawerClass   = "m3-nav-drawer__drawer";
        private const string HeaderClass   = "m3-nav-drawer__header";
        private const string NavListClass  = "m3-nav-drawer__nav-list";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _scrim;
        private readonly VisualElement _drawer;
        private readonly VisualElement _header;
        private readonly VisualElement _navList;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private DrawerMode _mode = DrawerMode.Modal;
        private bool       _open;
        private int        _activeIndex = -1;
        private readonly List<NavigationDrawerItem> _items = new();

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum DrawerMode { Modal, Standard }

        /// <summary>Fired when a navigation item is selected. Arg is the item index.</summary>
        public event Action<int> OnItemSelected;

        /// <summary>Modal (overlay) or Standard (persistent) mode.</summary>
        [UxmlAttribute("mode")]
        public DrawerMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                ApplyMode();
            }
        }

        /// <summary>Whether the drawer is currently open/visible.</summary>
        public bool IsOpen
        {
            get => _open;
            set
            {
                if (_open == value) return;
                _open = value;
                EnableInClassList(OpenClass, _open);
                _scrim.style.display = (_open && _mode == DrawerMode.Modal)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3NavigationDrawer()
        {
            AddToClassList(BaseClass);
            style.position = Position.Absolute;

            // Scrim overlay (modal mode)
            _scrim = new VisualElement();
            _scrim.AddToClassList(ScrimClass);
            _scrim.style.display = DisplayStyle.None;
            _scrim.RegisterCallback<ClickEvent>(_ => IsOpen = false);

            // Drawer panel
            _drawer = new VisualElement();
            _drawer.AddToClassList(DrawerClass);

            _header = new VisualElement();
            _header.AddToClassList(HeaderClass);
            _header.style.display = DisplayStyle.None;

            _navList = new VisualElement();
            _navList.AddToClassList(NavListClass);

            _drawer.Add(_header);
            _drawer.Add(_navList);

            Add(_scrim);
            Add(_drawer);

            ApplyMode();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Adds a navigation item to the drawer.</summary>
        public NavigationDrawerItem AddItem(string label, string iconCodepoint = null)
        {
            var item = new NavigationDrawerItem(label, iconCodepoint);
            int index = _items.Count;
            item.OnItemClicked += () => SelectItem(index);
            _items.Add(item);
            _navList.Add(item);
            return item;
        }

        /// <summary>Selects the nav item at the given index and fires OnItemSelected.</summary>
        public void SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            if (_activeIndex >= 0 && _activeIndex < _items.Count)
                _items[_activeIndex].Active = false;

            _activeIndex = index;
            _items[_activeIndex].Active = true;

            OnItemSelected?.Invoke(_activeIndex);

            if (_mode == DrawerMode.Modal)
                IsOpen = false;
        }

        /// <summary>Opens the drawer.</summary>
        public void Open() => IsOpen = true;

        /// <summary>Closes the drawer.</summary>
        public void Close() => IsOpen = false;

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void ApplyMode()
        {
            if (_mode == DrawerMode.Modal)
            {
                AddToClassList(ModalClass);
                RemoveFromClassList(StandardClass);
            }
            else
            {
                AddToClassList(StandardClass);
                RemoveFromClassList(ModalClass);
                _scrim.style.display = DisplayStyle.None;
            }
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }

        // ================================================================== //
        //  Inner class: NavigationDrawerItem                                  //
        // ================================================================== //

        /// <summary>Single navigation item in an M3NavigationDrawer.</summary>
        public class NavigationDrawerItem : VisualElement
        {
            private const string ItemBase    = "m3-nav-drawer-item";
            private const string ItemActive  = "m3-nav-drawer-item--active";
            private const string ItemIcon    = "m3-nav-drawer-item__icon";
            private const string ItemLabel   = "m3-nav-drawer-item__label";

            private readonly Label _icon;
            private readonly Label _label;
            private bool           _active;

            internal event Action OnItemClicked;

            public bool Active
            {
                get => _active;
                internal set
                {
                    _active = value;
                    EnableInClassList(ItemActive, _active);
                }
            }

            public NavigationDrawerItem(string label, string iconCodepoint = null)
            {
                AddToClassList(ItemBase);
                pickingMode = PickingMode.Position;
                focusable   = true;

                _icon = new M3Label(iconCodepoint ?? string.Empty);
                _icon.AddToClassList("m3-icon");
                _icon.AddToClassList(ItemIcon);
                _icon.style.display = string.IsNullOrEmpty(iconCodepoint)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

                _label = new M3Label(label);
                _label.AddToClassList(ItemLabel);
                _label.AddToClassList("m3-label-large");

                Add(_icon);
                Add(_label);

                RegisterCallback<ClickEvent>(_ => OnItemClicked?.Invoke());
            }
        }
    }
}
