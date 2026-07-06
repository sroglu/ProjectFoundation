using System;
using PFound.UISystem.Core;
using PFound.UISystem.Utils;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Search Bar — full-width search input.
    ///
    /// Composition:
    ///   VisualElement (this) — bar container
    ///   Label (_leadingIcon) — search icon (MaterialSymbols.Search)
    ///   TextField (_input) — actual text input field
    ///   Label (_trailingAction) — optional trailing icon (e.g. clear/avatar)
    ///
    /// M3 spec:
    ///   Height: 56dp
    ///   Background: --m3-surface-container-high
    ///   Shape: Full (28dp corners = pill when height 56dp)
    ///   Leading: search icon (--m3-on-surface)
    ///   Hint text: "Search" (localizable via Placeholder)
    ///
    /// USS: search-bar.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3SearchBar : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass          = "m3-search-bar";
        private const string LeadingIconClass   = "m3-search-bar__leading";
        private const string InputClass         = "m3-search-bar__input";
        private const string TrailingClass      = "m3-search-bar__trailing";
        private const string FocusedClass       = "m3-search-bar--focused";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly Label         _leadingIcon;
        private readonly TextField     _input;
        private readonly Label         _trailingAction;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string _placeholder = "Search";
        private string _leadingIconCodepoint;
        private string _trailingIconCodepoint;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the search query changes.</summary>
        public event Action<string> OnValueChanged;

        /// <summary>Fired when the user submits (presses Enter).</summary>
        public event Action<string> OnSubmit;

        /// <summary>Current search text.</summary>
        public string Value
        {
            get => _input.value;
            set => _input.value = value;
        }

        /// <summary>Leading icon codepoint (MaterialSymbols). Default: Search icon.</summary>
        [UxmlAttribute("leading-icon")]
        public string LeadingIcon
        {
            get => _leadingIconCodepoint;
            set
            {
                _leadingIconCodepoint = value;
                _leadingIcon.text = string.IsNullOrEmpty(value) ? MaterialSymbols.Search : value;
            }
        }

        /// <summary>Placeholder text shown when empty. Default: "Search".</summary>
        [UxmlAttribute("placeholder")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? "Search";
                // In Unity's TextField, hint is shown via the placeholder attribute
                // accessible via text when empty — handled by USS :placeholder pseudo-class
            }
        }

        /// <summary>Optional trailing action icon (MaterialSymbols codepoint).</summary>
        [UxmlAttribute("trailing-icon")]
        public string TrailingIcon
        {
            get => _trailingIconCodepoint;
            set
            {
                _trailingIconCodepoint = value;
                _trailingAction.text   = value ?? string.Empty;
                _trailingAction.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3SearchBar()
        {
            AddToClassList(BaseClass);
            focusable = false; // Focus goes to inner TextField

            // Critical layout — inline to guarantee application
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = 56;
            style.borderTopLeftRadius = 28;
            style.borderTopRightRadius = 28;
            style.borderBottomLeftRadius = 28;
            style.borderBottomRightRadius = 28;
            style.paddingLeft = 16;
            style.paddingRight = 16;

            _leadingIcon = new M3Label(MaterialSymbols.Search);
            _leadingIcon.AddToClassList("m3-icon");
            _leadingIcon.AddToClassList(LeadingIconClass);

            _input = new TextField();
            _input.AddToClassList(InputClass);
            _input.AddToClassList("m3-body-large");
            // Strip Unity's default TextField chrome
            _input.style.backgroundColor = Color.clear;
            _input.style.borderTopWidth = 0;
            _input.style.borderBottomWidth = 0;
            _input.style.borderLeftWidth = 0;
            _input.style.borderRightWidth = 0;
            _input.style.flexGrow = 1;
            _input.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var textInput = _input.Q(className: "unity-text-field__input");
                if (textInput != null)
                {
                    textInput.style.backgroundColor = Color.clear;
                    textInput.style.borderTopWidth = 0;
                    textInput.style.borderBottomWidth = 0;
                    textInput.style.borderLeftWidth = 0;
                    textInput.style.borderRightWidth = 0;
                    textInput.style.paddingLeft = 0;
                    textInput.style.paddingRight = 0;
                    textInput.style.paddingTop = 0;
                    textInput.style.paddingBottom = 0;
                    textInput.style.marginLeft = 0;
                    textInput.style.marginRight = 0;
                    textInput.style.marginTop = 0;
                    textInput.style.marginBottom = 0;
                }
            });

            _trailingAction = new M3Label();
            _trailingAction.AddToClassList("m3-icon");
            _trailingAction.AddToClassList(TrailingClass);
            _trailingAction.style.display = DisplayStyle.None;

            Add(_leadingIcon);
            Add(_input);
            Add(_trailingAction);

            _input.RegisterCallback<FocusInEvent>(_ => AddToClassList(FocusedClass));
            _input.RegisterCallback<FocusOutEvent>(_ => RemoveFromClassList(FocusedClass));
            _input.RegisterValueChangedCallback(evt => OnValueChanged?.Invoke(evt.newValue));
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == UnityEngine.KeyCode.Return ||
                    evt.keyCode == UnityEngine.KeyCode.KeypadEnter)
                    OnSubmit?.Invoke(_input.value);
            });
        }

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;
            style.backgroundColor = theme.GetColor(Enums.ColorRole.SurfaceContainerHigh);
        }
    }
}
