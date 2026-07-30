using System;
using System.Collections.Generic;
using PFound.UISystem.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Navigation Bar component.
    ///
    /// Structure:
    ///   VisualElement (this) — 80dp height bar, row layout
    ///   M3NavigationItem children — managed via Items list or UXML children
    ///
    /// M3 Spec:
    ///   Height: 80dp
    ///   3–5 items, evenly distributed
    ///   Colors: --m3-surface bar bg
    ///   Active indicator: 64×32dp pill, secondary-container color
    ///
    /// USS: navigation-bar.uss
    ///
    /// Usage (UXML):
    ///   &lt;components:M3NavigationBar&gt;
    ///     &lt;components:M3NavigationItem icon="&#xe88a;" label="Home" active="true" /&gt;
    ///     &lt;components:M3NavigationItem icon="&#xe8b6;" label="Search" /&gt;
    ///   &lt;/components:M3NavigationBar&gt;
    /// </summary>
    [UxmlElement]
    public partial class M3NavigationBar : M3ComponentBase
    {
        private const string BaseClass = "m3-nav-bar";

        private readonly List<M3NavigationItem> _items = new();
        private int _selectedIndex = -1;

        /// <summary>Fired when a navigation item is selected.</summary>
        public event Action<int> OnItemSelected;

        /// <summary>Currently selected index (-1 if none).</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SelectAt(value);
        }

        public M3NavigationBar()
        {
            AddToClassList(BaseClass);
            pickingMode          = PickingMode.Position;
            style.flexDirection  = FlexDirection.Row;
            style.height         = 80f;
            style.width          = Length.Percent(100);
            style.alignItems     = Align.Stretch;

            // Watch for children being added (UXML workflow)
            RegisterCallback<AttachToPanelEvent>(_ => RefreshItems());
        }

        private void RefreshItems()
        {
            _items.Clear();

            foreach (var child in Children())
            {
                if (child is M3NavigationItem item)
                    RegisterItem(item);
            }

            // Ensure at least one is selected
            if (_selectedIndex < 0 && _items.Count > 0)
                SelectAt(0);
        }

        /// <summary>Add a navigation item programmatically.</summary>
        public void AddItem(M3NavigationItem item)
        {
            RegisterItem(item);
            Add(item);

            if (_selectedIndex < 0)
                SelectAt(0);
        }

        private void RegisterItem(M3NavigationItem item)
        {
            int idx = _items.Count;
            _items.Add(item);
            item.Clicked += _ => SelectAt(idx);

            if (item.Active)
                _selectedIndex = idx;
        }

        private void SelectAt(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                _items[_selectedIndex].Active = false;

            _selectedIndex = index;
            _items[_selectedIndex].Active = true;

            OnItemSelected?.Invoke(_selectedIndex);
        }
    }
}
