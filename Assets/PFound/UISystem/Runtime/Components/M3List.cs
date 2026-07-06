using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style List container — groups M3ListItem children.
    ///
    /// A thin wrapper that applies list styling (no padding/margin between items,
    /// optional border, full-width column layout).
    ///
    /// USS: list.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3List : VisualElement
    {
        private const string BaseClass      = "m3-list";
        private const string DividerClass   = "m3-list--dividers";
        private const string SegmentedClass = "m3-list--segmented";
        private const string LastItemClass  = "m3-list-item--last";

        public enum ListStyle { Standard, Dividers, Segmented }

        private ListStyle _listStyle = ListStyle.Standard;

        /// <summary>Visual style: Standard (no dividers), Dividers (1dp lines), Segmented (rounded boxes with gap).</summary>
        [UxmlAttribute("list-style")]
        public ListStyle Style
        {
            get => _listStyle;
            set
            {
                _listStyle = value;
                EnableInClassList(DividerClass, value == ListStyle.Dividers);
                EnableInClassList(SegmentedClass, value == ListStyle.Segmented);
            }
        }

        /// <summary>When true, shows 1dp dividers between list items.</summary>
        [UxmlAttribute("show-dividers")]
        public bool ShowDividers
        {
            get => _listStyle == ListStyle.Dividers;
            set => Style = value ? ListStyle.Dividers : ListStyle.Standard;
        }

        public M3List()
        {
            AddToClassList(BaseClass);

            // Critical layout — inline to guarantee application
            style.flexDirection = FlexDirection.Column;
            style.paddingTop = 8;
            style.paddingBottom = 8;

            // Track child changes to maintain --last class
            RegisterCallback<AttachToPanelEvent>(_ => RefreshLastItem());
        }

        /// <summary>Adds a child and updates the last-item class for divider support.</summary>
        public new void Add(VisualElement child)
        {
            // Remove --last from previous last child
            if (childCount > 0)
            {
                var prev = ElementAt(childCount - 1);
                prev.RemoveFromClassList(LastItemClass);
            }
            child.AddToClassList(LastItemClass);
            base.Add(child);
        }

        private void RefreshLastItem()
        {
            for (int i = 0; i < childCount; i++)
            {
                var child = ElementAt(i);
                child.EnableInClassList(LastItemClass, i == childCount - 1);
            }
        }
    }
}
