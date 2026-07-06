using PFound.UISystem.Core;
using PFound.UISystem.Utils;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style List Item — a single row in a list.
    ///
    /// Variants (line count):
    ///   OneLine: 56dp height, headline only
    ///   TwoLine: 72dp height, headline + supporting text
    ///   ThreeLine: 88dp height, headline + 2-line supporting text
    ///
    /// Slots:
    ///   Leading: avatar image, icon (24dp), or thumbnail — optional
    ///   Trailing: icon, checkbox, metadata text — optional
    ///
    /// Composition:
    ///   VisualElement (this) — row container
    ///   VisualElement (_leading) — leading slot (hidden by default)
    ///   VisualElement (_text) — headline + supporting text column
    ///   VisualElement (_trailing) — trailing slot (hidden by default)
    ///
    /// USS: list.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3ListItem : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass        = "m3-list-item";
        private const string OneLineClass     = "m3-list-item--one-line";
        private const string TwoLineClass     = "m3-list-item--two-line";
        private const string ThreeLineClass   = "m3-list-item--three-line";
        private const string LeadingClass     = "m3-list-item__leading";
        private const string TextClass        = "m3-list-item__text";
        private const string HeadlineClass    = "m3-list-item__headline";
        private const string SupportingClass  = "m3-list-item__supporting";
        private const string TrailingClass    = "m3-list-item__trailing";
        private const string LeadingIconClass      = "m3-list-item__leading--icon";
        private const string LeadingAvatarClass    = "m3-list-item__leading--avatar";
        private const string LeadingThumbnailClass = "m3-list-item__leading--thumbnail";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _leading;
        private readonly VisualElement _text;
        private readonly Label         _headline;
        private readonly Label         _supporting;
        private readonly VisualElement _trailing;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private ListItemVariant _variant = ListItemVariant.OneLine;
        private string          _headlineText  = string.Empty;
        private string          _supportingText = string.Empty;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum ListItemVariant { OneLine, TwoLine, ThreeLine }
        public enum LeadingType { None, Icon, Avatar, Thumbnail }

        /// <summary>One-line, Two-line or Three-line variant.</summary>
        [UxmlAttribute("variant")]
        public ListItemVariant Variant
        {
            get => _variant;
            set
            {
                _variant = value;
                ApplyVariant();
            }
        }

        /// <summary>Primary headline text.</summary>
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

        /// <summary>Secondary supporting text (shown in TwoLine/ThreeLine variants).</summary>
        [UxmlAttribute("supporting")]
        public string Supporting
        {
            get => _supportingText;
            set
            {
                _supportingText  = value ?? string.Empty;
                _supporting.text = _supportingText;
                _supporting.style.display = string.IsNullOrEmpty(_supportingText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Leading slot — add icons, avatars, etc. here.</summary>
        public VisualElement LeadingSlot => _leading;

        /// <summary>Trailing slot — add icons, checkboxes, etc. here.</summary>
        public VisualElement TrailingSlot => _trailing;

        /// <summary>Sets the leading slot to show a Material Symbols icon (24x24).</summary>
        public void SetLeadingIcon(string codepoint)
        {
            _leading.Clear();
            ClearLeadingClasses();
            _leading.AddToClassList(LeadingIconClass);
            var icon = new M3Label(codepoint);
            icon.AddToClassList("m3-icon");
            icon.style.fontSize = 24;
            icon.style.color = new StyleColor(new Color(0.28f, 0.27f, 0.31f)); // on-surface via USS
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _leading.Add(icon);
            _leading.style.display = DisplayStyle.Flex;
        }

        /// <summary>Sets the leading slot to show a circular avatar (40x40).</summary>
        public void SetLeadingAvatar(Texture2D texture)
        {
            _leading.Clear();
            ClearLeadingClasses();
            _leading.AddToClassList(LeadingAvatarClass);
            if (texture != null)
                _leading.style.backgroundImage = new StyleBackground(texture);
            _leading.style.display = DisplayStyle.Flex;
        }

        /// <summary>Sets the leading slot to show a thumbnail (56x56).</summary>
        public void SetLeadingThumbnail(Texture2D texture)
        {
            _leading.Clear();
            ClearLeadingClasses();
            _leading.AddToClassList(LeadingThumbnailClass);
            if (texture != null)
                _leading.style.backgroundImage = new StyleBackground(texture);
            _leading.style.display = DisplayStyle.Flex;
        }

        private void ClearLeadingClasses()
        {
            _leading.RemoveFromClassList(LeadingIconClass);
            _leading.RemoveFromClassList(LeadingAvatarClass);
            _leading.RemoveFromClassList(LeadingThumbnailClass);
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3ListItem()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;
            focusable   = true;

            // Critical layout — inline to guarantee application
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 16;
            style.paddingRight = 16;

            _leading = new VisualElement();
            _leading.AddToClassList(LeadingClass);
            _leading.style.display = DisplayStyle.None;

            _text = new VisualElement();
            _text.AddToClassList(TextClass);

            _headline = new M3Label(_headlineText);
            _headline.AddToClassList(HeadlineClass);
            _headline.AddToClassList("m3-body-large");
            _text.Add(_headline);

            _supporting = new M3Label(_supportingText);
            _supporting.AddToClassList(SupportingClass);
            _supporting.AddToClassList("m3-body-medium");
            _supporting.style.display = DisplayStyle.None;
            _text.Add(_supporting);

            _trailing = new VisualElement();
            _trailing.AddToClassList(TrailingClass);
            _trailing.style.display = DisplayStyle.None;

            Add(_leading);
            Add(_text);
            Add(_trailing);

            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void ApplyVariant()
        {
            RemoveFromClassList(OneLineClass);
            RemoveFromClassList(TwoLineClass);
            RemoveFromClassList(ThreeLineClass);

            switch (_variant)
            {
                case ListItemVariant.TwoLine:
                    AddToClassList(TwoLineClass);
                    break;
                case ListItemVariant.ThreeLine:
                    AddToClassList(ThreeLineClass);
                    break;
                default:
                    AddToClassList(OneLineClass);
                    break;
            }
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }
    }
}
