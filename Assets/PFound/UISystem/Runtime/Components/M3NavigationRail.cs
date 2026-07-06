using System;
using System.Collections.Generic;
using PFound.UISystem.Core;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Navigation Rail — vertical side navigation for medium screens.
    ///
    /// Composition:
    ///   VisualElement (this) — 72–80dp wide column
    ///   VisualElement (_fabSlot) — optional top FAB slot
    ///   VisualElement (_itemsContainer) — stack of NavigationRailItem children
    ///
    /// M3 spec:
    ///   Width: 72–80dp
    ///   Active item: pill indicator (56dp×32dp) behind icon
    ///   Background: --m3-surface-container-low
    ///
    /// USS: navigation-rail.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3NavigationRail : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass       = "m3-nav-rail";
        private const string FabSlotClass    = "m3-nav-rail__fab-slot";
        private const string ItemsClass      = "m3-nav-rail__items";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _fabSlot;
        private readonly VisualElement _itemsContainer;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private int _activeIndex = -1;
        private readonly List<NavigationRailItem> _items = new();

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when a navigation item is selected. Arg is the item index.</summary>
        public event Action<int> OnItemSelected;

        /// <summary>FAB slot — add an M3FAB here to show it at the top of the rail.</summary>
        public VisualElement FabSlot => _fabSlot;

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3NavigationRail()
        {
            AddToClassList(BaseClass);

            _fabSlot = new VisualElement();
            _fabSlot.AddToClassList(FabSlotClass);

            _itemsContainer = new VisualElement();
            _itemsContainer.AddToClassList(ItemsClass);

            Add(_fabSlot);
            Add(_itemsContainer);
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Adds a navigation item. Returns the created item.</summary>
        public NavigationRailItem AddItem(string label, string iconCodepoint)
        {
            var item = new NavigationRailItem(label, iconCodepoint);
            int index = _items.Count;
            item.OnItemClicked += () => SelectItem(index);
            _items.Add(item);
            _itemsContainer.Add(item);

            if (index == 0) SelectItem(0);
            return item;
        }

        /// <summary>Selects the item at the given index.</summary>
        public void SelectItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            if (_activeIndex >= 0 && _activeIndex < _items.Count)
                _items[_activeIndex].Active = false;
            _activeIndex = index;
            _items[_activeIndex].Active = true;
            OnItemSelected?.Invoke(_activeIndex);
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }

        // ================================================================== //
        //  Inner class: NavigationRailItem                                    //
        // ================================================================== //

        /// <summary>Single item in an M3NavigationRail.</summary>
        public class NavigationRailItem : VisualElement
        {
            private const string ItemBase      = "m3-nav-rail-item";
            private const string ItemActive    = "m3-nav-rail-item--active";
            private const string IndicatorClass = "m3-nav-rail-item__indicator";
            private const string ItemIcon      = "m3-nav-rail-item__icon";
            private const string ItemLabel     = "m3-nav-rail-item__label";

            private readonly VisualElement _indicator;
            private readonly Label         _icon;
            private readonly Label         _label;
            private bool                   _active;

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

            public NavigationRailItem(string label, string iconCodepoint)
            {
                AddToClassList(ItemBase);
                pickingMode = PickingMode.Position;
                focusable   = true;

                _indicator = new VisualElement();
                _indicator.AddToClassList(IndicatorClass);
                _indicator.pickingMode = PickingMode.Ignore;

                _icon = new M3Label(iconCodepoint ?? string.Empty);
                _icon.AddToClassList("m3-icon");
                _icon.AddToClassList(ItemIcon);

                _label = new M3Label(label);
                _label.AddToClassList(ItemLabel);
                _label.AddToClassList("m3-label-medium");

                Add(_indicator);
                Add(_icon);
                Add(_label);

                RegisterCallback<ClickEvent>(_ => OnItemClicked?.Invoke());
            }
        }
    }
}
