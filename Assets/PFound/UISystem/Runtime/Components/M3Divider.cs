using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Divider — horizontal or vertical visual separator.
    ///
    /// A thin 1dp line using --m3-outline-variant color.
    ///
    /// Usage (C#):
    ///   container.Add(new M3Divider());
    ///   container.Add(new M3Divider { Orientation = DividerOrientation.Vertical });
    ///
    /// Usage (UXML):
    ///   &lt;components:M3Divider orientation="Vertical" /&gt;
    ///
    /// USS: divider.uss. Color via var(--m3-outline-variant).
    /// </summary>
    [UxmlElement]
    public partial class M3Divider : VisualElement
    {
        private const string BaseClass       = "m3-divider";
        private const string HorizontalClass = "m3-divider--horizontal";
        private const string VerticalClass   = "m3-divider--vertical";

        private DividerOrientation _orientation = DividerOrientation.Horizontal;

        public enum DividerOrientation { Horizontal, Vertical }

        /// <summary>Horizontal (default) or Vertical orientation.</summary>
        [UxmlAttribute("orientation")]
        public DividerOrientation Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                ApplyOrientation();
            }
        }

        public M3Divider()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Ignore;
            ApplyOrientation();
        }

        private void ApplyOrientation()
        {
            if (_orientation == DividerOrientation.Horizontal)
            {
                AddToClassList(HorizontalClass);
                RemoveFromClassList(VerticalClass);
            }
            else
            {
                AddToClassList(VerticalClass);
                RemoveFromClassList(HorizontalClass);
            }
        }
    }
}
