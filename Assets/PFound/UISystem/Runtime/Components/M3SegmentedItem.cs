using System;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Segmented Button Item — a single segment within an M3SegmentedButton.
    ///
    /// USS: segmented-button.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3SegmentedItem : VisualElement
    {
        internal const string BaseClass      = "m3-segmented-item";
        internal const string ActiveClass    = "m3-segmented-item--active";
        internal const string LastClass      = "m3-segmented-item--last";
        internal const string IconClass      = "m3-segmented-item__icon";
        internal const string LabelClass     = "m3-segmented-item__label";

        private readonly Label         _checkIcon;
        private readonly Label         _customIcon;
        private readonly Label         _label;

        private string _labelText = string.Empty;
        private string _iconCodepoint;
        private bool   _active;

        internal event Action<M3SegmentedItem> OnSegmentClicked;

        /// <summary>Label text for this segment.</summary>
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

        /// <summary>Optional icon (MaterialSymbols codepoint). Shows in addition to label.</summary>
        [UxmlAttribute("icon")]
        public string Icon
        {
            get => _iconCodepoint;
            set
            {
                _iconCodepoint = value;
                _customIcon.text = value ?? string.Empty;
                _customIcon.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Whether this segment is currently selected.</summary>
        public bool Active
        {
            get => _active;
            internal set
            {
                _active = value;
                EnableInClassList(ActiveClass, _active);
                // M3: show checkmark when active, custom icon when inactive+has icon
                _checkIcon.style.display = _active ? DisplayStyle.Flex : DisplayStyle.None;
                if (!string.IsNullOrEmpty(_iconCodepoint))
                    _customIcon.style.display = _active ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public M3SegmentedItem()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;
            focusable   = true;

            // Critical layout — inline to guarantee application
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.paddingLeft = 12;
            style.paddingRight = 12;

            // M3 uses a checkmark icon when the segment is active
            _checkIcon = new M3Label("\ue876"); // Done/Checkmark
            _checkIcon.AddToClassList("m3-icon");
            _checkIcon.AddToClassList(IconClass);
            _checkIcon.style.display = DisplayStyle.None;

            _customIcon = new M3Label();
            _customIcon.AddToClassList("m3-icon");
            _customIcon.AddToClassList(IconClass);
            _customIcon.style.display = DisplayStyle.None;

            _label = new M3Label(_labelText);
            _label.AddToClassList(LabelClass);

            Add(_checkIcon);
            Add(_customIcon);
            Add(_label);

            RegisterCallback<ClickEvent>(_ => OnSegmentClicked?.Invoke(this));
        }
    }
}
