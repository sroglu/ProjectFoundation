using System;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Date Picker — modal calendar dialog.
    ///
    /// Displays a month grid calendar with year/month navigation.
    /// Built on M3Dialog for modal container behavior.
    ///
    /// Composition:
    ///   M3Dialog — modal backdrop and surface
    ///     VisualElement (_header) — month/year label + nav arrows
    ///     VisualElement (_daysHeader) — day-of-week labels (Sun–Sat)
    ///     VisualElement (_calendarGrid) — 6×7 day buttons grid
    ///     VisualElement (_actions) — Cancel + OK buttons
    ///
    /// M3 spec:
    ///   Surface: --m3-surface-container-high
    ///   Selected day: --m3-primary background, --m3-on-primary text
    ///   Today: --m3-primary outline
    ///
    /// Style scope note — like other popped dialogs, USS rules don't reliably
    /// reach descendants of M3Dialog after it's been attached to panel.visualTree.
    /// All layout dimensions and theme colours are pinned inline in C# and
    /// refreshed on theme change, instead of relying on date-picker.uss.
    /// </summary>
    public class M3DatePicker : VisualElement
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass       = "m3-date-picker";
        private const string HeaderClass     = "m3-date-picker__header";
        private const string NavBtnClass     = "m3-date-picker__nav-btn";
        private const string MonthLabelClass = "m3-date-picker__month-label";
        private const string DaysHeaderClass = "m3-date-picker__days-header";
        private const string DayLabelClass   = "m3-date-picker__day-label";
        private const string GridClass       = "m3-date-picker__grid";
        private const string DayBtnClass     = "m3-date-picker__day-btn";
        private const string DayBtnSelectedClass = "m3-date-picker__day-btn--selected";
        private const string DayBtnTodayClass    = "m3-date-picker__day-btn--today";
        private const string DayBtnOtherClass    = "m3-date-picker__day-btn--other-month";
        private const string YearBtnClass        = "m3-date-picker__year-btn";

        private static readonly string[] DayNames = { "S", "M", "T", "W", "T", "F", "S" };

        // Year picker spans 1925 → 2100 (176 entries). 3-column grid inside a
        // 240dp-tall scroll view that replaces the calendar grid when active.
        private const int YearRangeStart = 1925;
        private const int YearRangeEnd   = 2100;
        private const int YearCount      = YearRangeEnd - YearRangeStart + 1;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Dialog      _dialog;
        private readonly Label         _monthLabel;
        private readonly Label         _prevBtn;
        private readonly Label         _nextBtn;
        private readonly Label         _selectedDateCaption; // small "SELECT DATE"
        private readonly Label         _selectedDateDisplay; // headline "Sat, May 17"
        private readonly VisualElement _selectedDateSeparator;
        private readonly VisualElement _daysHeader;
        private readonly VisualElement _calendarGrid;
        private readonly Label[]       _dayButtons     = new Label[42]; // 6 weeks × 7 days
        private readonly Label[]       _dayOfWeekLabels = new Label[7];
        private readonly ScrollView    _yearScroll;
        private readonly VisualElement _yearGrid;
        private readonly Label[]       _yearButtons    = new Label[YearCount];
        private readonly Label         _monthLabelChevron;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private DateTime _displayMonth;
        private DateTime _selectedDate;
        private DateTime _pendingDate;
        private bool     _yearMode; // true → year list visible, calendar hidden

        // Cached theme colours — refreshed on every theme change.
        private Color _cOnSurface        = new Color(0.110f, 0.106f, 0.122f);
        private Color _cOnSurfaceVariant = new Color(0.290f, 0.275f, 0.306f);
        private Color _cPrimary          = new Color(0.404f, 0.314f, 0.643f);
        private Color _cOnPrimary        = Color.white;
        private Color _cOutlineVariant   = new Color(0.769f, 0.780f, 0.773f);

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the user confirms a date. Arg is the chosen DateTime (date only).</summary>
        public event Action<DateTime> OnDateSelected;

        /// <summary>Fired when the dialog is cancelled.</summary>
        public event Action OnCancelled;

        /// <summary>Currently selected date.</summary>
        public DateTime Value
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value.Date;
                _pendingDate  = _selectedDate;
                _displayMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                RefreshCalendar();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3DatePicker()
        {
            AddToClassList(BaseClass);

            var today     = DateTime.Today;
            _selectedDate = today;
            _pendingDate  = today;
            _displayMonth = new DateTime(today.Year, today.Month, 1);

            _dialog = new M3Dialog();

            // ── Content wrapper ──
            // All calendar sections live inside a single 280dp wide wrapper so
            // they share an identical layout box. Children of the wrapper are
            // stretched to its 280dp width by default (flex-column cross-axis
            // stretch), so the day-of-week row and the date grid render with
            // matching 7×40dp columns without needing per-row width pins.
            var content = new VisualElement();
            content.style.width         = 280f;
            content.style.alignSelf     = Align.Center;
            content.style.flexDirection = FlexDirection.Column;

            // ── Selected date display (M3 spec header) ──
            //   "SELECT DATE"  ← small caption, label-small
            //   "Sat, May 17"  ← large date display, headline-medium
            //   ────────────── ← outline-variant separator
            _selectedDateCaption = new M3Label("SELECT DATE");
            _selectedDateCaption.AddToClassList("m3-label-small");
            _selectedDateCaption.style.unityTextAlign = TextAnchor.MiddleLeft;
            _selectedDateCaption.style.fontSize       = 12f;
            _selectedDateCaption.style.marginBottom   = 4f;

            _selectedDateDisplay = new M3Label();
            _selectedDateDisplay.AddToClassList("m3-headline-medium");
            _selectedDateDisplay.style.unityTextAlign = TextAnchor.MiddleLeft;
            _selectedDateDisplay.style.fontSize       = 32f;
            _selectedDateDisplay.style.marginBottom   = 16f;

            _selectedDateSeparator = new VisualElement();
            _selectedDateSeparator.style.height       = 1f;
            _selectedDateSeparator.style.marginBottom = 16f;

            content.Add(_selectedDateCaption);
            content.Add(_selectedDateDisplay);
            content.Add(_selectedDateSeparator);

            // ── Month-nav row (prev | month label | next) ──
            // No explicit width — fills the 280dp wrapper via cross-axis stretch.
            var header = new VisualElement();
            header.AddToClassList(HeaderClass);
            header.style.flexDirection    = FlexDirection.Row;
            header.style.alignItems       = Align.Center;
            header.style.marginBottom     = 12f;

            var monthGroup = new VisualElement();
            monthGroup.style.flexDirection = FlexDirection.Row;
            monthGroup.style.alignItems    = Align.Center;
            monthGroup.style.flexGrow      = 1f;
            monthGroup.RegisterCallback<ClickEvent>(_ => ToggleYearMode());

            _prevBtn = new M3Label("\ue5cb"); // ChevronLeft
            _prevBtn.AddToClassList("m3-icon");
            _prevBtn.AddToClassList(NavBtnClass);
            ApplyNavBtnStyle(_prevBtn);
            M3Label.ApplyMaterialSymbolsFont(_prevBtn);
            _prevBtn.RegisterCallback<ClickEvent>(_ => ChangeMonth(-1));

            _monthLabel = new M3Label();
            _monthLabel.AddToClassList(MonthLabelClass);
            _monthLabel.AddToClassList("m3-title-medium");
            _monthLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            _monthLabelChevron = new M3Label("\ue5c5"); // arrow_drop_down
            _monthLabelChevron.AddToClassList("m3-icon");
            _monthLabelChevron.style.fontSize    = 24f;
            _monthLabelChevron.style.marginLeft  = 4f;
            _monthLabelChevron.pickingMode       = PickingMode.Ignore;
            M3Label.ApplyMaterialSymbolsFont(_monthLabelChevron);

            monthGroup.Add(_monthLabel);
            monthGroup.Add(_monthLabelChevron);

            _nextBtn = new M3Label("\ue5cc"); // ChevronRight
            _nextBtn.AddToClassList("m3-icon");
            _nextBtn.AddToClassList(NavBtnClass);
            ApplyNavBtnStyle(_nextBtn);
            M3Label.ApplyMaterialSymbolsFont(_nextBtn);
            _nextBtn.RegisterCallback<ClickEvent>(_ => ChangeMonth(1));

            header.Add(monthGroup);
            header.Add(_prevBtn);
            header.Add(_nextBtn);

            // ── Day-of-week header (S M T W T F S) ──
            // Fills the 280dp wrapper via cross-axis stretch. Each label uses
            // flexGrow:1 + flexBasis:0 so the seven labels distribute equally
            // across 280dp (= 40dp per cell), matching the day grid below.
            _daysHeader = new VisualElement();
            _daysHeader.AddToClassList(DaysHeaderClass);
            _daysHeader.style.flexDirection = FlexDirection.Row;
            _daysHeader.style.marginBottom  = 4f;
            for (int i = 0; i < 7; i++)
            {
                var lbl = new M3Label(DayNames[i]);
                lbl.AddToClassList(DayLabelClass);
                lbl.AddToClassList("m3-label-medium");
                lbl.style.flexGrow       = 1f;
                lbl.style.flexBasis      = 0f;
                lbl.style.height         = 32f;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                _dayOfWeekLabels[i] = lbl;
                _daysHeader.Add(lbl);
            }

            // ── Calendar grid (6×7 days, wraps) ──
            // Width pinned to 280dp + flexWrap so 7 × 40dp buttons fit per row
            // and wrap to the next.
            _calendarGrid = new VisualElement();
            _calendarGrid.AddToClassList(GridClass);
            _calendarGrid.style.flexDirection = FlexDirection.Row;
            _calendarGrid.style.flexWrap      = Wrap.Wrap;
            _calendarGrid.style.width         = 280f;
            for (int i = 0; i < 42; i++)
            {
                var dayBtn = new M3Label();
                dayBtn.AddToClassList(DayBtnClass);
                dayBtn.AddToClassList("m3-label-large");
                ApplyDayBtnStyle(dayBtn);
                int capture = i;
                dayBtn.RegisterCallback<ClickEvent>(_ => OnDayClicked(capture));
                _dayButtons[i] = dayBtn;
                _calendarGrid.Add(dayBtn);
            }

            // ── Year picker (scrollable 3-column grid, replaces calendar) ──
            // Hidden by default; ToggleYearMode swaps the daysHeader+calendarGrid
            // display for this scroll view. Total height matches the calendar
            // grid so the dialog footprint stays stable when toggling modes.
            _yearScroll = new ScrollView(ScrollViewMode.Vertical);
            _yearScroll.style.width   = 280f;
            _yearScroll.style.height  = 240f;
            _yearScroll.style.display = DisplayStyle.None;

            _yearGrid = new VisualElement();
            _yearGrid.style.flexDirection = FlexDirection.Row;
            _yearGrid.style.flexWrap      = Wrap.Wrap;
            _yearGrid.style.width         = 280f;
            for (int i = 0; i < YearCount; i++)
            {
                int year = YearRangeStart + i;
                var yearBtn = new M3Label(year.ToString());
                yearBtn.AddToClassList(YearBtnClass);
                yearBtn.AddToClassList("m3-label-large");
                ApplyYearBtnStyle(yearBtn);
                int capture = year;
                yearBtn.RegisterCallback<ClickEvent>(_ => OnYearClicked(capture));
                _yearButtons[i] = yearBtn;
                _yearGrid.Add(yearBtn);
            }
            _yearScroll.Add(_yearGrid);

            // Action row is reused from M3Dialog's built-in Cancel / OK buttons —
            // they wire through M3Dialog.OnConfirm / OnDismiss below. The dialog
            // also auto-closes itself when either fires.
            _dialog.OnDismiss += () =>
            {
                _pendingDate = _selectedDate;
                OnCancelled?.Invoke();
            };
            _dialog.OnConfirm += () =>
            {
                _selectedDate = _pendingDate;
                OnDateSelected?.Invoke(_selectedDate);
            };

            // Assemble — pack month nav / days header / calendar grid into the
            // 280dp content wrapper, then hand the wrapper to the dialog. This
            // collapses the dialog into a single 280dp column where all rows
            // share the same width, instead of three independently-stretched
            // children.
            content.Add(header);
            content.Add(_daysHeader);
            content.Add(_calendarGrid);
            content.Add(_yearScroll);

            _dialog.AddContent(content);

            Add(_dialog);

            // Theme routing — subscribed on M3Dialog.Scrim, NOT on _dialog itself
            // or `this`. M3Dialog.Show() reparents Scrim to the panel root —
            // that is the only element in this chain that ever fires
            // AttachToPanelEvent. Subscribing anywhere else leaves every label
            // stuck at its constructor-default colour, so themes (esp. dark)
            // never apply to the picker labels.
            _dialog.Scrim.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged += OnDpThemeChanged;
                RefreshDpTheme();
            });
            _dialog.Scrim.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged -= OnDpThemeChanged;
            });

            RefreshCalendar();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Shows the date picker as a child of the given parent element.</summary>
        public void Show(VisualElement parent)
        {
            _pendingDate = _selectedDate;
            // Always open in calendar mode — Show() may be invoked multiple
            // times on the same instance, and a stale year-mode state would
            // mean the calendar is hidden the moment the dialog reappears.
            if (_yearMode)
            {
                _yearMode = false;
                _daysHeader.style.display   = DisplayStyle.Flex;
                _calendarGrid.style.display = DisplayStyle.Flex;
                _prevBtn.style.display      = DisplayStyle.Flex;
                _nextBtn.style.display      = DisplayStyle.Flex;
                _yearScroll.style.display   = DisplayStyle.None;
                UpdateChevronRotation();
            }
            _dialog.Show(parent);
        }

        /// <summary>Hides the date picker.</summary>
        public void Hide() => _dialog.Close();

        // ------------------------------------------------------------------ //
        //  Theme routing                                                       //
        // ------------------------------------------------------------------ //

        private void OnDpThemeChanged(ThemeData _) => RefreshDpTheme();

        private void RefreshDpTheme()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _cOnSurface        = theme.GetColor(ColorRole.OnSurface);
            _cOnSurfaceVariant = theme.GetColor(ColorRole.OnSurfaceVariant);
            _cPrimary          = theme.GetColor(ColorRole.Primary);
            _cOnPrimary        = theme.GetColor(ColorRole.OnPrimary);
            _cOutlineVariant   = theme.GetColor(ColorRole.OutlineVariant);

            _selectedDateCaption.style.color = _cOnSurfaceVariant;
            _selectedDateDisplay.style.color = _cOnSurface;
            _selectedDateSeparator.style.backgroundColor = _cOutlineVariant;

            _monthLabel.style.color        = _cOnSurface;
            _monthLabelChevron.style.color = _cOnSurfaceVariant;
            _prevBtn.style.color           = _cOnSurfaceVariant;
            _nextBtn.style.color           = _cOnSurfaceVariant;
            for (int i = 0; i < 7; i++)
                _dayOfWeekLabels[i].style.color = _cOnSurfaceVariant;

            RefreshCalendar();    // re-apply per-state colours on day buttons
            RefreshYearList();    // safe to call when year mode is hidden
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void ChangeMonth(int delta)
        {
            _displayMonth = _displayMonth.AddMonths(delta);
            RefreshCalendar();
        }

        // ── Year picker mode ─────────────────────────────────────────────────
        // Clicking the "May 2026 ▾" month-label group toggles between the
        // calendar grid and a scrollable year list (1925 → 2100). Picking a
        // year keeps the same Month, snaps the display to (year, currentMonth, 1)
        // and returns to calendar mode.

        private void ToggleYearMode()
        {
            _yearMode = !_yearMode;

            var calMode  = _yearMode ? DisplayStyle.None : DisplayStyle.Flex;
            var yearOnly = _yearMode ? DisplayStyle.Flex : DisplayStyle.None;
            _daysHeader.style.display   = calMode;
            _calendarGrid.style.display = calMode;
            _prevBtn.style.display      = calMode;
            _nextBtn.style.display      = calMode;
            _yearScroll.style.display   = yearOnly;

            if (_yearMode)
            {
                RefreshYearList();
                ScrollToCurrentYear();
            }
            UpdateChevronRotation();
        }

        private void OnYearClicked(int year)
        {
            _displayMonth = new DateTime(year, _displayMonth.Month, 1);
            _yearMode     = false;
            _daysHeader.style.display   = DisplayStyle.Flex;
            _calendarGrid.style.display = DisplayStyle.Flex;
            _prevBtn.style.display      = DisplayStyle.Flex;
            _nextBtn.style.display      = DisplayStyle.Flex;
            _yearScroll.style.display   = DisplayStyle.None;
            RefreshCalendar();
            UpdateChevronRotation();
        }

        private void RefreshYearList()
        {
            int currentYear = _displayMonth.Year;
            int todayYear   = DateTime.Today.Year;
            for (int i = 0; i < YearCount; i++)
            {
                int y     = YearRangeStart + i;
                var btn   = _yearButtons[i];
                bool isCurrent = y == currentYear;
                bool isToday   = y == todayYear;

                if (isCurrent)
                {
                    btn.style.backgroundColor = _cPrimary;
                    btn.style.color           = _cOnPrimary;
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
                }
                else if (isToday)
                {
                    btn.style.backgroundColor = StyleKeyword.Initial;
                    btn.style.color           = _cPrimary;
                    btn.style.borderTopColor  = btn.style.borderBottomColor =
                        btn.style.borderLeftColor = btn.style.borderRightColor = _cPrimary;
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
                }
                else
                {
                    btn.style.backgroundColor = StyleKeyword.Initial;
                    btn.style.color           = _cOnSurface;
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
                }
            }
        }

        private void ScrollToCurrentYear()
        {
            int idx = _displayMonth.Year - YearRangeStart;
            if (idx < 0) idx = 0;
            int row = idx / 3;
            // Row pitch = year-btn height (36) + vertical margin (4 top + 4 bottom).
            // Offset by ~90 so the current year lands roughly mid-viewport.
            float y = row * 44f - 90f;
            if (y < 0f) y = 0f;
            _yearScroll.scrollOffset = new Vector2(0f, y);
        }

        private void UpdateChevronRotation()
        {
            // arrow_drop_down rotates 180° when year mode is active so it points
            // up — M3 convention for "tap to collapse back to calendar".
            _monthLabelChevron.style.rotate = new Rotate(new Angle(_yearMode ? 180f : 0f, AngleUnit.Degree));
        }

        private void OnDayClicked(int index)
        {
            var date = GetDateForIndex(index);
            if (date == DateTime.MinValue) return;
            _pendingDate = date;
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            // Selected-date display header — e.g. "Sat, May 17".
            _selectedDateDisplay.text = _pendingDate.ToString("ddd, MMM d");

            _monthLabel.text = _displayMonth.ToString("MMMM yyyy");

            var firstDay  = _displayMonth;
            int startDow  = (int)firstDay.DayOfWeek; // 0=Sun
            var today     = DateTime.Today;
            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

            for (int i = 0; i < 42; i++)
            {
                int dayNum = i - startDow + 1;
                var btn    = _dayButtons[i];

                btn.RemoveFromClassList(DayBtnSelectedClass);
                btn.RemoveFromClassList(DayBtnTodayClass);
                btn.RemoveFromClassList(DayBtnOtherClass);

                if (dayNum < 1 || dayNum > daysInMonth)
                {
                    btn.text = string.Empty;
                    btn.AddToClassList(DayBtnOtherClass);
                    // Reset visual state for empty slots so they don't carry
                    // selection colours over from a previous month.
                    btn.style.backgroundColor = StyleKeyword.Initial;
                    btn.style.color           = new StyleColor(new Color(0, 0, 0, 0));
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
                    continue;
                }

                btn.text = dayNum.ToString();

                var thisDate    = new DateTime(_displayMonth.Year, _displayMonth.Month, dayNum);
                bool isSelected = thisDate == _pendingDate.Date;
                bool isToday    = thisDate == today;

                if (isSelected)
                {
                    btn.AddToClassList(DayBtnSelectedClass);
                    btn.style.backgroundColor = _cPrimary;
                    btn.style.color           = _cOnPrimary;
                }
                else
                {
                    btn.style.backgroundColor = StyleKeyword.Initial;
                    btn.style.color           = _cOnSurface;
                }

                if (isToday && !isSelected)
                {
                    btn.AddToClassList(DayBtnTodayClass);
                    btn.style.borderTopColor = btn.style.borderBottomColor =
                        btn.style.borderLeftColor = btn.style.borderRightColor = _cPrimary;
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
                }
                else
                {
                    btn.style.borderTopWidth = btn.style.borderBottomWidth =
                        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
                }
            }
        }

        private DateTime GetDateForIndex(int index)
        {
            int firstDow = (int)_displayMonth.DayOfWeek;
            int dayNum   = index - firstDow + 1;
            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
            if (dayNum < 1 || dayNum > daysInMonth) return DateTime.MinValue;
            return new DateTime(_displayMonth.Year, _displayMonth.Month, dayNum);
        }

        // ------------------------------------------------------------------ //
        //  Inline style helpers                                                //
        // ------------------------------------------------------------------ //

        private static void ApplyNavBtnStyle(Label btn)
        {
            btn.style.fontSize       = 24f;
            btn.style.width          = 40f;
            btn.style.height         = 40f;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.borderTopLeftRadius     = 20f;
            btn.style.borderTopRightRadius    = 20f;
            btn.style.borderBottomLeftRadius  = 20f;
            btn.style.borderBottomRightRadius = 20f;
        }

        private static void ApplyDayBtnStyle(Label btn)
        {
            btn.style.width          = 40f;
            btn.style.height         = 40f;
            btn.style.flexShrink     = 0f; // never shrink — 7 × 40 must fit per row
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.borderTopLeftRadius     = 20f;
            btn.style.borderTopRightRadius    = 20f;
            btn.style.borderBottomLeftRadius  = 20f;
            btn.style.borderBottomRightRadius = 20f;
        }

        // Year-picker cell: 80×36dp pill button. 3 per row inside a 280dp grid
        // with 6dp horizontal margin each side → 3 × (80 + 12) = 276 ≈ 280.
        private static void ApplyYearBtnStyle(Label btn)
        {
            btn.style.width          = 80f;
            btn.style.height         = 36f;
            btn.style.marginLeft     = 6f;
            btn.style.marginRight    = 6f;
            btn.style.marginTop      = 4f;
            btn.style.marginBottom   = 4f;
            btn.style.flexShrink     = 0f;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.borderTopLeftRadius     = 18f;
            btn.style.borderTopRightRadius    = 18f;
            btn.style.borderBottomLeftRadius  = 18f;
            btn.style.borderBottomRightRadius = 18f;
        }
    }
}
