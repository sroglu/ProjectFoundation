using System;
using PFound.UISystem.Core;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Tooltip — informational overlay triggered by hover.
    ///
    /// Variants:
    ///   Plain: text only (body medium, single line)
    ///   Rich: title + body text
    ///
    /// Composition:
    ///   VisualElement (this) — tooltip surface
    ///   Label (_title) — title (Rich variant only)
    ///   Label (_body) — tooltip text
    ///
    /// M3 spec:
    ///   Background: --m3-inverse-surface
    ///   Text: --m3-inverse-on-surface
    ///   Corner radius: 4dp
    ///   Appear after: 500ms hover delay
    ///   Auto-positioned above/below anchor
    ///
    /// Usage:
    ///   var tip = new M3Tooltip { Body = "This action saves your work." };
    ///   tip.Attach(saveButton);
    ///
    /// USS: tooltip.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    public class M3Tooltip : VisualElement
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass     = "m3-tooltip";
        private const string PlainClass    = "m3-tooltip--plain";
        private const string RichClass     = "m3-tooltip--rich";
        private const string TitleClass    = "m3-tooltip__title";
        private const string BodyClass     = "m3-tooltip__body";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly Label _title;
        private readonly Label _body;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private TooltipVariant             _variant = TooltipVariant.Plain;
        private string                     _titleText = string.Empty;
        private string                     _bodyText  = string.Empty;
        private VisualElement              _anchor;
        private IVisualElementScheduledItem _showSchedule;

        private const int HoverDelayMs = 500;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum TooltipVariant { Plain, Rich }

        /// <summary>Plain (text only) or Rich (title + body).</summary>
        public TooltipVariant Variant
        {
            get => _variant;
            set
            {
                _variant = value;
                ApplyVariant();
            }
        }

        /// <summary>Tooltip title text (Rich variant only).</summary>
        public string Title
        {
            get => _titleText;
            set
            {
                _titleText  = value ?? string.Empty;
                _title.text = _titleText;
                _title.style.display = string.IsNullOrEmpty(_titleText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>Tooltip body / plain text.</summary>
        public string Body
        {
            get => _bodyText;
            set
            {
                _bodyText  = value ?? string.Empty;
                _body.text = _bodyText;
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Tooltip()
        {
            AddToClassList(BaseClass);
            style.position = Position.Absolute;
            style.display  = DisplayStyle.None;
            pickingMode    = PickingMode.Ignore;

            _title = new M3Label();
            _title.AddToClassList(TitleClass);
            _title.AddToClassList("m3-title-small");
            _title.style.display = DisplayStyle.None;

            _body = new M3Label();
            _body.AddToClassList(BodyClass);
            _body.AddToClassList("m3-body-medium");

            Add(_title);
            Add(_body);

            ApplyVariant();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Attaches this tooltip to the given anchor element.
        /// The tooltip shows after 500ms hover and hides on mouse leave.
        /// </summary>
        public void Attach(VisualElement anchor)
        {
            Detach();
            _anchor = anchor;
            _anchor.RegisterCallback<MouseEnterEvent>(OnAnchorEnter);
            _anchor.RegisterCallback<MouseLeaveEvent>(OnAnchorLeave);
        }

        /// <summary>Detaches the tooltip from its current anchor.</summary>
        public void Detach()
        {
            if (_anchor == null) return;
            _anchor.UnregisterCallback<MouseEnterEvent>(OnAnchorEnter);
            _anchor.UnregisterCallback<MouseLeaveEvent>(OnAnchorLeave);
            _anchor = null;
            Hide();
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void OnAnchorEnter(MouseEnterEvent _)
        {
            _showSchedule?.Pause();
            _showSchedule = _anchor?.schedule.Execute(Show).StartingIn(HoverDelayMs);
        }

        private void OnAnchorLeave(MouseLeaveEvent _)
        {
            _showSchedule?.Pause();
            _showSchedule = null;
            Hide();
        }

        private const float AnchorGap = 4f; // M3 spec: 4dp gap between anchor and tooltip

        private void Show()
        {
            if (_anchor == null) return;

            var root = _anchor.panel?.visualTree;
            if (root == null) return;

            // Add to root overlay so tooltip is on top
            if (parent != root) root.Add(this);

            style.display = DisplayStyle.Flex;

            // Provisional bottom-anchored position above the anchor. Real
            // measurement happens next tick — the tooltip just got added to
            // the hierarchy, so its height isn't resolved this frame.
            PositionRelativeToAnchor();
            schedule.Execute(PositionRelativeToAnchor).StartingIn(0);
        }

        /// <summary>
        /// Default M3 placement is above the anchor with the tooltip BOTTOM
        /// fixed to (anchor.top - 4dp). Using style.bottom (not style.top)
        /// keeps the tooltip bottom-anchored so as its body text grows the
        /// tooltip expands upward, leaving the gap to the anchor constant.
        /// If there is no room above, fall back to below the anchor (using
        /// style.top, top-anchored — text growth pushes the tooltip down).
        /// </summary>
        private void PositionRelativeToAnchor()
        {
            if (_anchor == null || panel == null) return;
            var root = panel.visualTree;
            var anchorBounds = _anchor.worldBound;
            float panelHeight = root.layout.height;

            float tooltipHeight = resolvedStyle.height > 0
                ? resolvedStyle.height
                : layout.height;

            // Try above: tooltip bottom edge sits AnchorGap above anchor.y.
            // Distance from panel bottom = panelHeight - anchor.y + AnchorGap.
            float aboveBottomOffset = panelHeight - anchorBounds.y + AnchorGap;
            bool fitsAbove = anchorBounds.y - tooltipHeight - AnchorGap >= 0f;

            style.left = anchorBounds.x;
            if (fitsAbove)
            {
                // Bottom-anchored: top grows upward with text.
                style.top    = StyleKeyword.Auto;
                style.bottom = aboveBottomOffset;
            }
            else
            {
                // Fallback below: top-anchored (text growth pushes down).
                style.bottom = StyleKeyword.Auto;
                style.top    = anchorBounds.yMax + AnchorGap;
            }
        }

        private void Hide()
        {
            style.display = DisplayStyle.None;
            RemoveFromHierarchy();
        }

        private void ApplyVariant()
        {
            if (_variant == TooltipVariant.Plain)
            {
                AddToClassList(PlainClass);
                RemoveFromClassList(RichClass);
            }
            else
            {
                AddToClassList(RichClass);
                RemoveFromClassList(PlainClass);
            }
        }
    }
}
