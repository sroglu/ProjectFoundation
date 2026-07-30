using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using PFound.UISystem.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Floating Action Button.
    ///
    /// Sizes:
    ///   Small   — 40×40dp, 12dp corners
    ///   Regular — 56×56dp, 16dp corners
    ///   Large   — 96×96dp, 28dp corners
    ///
    /// Extended mode: Row with icon + label, min-width 80dp, 16dp corners.
    ///
    /// M3 Spec:
    ///   Colors: primary-container bg, on-primary-container icon/text
    ///   Elevation: Level 3
    ///
    /// USS: fab.uss
    /// </summary>
    [UxmlElement]
    public partial class M3FAB : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass     = "m3-fab";
        private const string SmallClass    = "m3-fab--small";
        private const string RegularClass  = "m3-fab--regular";
        private const string LargeClass    = "m3-fab--large";
        private const string ExtendedClass = "m3-fab--extended";
        private const string IconClass     = "m3-fab__icon";
        private const string LabelClass    = "m3-fab__label";

        // ------------------------------------------------------------------ //
        //  Size config                                                         //
        // ------------------------------------------------------------------ //
        private static readonly (float size, float radius)[] SizeConfig =
        {
            (40f, 12f),  // Small
            (56f, 16f),  // Regular
            (96f, 28f),  // Large
        };

        // M3 elevation-3 SDF shadow approximation
        private const float ElevationShadowBlur    = 8f;
        private const float ElevationShadowOffsetY = 4f;

        // Resolved theme colors
        private Color _themePrimaryContainer;
        private Color _themeOnPrimaryContainer;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Surface      _container;
        private readonly Label          _iconEl;
        private readonly Label          _labelEl;
        private readonly RippleElement  _ripple;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private FABSize _size     = FABSize.Regular;
        private bool    _extended = false;
        private string  _text     = string.Empty;
        private FABIcon _fabIcon  = FABIcon.Add;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the FAB is pressed.</summary>
        public event Action OnClick;

        /// <summary>FAB size (Small, Regular, Large).</summary>
        [UxmlAttribute("size")]
        public FABSize Size
        {
            get => _size;
            set { _size = value; ApplySize(); }
        }

        /// <summary>Extended mode — shows icon + label side by side.</summary>
        [UxmlAttribute("extended")]
        public bool Extended
        {
            get => _extended;
            set { _extended = value; ApplySize(); }
        }

        /// <summary>Label text (shown in extended mode).</summary>
        [UxmlAttribute("text")]
        public string Text
        {
            get => _text;
            set { _text = value; _labelEl.text = value ?? string.Empty; }
        }

        /// <summary>Icon type (mapped to Material Symbols font glyph).</summary>
        [UxmlAttribute("icon")]
        public FABIcon Icon
        {
            get => _fabIcon;
            set { _fabIcon = value; _iconEl.text = GetIconText(value); }
        }

        /// <summary>When true, dims and ignores input.</summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set => base.Disabled = value;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3FAB()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;

            // --- Container ---
            _container = new M3Surface { pickingMode = PickingMode.Position };
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.alignItems    = Align.Center;
            _container.style.justifyContent = Justify.Center;
            _container.style.overflow      = Overflow.Hidden;

            // --- Ripple ---
            _ripple = new RippleElement();
            _container.Add(_ripple);

            // --- Icon (Material Symbols font) ---
            _iconEl = new M3Label(GetIconText(_fabIcon));
            _iconEl.AddToClassList("m3-icon");
            _iconEl.AddToClassList(IconClass);
            _iconEl.pickingMode = PickingMode.Ignore;
            _container.Add(_iconEl);

            // --- Label (extended mode only) ---
            _labelEl = new M3Label(string.Empty);
            _labelEl.AddToClassList("m3-label");
            _labelEl.AddToClassList(LabelClass);
            _labelEl.pickingMode   = PickingMode.Ignore;
            _labelEl.style.display = DisplayStyle.None;
            _container.Add(_labelEl);

            // --- State layer ---
            InitStateLayer(_container, _ripple);

            // --- Events ---
            _container.RegisterCallback<ClickEvent>(OnContainerClicked);

            Add(_container);
            ApplySize();
        }

        // ------------------------------------------------------------------ //
        //  Visual State                                                        //
        // ------------------------------------------------------------------ //

        private void ApplySize()
        {
            // Clear size classes
            _container.RemoveFromClassList(SmallClass);
            _container.RemoveFromClassList(RegularClass);
            _container.RemoveFromClassList(LargeClass);
            _container.RemoveFromClassList(ExtendedClass);

            float appliedRadius;
            if (_extended)
            {
                _container.AddToClassList(ExtendedClass);
                appliedRadius = 16f;
                _container.CornerRadius = appliedRadius;
                _container.style.borderTopLeftRadius     = appliedRadius;
                _container.style.borderTopRightRadius    = appliedRadius;
                _container.style.borderBottomLeftRadius  = appliedRadius;
                _container.style.borderBottomRightRadius = appliedRadius;
                _container.style.width  = StyleKeyword.Auto;
                _container.style.height = 56f;
                _container.style.minWidth = 80f;
                _container.style.paddingLeft  = 16f;
                _container.style.paddingRight = 20f;
                _iconEl.style.fontSize    = 24f;
                _iconEl.style.marginRight = 8f;
                _labelEl.style.display = DisplayStyle.Flex;
            }
            else
            {
                var (size, radius) = SizeConfig[(int)_size];
                appliedRadius = radius;
                string sizeClass = _size switch
                {
                    FABSize.Small   => SmallClass,
                    FABSize.Large   => LargeClass,
                    _               => RegularClass,
                };
                _container.AddToClassList(sizeClass);
                _container.CornerRadius = radius;
                _container.style.borderTopLeftRadius     = radius;
                _container.style.borderTopRightRadius    = radius;
                _container.style.borderBottomLeftRadius  = radius;
                _container.style.borderBottomRightRadius = radius;
                _container.style.width  = size;
                _container.style.height = size;
                _container.style.minWidth = StyleKeyword.Auto;
                _container.style.paddingLeft  = 0f;
                _container.style.paddingRight = 0f;

                _iconEl.style.fontSize    = _size == FABSize.Large ? 36f : 24f;
                _iconEl.style.marginRight = 0f;
                _labelEl.style.display = DisplayStyle.None;
            }

            // M3 elevation-3 shadow on the SDF surface
            _container.ShadowBlur    = ElevationShadowBlur;
            _container.ShadowOffsetY = ElevationShadowOffsetY;

            // State + ripple tint — FAB sits on primary-container, so the press
            // overlay + ripple use on-primary-container (dark mor) for contrast.
            StateLayer.OverlayColor = _themeOnPrimaryContainer;
            _ripple.RippleColor     = _themeOnPrimaryContainer;
            _ripple.PeakOpacity     = 0.18f;

            // Mirror the container's corner radius onto the ripple so the painter2D
            // circle stays clipped inside the FAB silhouette instead of bleeding out.
            _ripple.SetClipRadius(appliedRadius);
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimaryContainer   = theme.GetColor(ColorRole.PrimaryContainer);
            _themeOnPrimaryContainer = theme.GetColor(ColorRole.OnPrimaryContainer);

            _container.FillColorOverride = _themePrimaryContainer;
            ApplySize();
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                      //
        // ------------------------------------------------------------------ //

        private void OnContainerClicked(ClickEvent evt)
        {
            if (base.Disabled) return;
            OnClick?.Invoke();
        }

        // ------------------------------------------------------------------ //
        //  Icon Text Mapping                                                   //
        // ------------------------------------------------------------------ //

        private static string GetIconText(FABIcon icon) => icon switch
        {
            FABIcon.Add    => MaterialSymbols.Add,
            FABIcon.Edit   => MaterialSymbols.Edit,
            FABIcon.Mail   => MaterialSymbols.Email,
            FABIcon.Search => MaterialSymbols.Search,
            FABIcon.Star   => MaterialSymbols.Star,
            _              => MaterialSymbols.Add,
        };
    }
}
