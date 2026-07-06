using System;
using PFound.UISystem.Core;
using PFound.UISystem.Utils;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Top App Bar.
    ///
    /// Composition:
    ///   VisualElement (this) — full-width bar container
    ///   VisualElement (_leading) — navigation icon slot (M3Button or Label)
    ///   Label (_headline) — screen title / headline label
    ///   VisualElement (_actions) — trailing action icon slots
    ///
    /// Variants:
    ///   CenterAligned: headline centered, 1 trailing action max
    ///   Small: headline left-aligned, up to 3 trailing actions
    ///
    /// M3 spec:
    ///   Height: 64dp
    ///   Background: --m3-surface-container
    ///   Headline: Title Large (22sp/400w)
    ///   On-scroll: elevation lifts to level 2 (shadow via M3Surface)
    ///
    /// USS: top-app-bar.uss. All colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3TopAppBar : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass          = "m3-top-app-bar";
        private const string CenterAlignedClass = "m3-top-app-bar--center-aligned";
        private const string SmallClass         = "m3-top-app-bar--small";
        private const string LeadingClass       = "m3-top-app-bar__leading";
        private const string HeadlineClass      = "m3-top-app-bar__headline";
        private const string ActionsClass       = "m3-top-app-bar__actions";
        private const string NavIconClass       = "m3-top-app-bar__nav-icon";
        private const string ActionIconClass    = "m3-top-app-bar__action-icon";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _leading;
        private readonly Label         _navIcon;
        private readonly Label         _headline;
        private readonly VisualElement _actions;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private TopAppBarVariant _variant = TopAppBarVariant.Small;
        private string           _headlineText = string.Empty;
        private bool             _showNavIcon  = true;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum TopAppBarVariant { Small, CenterAligned }

        /// <summary>Fired when the navigation icon button is clicked.</summary>
        public event Action OnNavigationClick;

        /// <summary>Small (left-aligned) or CenterAligned headline variant.</summary>
        [UxmlAttribute("variant")]
        public TopAppBarVariant Variant
        {
            get => _variant;
            set
            {
                if (_variant == value) return;
                _variant = value;
                ApplyVariant();
            }
        }

        /// <summary>Headline / title text.</summary>
        [UxmlAttribute("headline")]
        public string Headline
        {
            get => _headlineText;
            set
            {
                _headlineText  = value ?? string.Empty;
                _headline.text = _headlineText;
            }
        }

        /// <summary>Whether to show the navigation icon (hamburger/back). Default true.</summary>
        [UxmlAttribute("show-nav-icon")]
        public bool ShowNavIcon
        {
            get => _showNavIcon;
            set
            {
                _showNavIcon = value;
                _leading.style.display = _showNavIcon
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3TopAppBar()
        {
            AddToClassList(BaseClass);

            // Leading / navigation icon
            _leading = new VisualElement();
            _leading.AddToClassList(LeadingClass);

            _navIcon           = new M3Label(MaterialSymbols.Menu);
            _navIcon.AddToClassList("m3-icon");
            _navIcon.AddToClassList(NavIconClass);
            _navIcon.RegisterCallback<ClickEvent>(_ => OnNavigationClick?.Invoke());
            _leading.Add(_navIcon);

            // Headline
            _headline = new M3Label(_headlineText);
            _headline.AddToClassList(HeadlineClass);
            _headline.AddToClassList("m3-title-large");

            // Trailing actions container
            _actions = new VisualElement();
            _actions.AddToClassList(ActionsClass);

            Add(_leading);
            Add(_headline);
            Add(_actions);

            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds an action icon button to the trailing actions area.
        /// Returns the created Label so callers can attach click handlers.
        /// </summary>
        public Label AddActionIcon(string codepoint)
        {
            var icon = new M3Label(codepoint);
            icon.AddToClassList("m3-icon");
            icon.AddToClassList(ActionIconClass);
            _actions.Add(icon);
            return icon;
        }

        /// <summary>Removes all action icons.</summary>
        public void ClearActions() => _actions.Clear();

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void ApplyVariant()
        {
            if (_variant == TopAppBarVariant.CenterAligned)
            {
                AddToClassList(CenterAlignedClass);
                RemoveFromClassList(SmallClass);
            }
            else
            {
                AddToClassList(SmallClass);
                RemoveFromClassList(CenterAlignedClass);
            }
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }
    }
}
