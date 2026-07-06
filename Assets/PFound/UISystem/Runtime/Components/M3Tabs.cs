using System;
using System.Collections.Generic;
using PFound.UISystem.Core;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Tabs container — manages a list of M3TabItem children.
    ///
    /// Composition:
    ///   VisualElement (this) — full-width tab bar
    ///   [M3TabItem children] — individual tabs (added via AddTab)
    ///
    /// Variants:
    ///   Primary: underline active indicator, on-surface colors
    ///   Secondary: filled pill indicator, secondary-container background
    ///
    /// M3 spec:
    ///   Height: 48dp (primary), 48dp (secondary)
    ///   Active indicator: 3dp underline (primary), filled pill (secondary)
    ///   Colors: active=--m3-primary, inactive=--m3-on-surface-variant
    ///
    /// USS: tabs.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3Tabs : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass      = "m3-tabs";
        private const string PrimaryClass   = "m3-tabs--primary";
        private const string SecondaryClass = "m3-tabs--secondary";

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private TabsVariant      _variant    = TabsVariant.Primary;
        private int              _activeIndex;
        private readonly List<M3TabItem> _tabs = new();

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum TabsVariant { Primary, Secondary }

        /// <summary>Fired when the active tab changes. Arg is the new active index.</summary>
        public event Action<int> OnTabChanged;

        /// <summary>Primary (underline) or Secondary (filled pill) variant.</summary>
        [UxmlAttribute("variant")]
        public TabsVariant Variant
        {
            get => _variant;
            set
            {
                if (_variant == value) return;
                _variant = value;
                ApplyVariant();
            }
        }

        /// <summary>Index of the currently active tab.</summary>
        public int ActiveIndex
        {
            get => _activeIndex;
            set => SelectTab(value);
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Tabs()
        {
            AddToClassList(BaseClass);

            // Critical layout — inline to guarantee application
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.height = 48;

            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Adds a tab with the given label. Returns the created M3TabItem.</summary>
        public M3TabItem AddTab(string label, string iconCodepoint = null)
        {
            var tab = new M3TabItem
            {
                LabelText = label,
                Icon      = iconCodepoint,
            };
            tab.OnTabClicked += OnTabItemClicked;

            int index = _tabs.Count;
            _tabs.Add(tab);
            Add(tab);

            // M3 spec: icon+label primary tabs = 64dp
            if (!string.IsNullOrEmpty(iconCodepoint))
                style.height = 64;

            // Activate first tab by default
            if (index == 0)
                tab.Active = true;
            else
                tab.Active = index == _activeIndex;

            return tab;
        }

        /// <summary>Removes all tabs.</summary>
        public void ClearTabs()
        {
            foreach (var t in _tabs)
                t.OnTabClicked -= OnTabItemClicked;
            _tabs.Clear();
            Clear();
            _activeIndex = 0;
        }

        /// <summary>Selects the tab at the given index. Indicator animated via USS transition.</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            if (_activeIndex == index && _tabs[index].Active) return;

            _tabs[_activeIndex].Active = false;
            _activeIndex               = index;
            _tabs[_activeIndex].Active = true;
            // USS transition on .m3-tab-item__indicator opacity handles the animation

            OnTabChanged?.Invoke(_activeIndex);
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void OnTabItemClicked(M3TabItem tab)
        {
            int idx = _tabs.IndexOf(tab);
            if (idx >= 0) SelectTab(idx);
        }

        private void ApplyVariant()
        {
            if (_variant == TabsVariant.Primary)
            {
                AddToClassList(PrimaryClass);
                RemoveFromClassList(SecondaryClass);
            }
            else
            {
                AddToClassList(SecondaryClass);
                RemoveFromClassList(PrimaryClass);
            }
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }
    }
}
