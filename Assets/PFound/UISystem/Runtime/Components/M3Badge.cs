using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Badge — a small status indicator attached to another element.
    ///
    /// Variants:
    ///   Small: 8dp dot, no text
    ///   Large: 16dp+ pill, shows count or short label
    ///
    /// Composition:
    ///   VisualElement (this) — absolute-positioned overlay
    ///   Label (_countLabel) — optional count text (Large variant only)
    ///
    /// M3 spec:
    ///   Small: 6dp, --m3-error background
    ///   Large: 16dp height, --m3-error background, --m3-on-error text
    ///   Positioning: absolute, top-right corner of host element
    ///
    /// USS: badge.uss. Colors via var(--m3-*) tokens.
    ///
    /// Usage:
    ///   var badge = new M3Badge { Count = 3 };
    ///   myIcon.Add(badge); // badge overlays top-right of myIcon
    /// </summary>
    [UxmlElement]
    public partial class M3Badge : VisualElement
    {
        private const string BaseClass      = "m3-badge";
        private const string SmallClass     = "m3-badge--small";
        private const string LargeClass     = "m3-badge--large";
        private const string CountClass     = "m3-badge__count";

        private readonly Label _countLabel;

        private int    _count;
        private bool   _forceSmall;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Display count. 0 = small dot; >0 = large pill with number.
        /// Values >999 display as "999+".
        /// </summary>
        [UxmlAttribute("count")]
        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                ApplyVariant();
            }
        }

        /// <summary>Force small (dot) variant regardless of count.</summary>
        [UxmlAttribute("small")]
        public bool ForceSmall
        {
            get => _forceSmall;
            set
            {
                _forceSmall = value;
                ApplyVariant();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Badge()
        {
            AddToClassList(BaseClass);
            style.position = Position.Absolute;
            pickingMode    = PickingMode.Ignore;

            _countLabel = new M3Label();
            _countLabel.AddToClassList(CountClass);
            _countLabel.AddToClassList("m3-label-small");
            Add(_countLabel);

            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void ApplyVariant()
        {
            bool isLarge = !_forceSmall && _count > 0;

            EnableInClassList(LargeClass, isLarge);
            EnableInClassList(SmallClass, !isLarge);

            if (isLarge)
            {
                _countLabel.style.display = DisplayStyle.Flex;
                _countLabel.text = _count > 999 ? "999+" : _count.ToString();
            }
            else
            {
                _countLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
