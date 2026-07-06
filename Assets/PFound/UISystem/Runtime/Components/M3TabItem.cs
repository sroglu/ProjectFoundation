using System;
using PFound.UISystem.Core;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Tab Item — a single tab within an M3Tabs container.
    ///
    /// Composition:
    ///   VisualElement (this) — tab button wrapper
    ///   Label (_icon) — optional icon (MaterialSymbols)
    ///   Label (_label) — tab label text
    ///
    /// USS: tabs.uss. Colors via var(--m3-*) tokens.
    /// Active indicator position is managed by parent M3Tabs.
    /// </summary>
    [UxmlElement]
    public partial class M3TabItem : VisualElement
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        internal const string BaseClass    = "m3-tab-item";
        internal const string ActiveClass  = "m3-tab-item--active";
        internal const string IconClass    = "m3-tab-item__icon";
        internal const string LabelClass   = "m3-tab-item__label";
        internal const string IndicatorClass = "m3-tab-item__indicator";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly Label         _icon;
        private readonly Label         _label;
        private readonly VisualElement _indicator;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string _labelText = string.Empty;
        private string _iconCodepoint;
        private bool   _active;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when this tab is clicked.</summary>
        internal event Action<M3TabItem> OnTabClicked;

        /// <summary>Tab label text.</summary>
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

        /// <summary>Optional Material Symbols codepoint for tab icon.</summary>
        [UxmlAttribute("icon")]
        public string Icon
        {
            get => _iconCodepoint;
            set
            {
                _iconCodepoint = value;
                _icon.text    = value ?? string.Empty;
                _icon.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Whether this tab is currently selected.</summary>
        public bool Active
        {
            get => _active;
            internal set
            {
                if (_active == value) return;
                _active = value;
                EnableInClassList(ActiveClass, _active);
                // Opacity animated via USS transition on .m3-tab-item--active .m3-tab-item__indicator
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3TabItem()
        {
            AddToClassList(BaseClass);
            focusable   = true;
            pickingMode = PickingMode.Position;

            // Critical layout — inline to guarantee application
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.FlexEnd;
            style.paddingBottom = 0;
            style.minWidth = 48;
            style.overflow = Overflow.Hidden;
            style.position = Position.Relative;

            _icon = new M3Label();
            _icon.AddToClassList("m3-icon");
            _icon.AddToClassList(IconClass);
            _icon.style.display = DisplayStyle.None; // hidden until icon set
            _icon.style.fontSize = 24;
            _icon.style.width = 24;
            _icon.style.height = 24;
            _icon.style.overflow = Overflow.Hidden;
            _icon.style.marginTop = 0;
            _icon.style.marginBottom = 0;
            _icon.style.paddingTop = 0;
            _icon.style.paddingBottom = 0;

            _label = new M3Label(_labelText);
            _label.AddToClassList(LabelClass);
            _label.style.height = 16;
            _label.style.marginTop = 2;
            _label.style.marginBottom = 10;

            _indicator = new VisualElement();
            _indicator.AddToClassList(IndicatorClass);
            // Initial opacity=0 via USS; becomes 1 via .m3-tab-item--active transition

            Add(_icon);
            Add(_label);
            Add(_indicator);

            RegisterCallback<ClickEvent>(_ => OnTabClicked?.Invoke(this));
        }

        // Indicator opacity is now fully controlled by USS transitions
        // via .m3-tab-item--active .m3-tab-item__indicator { opacity: 1; }
    }
}
