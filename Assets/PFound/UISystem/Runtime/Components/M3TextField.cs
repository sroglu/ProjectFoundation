using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Text Field component.
    ///
    /// Variants:
    ///   Filled   — surface-container-highest bg, 1dp bottom indicator
    ///   Outlined — 1dp outline border, no fill
    ///
    /// Floating label:
    ///   Resting: 16px inside field, centered vertically
    ///   Floating: 12px, translated up (on focus or non-empty)
    ///
    /// M3 Spec:
    ///   Height: 56dp
    ///   Filled: shape-extra-small-top corners (4dp top, 0dp bottom)
    ///   Outlined: 4dp corners
    ///
    /// USS: textfield.uss
    /// </summary>
    [UxmlElement]
    public partial class M3TextField : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass          = "m3-textfield";
        private const string FilledClass        = "m3-textfield--filled";
        private const string OutlinedClass      = "m3-textfield--outlined";
        private const string FloatingClass      = "m3-textfield--floating";
        private const string FocusedClass       = "m3-textfield--focused";
        private const string ErrorClass         = "m3-textfield--error";
        private const string ContainerClass     = "m3-textfield__container";
        private const string LabelClass         = "m3-textfield__label";
        private const string InputClass         = "m3-textfield__input";
        private const string IndicatorClass     = "m3-textfield__indicator";
        private const string HelperClass        = "m3-textfield__helper";

        // Resolved theme colors
        private Color _themePrimary;
        private Color _themeOutline;
        private Color _themeSurfaceContainerHighest;
        private Color _themeSurface;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Surface _container;
        private readonly Label          _floatingLabel;
        private readonly TextField      _input;
        private readonly VisualElement  _indicator;  // bottom indicator for Filled variant
        private readonly Label          _helperText;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string           _label       = string.Empty;
        private TextFieldVariant _variant     = TextFieldVariant.Filled;
        private string           _helperMsg   = string.Empty;
        private string           _errorMsg    = string.Empty;
        private bool             _hasError    = false;
        private int              _maxLength   = -1;
        private bool             _isFloating  = false;
        private IVisualElementScheduledItem _labelAnim;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the text value changes.</summary>
        public event Action<string> OnValueChanged;

        /// <summary>Fired when the user submits (Enter key).</summary>
        public event Action<string> OnSubmit;

        /// <summary>Current text value.</summary>
        [UxmlAttribute("value")]
        public string Value
        {
            get => _input.value;
            set
            {
                _input.SetValueWithoutNotify(value);
                UpdateFloatingState();
            }
        }

        /// <summary>Floating label text.</summary>
        [UxmlAttribute("label")]
        public string Label
        {
            get => _label;
            set { _label = value; _floatingLabel.text = value ?? string.Empty; }
        }

        [UxmlAttribute("variant")]
        public TextFieldVariant Variant
        {
            get => _variant;
            set { _variant = value; ApplyVariant(); }
        }

        [UxmlAttribute("helper-text")]
        public string HelperText
        {
            get => _helperMsg;
            set { _helperMsg = value; UpdateHelperText(); }
        }

        [UxmlAttribute("error-text")]
        public string ErrorText
        {
            get => _errorMsg;
            set { _errorMsg = value; UpdateHelperText(); }
        }

        [UxmlAttribute("has-error")]
        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                EnableInClassList(ErrorClass, value);
                UpdateHelperText();
            }
        }

        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set
            {
                base.Disabled = value;
                _input.SetEnabled(!value);
            }
        }

        [UxmlAttribute("max-length")]
        public int MaxLength
        {
            get => _maxLength;
            set { _maxLength = value; _input.maxLength = value; }
        }

        [UxmlAttribute("placeholder")]
        public string Placeholder
        {
            get => _input.textEdition.placeholder;
            set => _input.textEdition.placeholder = value ?? string.Empty;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3TextField()
        {
            AddToClassList(BaseClass);
            pickingMode  = PickingMode.Position;

            // --- Container ---
            _container = new M3Surface { pickingMode = PickingMode.Position };
            _container.AddToClassList(ContainerClass);
            _container.style.flexGrow      = 1;
            _container.style.height        = 56f;
            _container.style.position      = Position.Relative;
            _container.style.flexDirection = FlexDirection.Column;
            _container.style.justifyContent = Justify.Center;
            _container.style.paddingLeft   = 16f;
            _container.style.paddingRight  = 16f;
            _container.style.paddingTop    = 16f;
            _container.style.paddingBottom = 16f;
            _container.style.overflow      = Overflow.Hidden;

            // --- Floating label (in flow — padding controls position) ---
            _floatingLabel = new M3Label(string.Empty);
            _floatingLabel.AddToClassList("m3-body");
            _floatingLabel.AddToClassList(LabelClass);
            _floatingLabel.pickingMode = PickingMode.Ignore;
            _floatingLabel.style.overflow = Overflow.Hidden;
            _floatingLabel.style.marginTop    = 0;
            _floatingLabel.style.marginBottom = 0;
            _floatingLabel.style.paddingTop    = 0;
            _floatingLabel.style.paddingBottom = 0;
            _container.Add(_floatingLabel);

            // --- Native TextField (input, in flow below label) ---
            _input = new TextField { pickingMode = PickingMode.Position };
            _input.AddToClassList(InputClass);
            _input.style.height     = 24f;
            _input.style.fontSize   = 16f;
            _input.style.flexShrink = 0;
            // Remove Unity's default border/bg on TextField and its internal TextInput
            _input.style.borderTopWidth    = 0;
            _input.style.borderBottomWidth = 0;
            _input.style.borderLeftWidth   = 0;
            _input.style.borderRightWidth  = 0;
            _input.style.paddingLeft   = 0;
            _input.style.paddingRight  = 0;
            _input.style.paddingTop    = 0;
            _input.style.paddingBottom = 0;
            _input.style.marginLeft    = 0;
            _input.style.marginRight   = 0;
            _input.style.marginTop     = 0;
            _input.style.marginBottom  = 0;
            // _container is an M3Surface — UIR propagates its custom unityMaterial
            // (the SDF shape shader) to descendants, so the TextField glyph mesh and
            // caret would be rendered through the SDF shader and vanish (same gotcha
            // as RippleElement, M3Label inside M3Surface, and the M3Checkbox icon).
            // Reset both the outer TextField and its internal TextInput element back
            // to the default UI text material.
            _input.style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
            _input.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var textInput = _input.Q(className: "unity-text-field__input");
                if (textInput != null)
                {
                    textInput.style.borderTopWidth      = 0;
                    textInput.style.borderBottomWidth   = 0;
                    textInput.style.borderLeftWidth     = 0;
                    textInput.style.borderRightWidth    = 0;
                    textInput.style.paddingLeft         = 0;
                    textInput.style.paddingRight        = 0;
                    textInput.style.paddingTop          = 0;
                    textInput.style.paddingBottom       = 0;
                    // The internal TextInput is the element that actually emits the
                    // glyph + caret meshes — it needs its own material reset too.
                    textInput.style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
                }
            });
            _container.Add(_input);

            // --- Bottom indicator (Filled variant) ---
            _indicator = new VisualElement();
            _indicator.AddToClassList(IndicatorClass);
            _indicator.style.position = Position.Absolute;
            _indicator.style.bottom   = 0;
            _indicator.style.left     = 0;
            _indicator.style.right    = 0;
            _indicator.style.height   = 1f;
            // Clip indicator inside rounded bottom corners
            _indicator.style.borderBottomLeftRadius  = 4f;
            _indicator.style.borderBottomRightRadius = 4f;
            _indicator.style.overflow = Overflow.Hidden;
            _container.Add(_indicator);

            Add(_container);

            // --- Helper / error text ---
            _helperText = new M3Label(string.Empty);
            _helperText.AddToClassList("m3-caption");
            _helperText.AddToClassList(HelperClass);
            _helperText.style.paddingLeft = 16f;
            _helperText.style.marginTop   = 4f;
            Add(_helperText);

            // --- Events ---
            _input.RegisterValueChangedCallback(evt =>
            {
                UpdateFloatingState();
                OnValueChanged?.Invoke(evt.newValue);
            });

            _input.RegisterCallback<FocusInEvent>(_ => SetFocused(true));
            _input.RegisterCallback<FocusOutEvent>(_ => SetFocused(false));
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    OnSubmit?.Invoke(_input.value);
            });
            // Click anywhere on the container → focus the input
            _container.RegisterCallback<ClickEvent>(_ => _input.Focus());

            ApplyVariant();
            UpdateFloatingState(animate: false);
        }

        // ------------------------------------------------------------------ //
        //  Visual State                                                        //
        // ------------------------------------------------------------------ //

        private void ApplyVariant()
        {
            _container.RemoveFromClassList(FilledClass);
            _container.RemoveFromClassList(OutlinedClass);

            // Both variants: 4dp all corners — SDF + VisualElement border-radius
            _container.CornerRadius = 4f;
            _container.CornerRadiusTL = 4f;
            _container.CornerRadiusTR = 4f;
            _container.CornerRadiusBL = 4f;
            _container.CornerRadiusBR = 4f;
            _container.style.borderTopLeftRadius     = 4f;
            _container.style.borderTopRightRadius    = 4f;
            _container.style.borderBottomLeftRadius  = 4f;
            _container.style.borderBottomRightRadius = 4f;

            if (_variant == TextFieldVariant.Filled)
            {
                _container.AddToClassList(FilledClass);
                _container.OutlineThickness = 0f;
                _container.FillColorOverride = _themeSurfaceContainerHighest;
                _indicator.style.display = DisplayStyle.Flex;
                _container.style.overflow = Overflow.Hidden;
            }
            else
            {
                _container.AddToClassList(OutlinedClass);
                _container.OutlineThickness = 1f;
                _container.OutlineColor = _themeOutline;
                _container.FillColorOverride = Color.clear;
                _indicator.style.display = DisplayStyle.None;
                // Visible overflow so floating label at top:-8 is not clipped
                _container.style.overflow = Overflow.Visible;
            }

        }

        private void SetFocused(bool focused)
        {
            EnableInClassList(FocusedClass, focused);
            _container.EnableInClassList(FocusedClass, focused);

            if (_variant == TextFieldVariant.Filled)
            {
                _indicator.style.height = focused ? 2f : 1f;
            }
            else
            {
                _container.OutlineThickness = focused ? 2f : 1f;
                _container.OutlineColor = focused ? _themePrimary : _themeOutline;
            }

            UpdateFloatingState();
        }

        private void UpdateFloatingState(bool animate = true)
        {
            bool shouldFloat = !string.IsNullOrEmpty(_input.value) || _container.ClassListContains(FocusedClass);
            EnableInClassList(FloatingClass, shouldFloat);

            // Cancel any running label animation
            _labelAnim?.Pause();
            _labelAnim = null;

            bool stateChanged = shouldFloat != _isFloating;
            _isFloating = shouldFloat;

            if (shouldFloat)
                ApplyFloatingLayout();
            else
                ApplyRestingLayout();

            // Animate fontSize transition if state changed and panel is live
            if (stateChanged && animate && panel != null)
            {
                float fromSize = shouldFloat ? 16f : 12f;
                float toSize   = shouldFloat ? 12f : 16f;
                float fromInputOpacity = shouldFloat ? 0f : 1f;
                float toInputOpacity   = shouldFloat ? 1f : 0f;
                float fromInputHeight  = shouldFloat ? 0f : 24f;
                float toInputHeight    = shouldFloat ? 24f : 0f;

                _labelAnim = M3Animate.Float(this, 0f, 1f, 200f, t =>
                {
                    _floatingLabel.style.fontSize = Mathf.Lerp(fromSize, toSize, t);
                    _input.style.opacity = Mathf.Lerp(fromInputOpacity, toInputOpacity, t);
                    _input.style.height  = Mathf.Lerp(fromInputHeight, toInputHeight, t);
                });
            }
        }

        private void ApplyFloatingLayout()
        {
            // M3 spec (populated/focused):
            //   Filled:   8dp top → 16dp label (12sp) → 24dp input (16sp) → 8dp bottom = 56dp
            //   Outlined: label straddles top border (notch), 16dp top → 24dp input → 16dp bottom
            _floatingLabel.style.fontSize        = 12f;
            _floatingLabel.style.height          = 16f;
            _floatingLabel.style.unityTextAlign   = TextAnchor.UpperLeft;
            _floatingLabel.style.display         = DisplayStyle.Flex;
            _input.style.display                 = DisplayStyle.Flex;
            _input.style.height                  = 24f;
            _input.style.opacity                 = 1f;

            if (_variant == TextFieldVariant.Outlined)
            {
                _floatingLabel.style.position         = Position.Absolute;
                _floatingLabel.style.top              = -8f;
                _floatingLabel.style.left             = 16f;
                _floatingLabel.style.paddingLeft      = 4f;
                _floatingLabel.style.paddingRight     = 4f;
                _floatingLabel.style.backgroundColor  = new StyleColor(_themeSurface);
                _container.style.paddingTop    = 16f;
                _container.style.paddingBottom = 16f;
                _container.style.justifyContent = Justify.Center;
            }
            else
            {
                _floatingLabel.style.position         = Position.Relative;
                _floatingLabel.style.top              = StyleKeyword.Auto;
                _floatingLabel.style.left             = StyleKeyword.Auto;
                _floatingLabel.style.paddingLeft      = 0f;
                _floatingLabel.style.paddingRight     = 0f;
                _floatingLabel.style.backgroundColor  = StyleKeyword.None;
                _container.style.paddingTop    = 8f;
                _container.style.paddingBottom = 8f;
                _container.style.justifyContent = Justify.FlexStart;
            }
        }

        private void ApplyRestingLayout()
        {
            // M3 resting: label 16sp bodyLarge, vertically centered in 56dp
            _floatingLabel.style.fontSize        = 16f;
            _floatingLabel.style.height          = 24f;
            _floatingLabel.style.unityTextAlign   = TextAnchor.MiddleLeft;
            _floatingLabel.style.position        = Position.Relative;
            _floatingLabel.style.top             = StyleKeyword.Auto;
            _floatingLabel.style.left            = StyleKeyword.Auto;
            _floatingLabel.style.paddingLeft     = 0f;
            _floatingLabel.style.paddingRight    = 0f;
            _floatingLabel.style.backgroundColor = StyleKeyword.None;
            _floatingLabel.style.display         = DisplayStyle.Flex;
            _input.style.display  = DisplayStyle.Flex;
            _input.style.height   = 0f;
            _input.style.opacity  = 0f;
            _container.style.paddingTop    = 16f;
            _container.style.paddingBottom = 16f;
            _container.style.justifyContent = Justify.Center;
        }

        private void UpdateHelperText()
        {
            if (_hasError && !string.IsNullOrEmpty(_errorMsg))
                _helperText.text = _errorMsg;
            else
                _helperText.text = _helperMsg;
            // Color resolved by USS: .m3-textfield--error .m3-textfield__helper { color: var(--m3-error) }
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimary                 = theme.GetColor(ColorRole.Primary);
            _themeOutline                 = theme.GetColor(ColorRole.Outline);
            _themeSurfaceContainerHighest = theme.GetColor(ColorRole.SurfaceContainerHighest);
            _themeSurface                 = theme.GetColor(ColorRole.Surface);

            ApplyVariant();
            UpdateHelperText();
            UpdateFloatingState(animate: false);
        }
    }
}
