using System;
using System.Collections.Generic;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Segmented Button — a horizontal group of 2-5 toggleable segments.
    ///
    /// Composition:
    ///   VisualElement (this) — pill-shaped group container
    ///   [M3SegmentedItem children] — added via AddSegment
    ///
    /// Modes:
    ///   SingleSelect: only one segment active at a time
    ///   MultiSelect: any combination may be active
    ///
    /// M3 spec:
    ///   Height: 40dp, full corner radius
    ///   Active segment: --m3-secondary-container + checkmark
    ///   Borders: 1dp --m3-outline between segments
    ///
    /// USS: segmented-button.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3SegmentedButton : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass        = "m3-segmented-button";
        private const string SingleSelectClass = "m3-segmented-button--single";
        private const string MultiSelectClass  = "m3-segmented-button--multi";

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private SelectionMode            _mode = SelectionMode.SingleSelect;
        private readonly List<M3SegmentedItem> _segments = new();

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum SelectionMode { SingleSelect, MultiSelect }

        /// <summary>Fired when selection changes. For multi: called once per toggled segment.</summary>
        public event Action<int, bool> OnSelectionChanged;

        /// <summary>Single or multi-select mode.</summary>
        [UxmlAttribute("mode")]
        public SelectionMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                ApplyMode();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3SegmentedButton()
        {
            AddToClassList(BaseClass);

            // Critical layout — inline to guarantee application
            style.flexDirection = FlexDirection.Row;
            style.height = 40;
            style.borderTopLeftRadius = 20;
            style.borderTopRightRadius = 20;
            style.borderBottomLeftRadius = 20;
            style.borderBottomRightRadius = 20;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.overflow = Overflow.Hidden;

            ApplyMode();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Adds a segment. Returns the created item.</summary>
        public M3SegmentedItem AddSegment(string label, string iconCodepoint = null)
        {
            // Remove --last from previous last segment
            if (_segments.Count > 0)
                _segments[^1].RemoveFromClassList(M3SegmentedItem.LastClass);

            var seg = new M3SegmentedItem
            {
                LabelText = label,
                Icon      = iconCodepoint,
            };
            int index = _segments.Count;
            seg.OnSegmentClicked += s => OnSegmentClicked(index, s);
            seg.AddToClassList(M3SegmentedItem.LastClass); // new last segment
            _segments.Add(seg);
            Add(seg);

            // First segment is active by default in single-select
            if (index == 0 && _mode == SelectionMode.SingleSelect)
                seg.Active = true;

            return seg;
        }

        /// <summary>Selects the segment at index (for single-select) or toggles it (multi).</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= _segments.Count) return;

            if (_mode == SelectionMode.SingleSelect)
            {
                for (int i = 0; i < _segments.Count; i++)
                {
                    bool active = i == index;
                    if (_segments[i].Active != active)
                    {
                        _segments[i].Active = active;
                        OnSelectionChanged?.Invoke(i, active);
                    }
                }
            }
            else
            {
                _segments[index].Active = !_segments[index].Active;
                OnSelectionChanged?.Invoke(index, _segments[index].Active);
            }
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void OnSegmentClicked(int index, M3SegmentedItem _) => Select(index);

        private void ApplyMode()
        {
            if (_mode == SelectionMode.SingleSelect)
            {
                AddToClassList(SingleSelectClass);
                RemoveFromClassList(MultiSelectClass);
            }
            else
            {
                AddToClassList(MultiSelectClass);
                RemoveFromClassList(SingleSelectClass);
            }
        }

        protected override void RefreshThemeColors()
        {
            var theme = Core.ThemeManager.ActiveTheme;
            if (theme == null) return;

            var outline = theme.GetColor(ColorRole.Outline);
            style.borderTopColor = outline;
            style.borderBottomColor = outline;
            style.borderLeftColor = outline;
            style.borderRightColor = outline;

            foreach (var seg in _segments)
            {
                seg.style.borderRightColor = outline;
            }
        }
    }
}
