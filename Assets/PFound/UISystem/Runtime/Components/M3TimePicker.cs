using System;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Time Picker — modal clock-face time selector.
    ///
    /// Displays a circular clock face with hour and minute selection.
    /// Built on M3Dialog for modal container behavior.
    ///
    /// Composition:
    ///   M3Dialog — modal backdrop
    ///     VisualElement (displayRow) — current time HH : MM, two clickable segments
    ///     VisualElement (_amPmToggle) — AM/PM buttons
    ///     VisualElement (_clockFace) — vector callback clock face
    ///     VisualElement (_actions) — Cancel + OK buttons
    ///
    /// M3 spec:
    ///   Clock face: circular, ticks at each hour/5-min interval
    ///   Selected: --m3-primary background on selection arm
    ///   Surface: --m3-surface-container-high
    ///
    /// Style scope note — USS rules in time-picker.uss don't reliably reach
    /// descendants of M3Dialog after it's attached to panel.visualTree, so all
    /// layout dimensions and theme colours are pinned inline in C# and refreshed
    /// on every theme change.
    /// </summary>
    public class M3TimePicker : VisualElement
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass       = "m3-time-picker";
        private const string DisplayClass    = "m3-time-picker__display";
        private const string AmPmClass       = "m3-time-picker__ampm";
        private const string AmPmBtnClass    = "m3-time-picker__ampm-btn";
        private const string AmPmActiveCls   = "m3-time-picker__ampm-btn--active";
        private const string ClockClass      = "m3-time-picker__clock";

        private const float ClockSize        = 256f;
        private const float ClockRadius      = 96f;  // background fill
        // TWO concentric rings — separated so the bold labels never touch the
        // small dot ticks. Eyeball-derived: 32dp wrapper at radius 68 leaves a
        // ~3dp label-label gap; ticks at radius 90 leave a ~4dp gap between
        // each label's outer edge and the nearest tick.
        // M3 reference uses a single ring (labels + ticks at ~80% of dial radius).
        // In our smaller 256dp dial, bold labels at fontSize 14 overlap adjacent
        // ticks geometrically (text edge ~10dp from label centre, nearest tick at
        // 8dp away). Dual-ring as a workaround — both pushed near the perimeter
        // so the inner ring doesn't look "swallowed" by the dial.
        private const float LabelRingRadius  = 76f;  // 12 number labels (inner, near perimeter)
        private const float TickRingRadius   = 92f;  // 48 in-between minute ticks (outer)
        private const float SelectRadius     = 20f;  // selection circle radius at LabelRingRadius

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Dialog      _dialog;
        private readonly Label         _hourDisplay;
        private readonly Label         _minuteDisplay;
        private readonly Label         _amBtn;
        private readonly Label         _pmBtn;
        private readonly VisualElement _clockFace;
        private readonly Label[]         _clockNumbers        = new Label[12];
        private readonly VisualElement[] _clockNumberWrappers = new VisualElement[12];

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private TimeSpan _selectedTime;
        private TimeSpan _pendingTime;
        private bool     _selectingMinutes;

        // Theme-routed colours (refreshed on every theme change). The clock
        // face painter reads these directly so MarkDirtyRepaint after a theme
        // change is enough to swap the visuals.
        private Color _cPrimary               = new Color(0.404f, 0.314f, 0.643f);
        private Color _cOnPrimary             = Color.white;
        private Color _cOnSurface             = new Color(0.110f, 0.106f, 0.122f);
        private Color _cOnSurfaceVariant      = new Color(0.290f, 0.275f, 0.306f);
        private Color _cSecondaryContainer = new Color(0.898f, 0.871f, 0.918f);
        private Color _cTertiaryContainer     = new Color(0.929f, 0.875f, 0.961f);
        private Color _cOnTertiaryContainer   = new Color(0.169f, 0.067f, 0.282f);
        private Color _cOutline               = new Color(0.475f, 0.455f, 0.494f);

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the user confirms a time. Arg is the chosen TimeSpan.</summary>
        public event Action<TimeSpan> OnTimeSelected;

        /// <summary>Fired when the dialog is cancelled.</summary>
        public event Action OnCancelled;

        /// <summary>Currently selected time.</summary>
        public TimeSpan Value
        {
            get => _selectedTime;
            set
            {
                _selectedTime = value;
                _pendingTime  = value;
                UpdateDisplay();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3TimePicker()
        {
            AddToClassList(BaseClass);

            _selectedTime = TimeSpan.FromHours(12);
            _pendingTime  = _selectedTime;

            _dialog = new M3Dialog();

            // ── Time display (HH : MM, two clickable segments) ──
            // The display is split into hour and minute segments so the user
            // can click either to switch which one the dial is editing — M3
            // spec behaviour. The currently-active segment is highlighted via
            // ApplyTimeSegmentColour with a tertiary-container background.
            var displayRow = new VisualElement();
            displayRow.AddToClassList(DisplayClass);
            displayRow.style.flexDirection  = FlexDirection.Row;
            displayRow.style.justifyContent = Justify.Center;
            displayRow.style.alignItems     = Align.Center;
            displayRow.style.marginBottom   = 12f;
            displayRow.style.alignSelf      = Align.Center;

            _hourDisplay = new M3Label();
            ApplyTimeSegmentStyle(_hourDisplay);
            _hourDisplay.RegisterCallback<ClickEvent>(_ =>
            {
                _selectingMinutes = false;
                UpdateDisplay();
            });

            var colonLabel = new M3Label(":");
            colonLabel.style.fontSize          = 57f;
            colonLabel.style.unityTextAlign    = TextAnchor.MiddleCenter;
            colonLabel.style.marginLeft        = 4f;
            colonLabel.style.marginRight       = 4f;
            colonLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

            _minuteDisplay = new M3Label();
            ApplyTimeSegmentStyle(_minuteDisplay);
            _minuteDisplay.RegisterCallback<ClickEvent>(_ =>
            {
                _selectingMinutes = true;
                UpdateDisplay();
            });

            displayRow.Add(_hourDisplay);
            displayRow.Add(colonLabel);
            displayRow.Add(_minuteDisplay);

            // ── AM/PM toggle (segmented, 80×40, outlined) ──
            var amPm = new VisualElement();
            amPm.AddToClassList(AmPmClass);
            amPm.style.flexDirection  = FlexDirection.Row;
            amPm.style.justifyContent = Justify.Center;
            amPm.style.marginBottom   = 16f;
            amPm.style.width          = 80f;
            amPm.style.alignSelf      = Align.Center;
            amPm.style.overflow       = Overflow.Hidden;
            amPm.style.borderTopLeftRadius     = 4f;
            amPm.style.borderTopRightRadius    = 4f;
            amPm.style.borderBottomLeftRadius  = 4f;
            amPm.style.borderBottomRightRadius = 4f;
            amPm.style.borderTopWidth = amPm.style.borderBottomWidth =
                amPm.style.borderLeftWidth = amPm.style.borderRightWidth = 1f;

            _amBtn = new M3Label("AM");
            _amBtn.AddToClassList(AmPmBtnClass);
            ApplyAmPmBtnStyle(_amBtn);
            _amBtn.RegisterCallback<ClickEvent>(_ =>
            {
                if (_pendingTime.Hours >= 12)
                {
                    int h = _pendingTime.Hours - 12;
                    _pendingTime = new TimeSpan(h, _pendingTime.Minutes, _pendingTime.Seconds);
                }
                UpdateDisplay();
            });

            _pmBtn = new M3Label("PM");
            _pmBtn.AddToClassList(AmPmBtnClass);
            ApplyAmPmBtnStyle(_pmBtn);
            _pmBtn.RegisterCallback<ClickEvent>(_ =>
            {
                if (_pendingTime.Hours < 12)
                {
                    int h = _pendingTime.Hours + 12;
                    _pendingTime = new TimeSpan(h, _pendingTime.Minutes, _pendingTime.Seconds);
                }
                UpdateDisplay();
            });

            amPm.Add(_amBtn);
            amPm.Add(_pmBtn);

            // ── Clock face (256×256 painter2D) ──
            _clockFace = new VisualElement();
            _clockFace.AddToClassList(ClockClass);
            _clockFace.style.width      = ClockSize;
            _clockFace.style.height     = ClockSize;
            _clockFace.style.alignSelf  = Align.Center;
            _clockFace.style.marginBottom = 8f;
            // Explicit Position.Relative anchors any descendant with
            // position:absolute (the 12 number labels) to THIS element's box,
            // not the next positioned ancestor up the tree. Without this,
            // Unity 6 UI Toolkit can resolve the labels against M3Dialog._scrim
            // (which is Position.Absolute on the full panel), placing the
            // labels far outside the actual clock face.
            _clockFace.style.position = Position.Relative;
            // CRITICAL: M3Surface (M3Dialog._card) propagates its SDF shape
            // material to every descendant, and painter2D output gets filtered
            // through that shader → invisible. M3Label resets unityMaterial for
            // the same reason. Without this line the background circle, ticks,
            // selection arm and center dot all silently disappear, leaving the
            // dial as just floating hour numbers with no feedback at all.
            _clockFace.style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
            _clockFace.generateVisualContent += DrawClockFace;
            // Pointer-down + capture + move + up gives drag-to-select: the
            // selection arm and ring highlight follow the cursor in real time,
            // serving as the snap indicator. A click without drag still works
            // because Down fires UpdateValueFromPointer once.
            _clockFace.RegisterCallback<PointerDownEvent>(OnClockPointerDown);
            _clockFace.RegisterCallback<PointerMoveEvent>(OnClockPointerMove);
            _clockFace.RegisterCallback<PointerUpEvent>(OnClockPointerUp);
            // generateVisualContent only fires once at attach time before
            // layout is resolved, so the painter sees layout.width == 0
            // and bails out via the `w < 1` guard. After layout settles
            // nothing re-marks the element dirty, leaving the clock face
            // blank. Mark dirty AND reposition number labels on every
            // GeometryChanged.
            _clockFace.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                _clockFace.MarkDirtyRepaint();
                PositionClockNumbers();
            });

            // Hour numbers (12, 1, 2, …, 11) — painter2D can't draw text via
            // the vector callback, so each number is rendered as a flex-centred
            // M3Label inside an absolutely-positioned wrapper. The wrapper is
            // what gets positioned around the dial in PositionClockNumbers
            // (called from GeometryChangedEvent so it re-runs whenever layout
            // resolves or resizes).
            //
            // The wrapper exists so flex (justifyContent + alignItems = Center)
            // can centre the label naturally. Relying on `unityTextAlign =
            // MiddleCenter` on a fixed-size Label has a Unity 6 quirk: text
            // can sit visually off-centre inside the rect (typically biased
            // radially outward), which makes the number appear to drift inside
            // the selection circle. Wrapper-flex sidesteps that.
            for (int i = 0; i < 12; i++)
            {
                int display = i == 0 ? 12 : i;

                var wrapper = new VisualElement();
                wrapper.style.position        = Position.Absolute;
                wrapper.style.width           = 28f;
                wrapper.style.height          = 28f;
                wrapper.style.justifyContent  = Justify.Center;
                wrapper.style.alignItems      = Align.Center;
                wrapper.pickingMode           = PickingMode.Ignore;

                var num = new M3Label(display.ToString());
                num.AddToClassList("m3-label-large");
                num.style.fontSize                = 14f;
                num.style.unityFontStyleAndWeight = FontStyle.Bold; // user-requested boldface — pops the labels above the dot ticks
                num.style.whiteSpace              = WhiteSpace.NoWrap;
                num.pickingMode                   = PickingMode.Ignore;

                wrapper.Add(num);
                _clockNumbers[i]        = num;
                _clockNumberWrappers[i] = wrapper;
                _clockFace.Add(wrapper);
            }

            // Reuse M3Dialog's built-in Cancel / OK action row. OnDismiss
            // and OnConfirm fire when the user clicks them; the dialog
            // auto-closes after either event.
            _dialog.OnDismiss += () =>
            {
                _pendingTime = _selectedTime;
                OnCancelled?.Invoke();
            };
            _dialog.OnConfirm += () =>
            {
                _selectedTime = _pendingTime;
                OnTimeSelected?.Invoke(_selectedTime);
            };

            // Assemble — AddContent inserts each child before the dialog's
            // built-in action row, so display / amPm / clock sit in the body
            // and Cancel / OK render below them.
            _dialog.AddContent(displayRow);
            _dialog.AddContent(amPm);
            _dialog.AddContent(_clockFace);
            Add(_dialog);

            // Theme routing — subscribed on M3Dialog.Scrim, NOT on _dialog itself
            // or `this`. M3Dialog.Show() reparents Scrim to the panel root —
            // that is the only element in this chain that ever fires
            // AttachToPanelEvent. Subscribing anywhere else leaves every label
            // / button stuck at its constructor-default colour, so themes
            // (esp. dark) never apply to the picker labels.
            _dialog.Scrim.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged += OnTpThemeChanged;
                RefreshTpTheme();
            });
            _dialog.Scrim.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ThemeManager.OnThemeChanged -= OnTpThemeChanged;
            });

            // Also refresh visuals once at end of construction so a picker
            // shown before any theme attach still renders with whatever
            // theme is currently active (RefreshTpTheme handles null gracefully).
            UpdateDisplay();
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Shows the time picker as a child of the given parent element.</summary>
        public void Show(VisualElement parent)
        {
            _pendingTime      = _selectedTime;
            _selectingMinutes = false;
            UpdateDisplay();
            _dialog.Show(parent);
        }

        /// <summary>Hides the time picker.</summary>
        public void Hide() => _dialog.Close();

        // ------------------------------------------------------------------ //
        //  Theme routing                                                       //
        // ------------------------------------------------------------------ //

        private void OnTpThemeChanged(ThemeData _) => RefreshTpTheme();

        private void RefreshTpTheme()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _cPrimary             = theme.GetColor(ColorRole.Primary);
            _cOnPrimary           = theme.GetColor(ColorRole.OnPrimary);
            _cOnSurface           = theme.GetColor(ColorRole.OnSurface);
            _cOnSurfaceVariant    = theme.GetColor(ColorRole.OnSurfaceVariant);
            _cSecondaryContainer  = theme.GetColor(ColorRole.SecondaryContainer);
            _cTertiaryContainer   = theme.GetColor(ColorRole.TertiaryContainer);
            _cOnTertiaryContainer = theme.GetColor(ColorRole.OnTertiaryContainer);
            _cOutline             = theme.GetColor(ColorRole.Outline);

            // AM/PM border + button colours refresh on every UpdateDisplay
            // (because active state can change); just push the outline here.
            var amPmContainer = _amBtn.parent;
            if (amPmContainer != null)
            {
                amPmContainer.style.borderTopColor = amPmContainer.style.borderBottomColor =
                    amPmContainer.style.borderLeftColor = amPmContainer.style.borderRightColor = _cOutline;
            }

            UpdateDisplay();
        }

        // ------------------------------------------------------------------ //
        //  Internal                                                            //
        // ------------------------------------------------------------------ //

        private void UpdateDisplay()
        {
            int hours   = _pendingTime.Hours % 12;
            if (hours == 0) hours = 12;
            int minutes = _pendingTime.Minutes;
            _hourDisplay.text   = hours.ToString("D2");
            _minuteDisplay.text = minutes.ToString("D2");

            // Active segment (hour vs minute) — tertiary-container bg + on-tertiary-container text.
            ApplyTimeSegmentColour(_hourDisplay,   !_selectingMinutes);
            ApplyTimeSegmentColour(_minuteDisplay,  _selectingMinutes);

            bool isPm = _pendingTime.Hours >= 12;
            _amBtn.EnableInClassList(AmPmActiveCls, !isPm);
            _pmBtn.EnableInClassList(AmPmActiveCls, isPm);

            // Inline-style the active/inactive AM/PM buttons since USS won't reach.
            ApplyAmPmActiveColour(_amBtn, !isPm);
            ApplyAmPmActiveColour(_pmBtn, isPm);

            UpdateClockNumberLabels();
            RefreshClockNumberColours();
            _clockFace.MarkDirtyRepaint();
        }

        // In hour mode the 12 ring labels read 12, 1, 2, …, 11 (slot 0 = 12).
        // In minute mode they MUST read 00, 05, 10, …, 55 — otherwise the click
        // handler converts the angle to minutes 0–59 while the label still says
        // "11", and the visual disagrees with the value the user just picked.
        private void UpdateClockNumberLabels()
        {
            for (int i = 0; i < 12; i++)
            {
                if (_selectingMinutes)
                {
                    int m = i * 5; // 0, 5, 10, … 55
                    _clockNumbers[i].text = m.ToString("D2");
                }
                else
                {
                    int h = i == 0 ? 12 : i; // 12, 1, 2, … 11
                    _clockNumbers[i].text = h.ToString();
                }
            }
        }

        private void ApplyTimeSegmentColour(Label seg, bool active)
        {
            if (active)
            {
                // Active segment — tertiary-container bg + on-tertiary-container text.
                seg.style.backgroundColor = _cTertiaryContainer;
                seg.style.color           = _cOnTertiaryContainer;
            }
            else
            {
                // Inactive segment — transparent background so it blends with the
                // card surface; only the rounded radius hints that it's clickable.
                seg.style.backgroundColor = StyleKeyword.Initial;
                seg.style.color           = _cOnSurface;
            }
        }

        private void PositionClockNumbers()
        {
            // Use the constant ClockSize, not layout.width — the labels are
            // position:absolute inside _clockFace (Position.Relative), and the
            // anchor box is always the full ClockSize × ClockSize area we
            // pinned at construction. Reading layout.width would also work
            // but couples this code to the layout pass having resolved by the
            // time GeometryChangedEvent fires.
            const float cx = ClockSize / 2f;
            const float cy = ClockSize / 2f;
            for (int i = 0; i < 12; i++)
            {
                float a   = (i / 12f) * 360f - 90f;
                float rad = a * Mathf.Deg2Rad;
                float x   = cx + Mathf.Cos(rad) * LabelRingRadius - 14f; // 28dp wrapper, centre offset
                float y   = cy + Mathf.Sin(rad) * LabelRingRadius - 14f;
                _clockNumberWrappers[i].style.left = x;
                _clockNumberWrappers[i].style.top  = y;
            }
        }

        private void RefreshClockNumberColours()
        {
            // Hour mode: activeIndex = pending.Hours % 12 (slot 0 represents "12").
            // Minute mode: activeIndex = pending.Minutes / 5; but only highlight
            // when the minute lands exactly on a 5-step (otherwise no ring label
            // matches the actual minute value and forcing one would lie).
            int activeIndex;
            bool hasActive;
            if (_selectingMinutes)
            {
                activeIndex = (_pendingTime.Minutes / 5) % 12;
                hasActive   = _pendingTime.Minutes % 5 == 0;
            }
            else
            {
                activeIndex = _pendingTime.Hours % 12;
                hasActive   = true;
            }

            for (int i = 0; i < 12; i++)
            {
                bool active = hasActive && i == activeIndex;
                _clockNumbers[i].style.color = active ? _cOnPrimary : _cOnSurface;
            }
        }

        private void ApplyAmPmActiveColour(Label btn, bool active)
        {
            if (active)
            {
                btn.style.backgroundColor = _cTertiaryContainer;
                btn.style.color           = _cOnTertiaryContainer;
            }
            else
            {
                btn.style.backgroundColor = StyleKeyword.Initial;
                btn.style.color           = _cOnSurfaceVariant;
            }
        }

        private void DrawClockFace(MeshGenerationContext ctx)
        {
            float w  = _clockFace.layout.width;
            float h  = _clockFace.layout.height;
            if (w < 1f || h < 1f) return;

            var p = ctx.painter2D;
            // MUST match PositionClockNumbers: use the constant ClockSize, NOT
            // layout.width. Unity 6 sometimes resolves a flex child to a few
            // dp short of its style.width — pinning the painter to constants
            // keeps the selection circle exactly under the label rect; reading
            // layout.width here would put the circle 1-2 dp off-axis from the
            // text and the user sees the number sitting on the edge of the
            // circle instead of dead-centre.
            const float cx = ClockSize / 2f;
            const float cy = ClockSize / 2f;

            // ── Background circle ──
            p.fillColor   = _cSecondaryContainer;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), ClockRadius, 0f, 360f);
            p.Fill();

            int value = _selectingMinutes ? _pendingTime.Minutes : (_pendingTime.Hours % 12);
            int steps = _selectingMinutes ? 60 : 12;

            // ── Selection arm + indicator ──
            // Arm length depends on what sits at the value's angle:
            //   • Label slot (hour mode OR minute % 5 == 0) → arm to
            //     LabelRingRadius, BIG selection circle (SelectRadius) wraps
            //     the label, label text repainted OnPrimary (white-on-purple).
            //   • In-between minute → arm to TickRingRadius, SMALL primary
            //     dot at the tip, no label colour change.
            // Two-ring layout (labels inner / ticks outer) eliminates the
            // "label sits on top of a tick" visual clash from the single-ring
            // attempt.
            bool selectionOnLabel = !_selectingMinutes || (value % 5 == 0);
            float armRad          = selectionOnLabel ? LabelRingRadius : TickRingRadius;
            float indicatorRadius = selectionOnLabel ? SelectRadius    : 5f;

            float angle = (value / (float)steps) * 360f - 90f;
            float selX  = cx + Mathf.Cos(angle * Mathf.Deg2Rad) * armRad;
            float selY  = cy + Mathf.Sin(angle * Mathf.Deg2Rad) * armRad;

            p.strokeColor = _cPrimary;
            p.lineWidth   = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy));
            p.LineTo(new Vector2(selX, selY));
            p.Stroke();

            // Center dot
            p.fillColor = _cPrimary;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), 4f, 0f, 360f);
            p.Fill();

            // Selection indicator
            p.fillColor = _cPrimary;
            p.BeginPath();
            p.Arc(new Vector2(selX, selY), indicatorRadius, 0f, 360f);
            p.Fill();

            // ── In-between minute ticks at the outer ring ──
            // Only drawn in MINUTE mode. Skip positions that have a number
            // label (i % 5 == 0) and the active position (covered by the
            // small selection dot). Hour mode draws no ticks at all — the
            // 12 number labels stand alone, matching the M3 reference.
            if (_selectingMinutes)
            {
                for (int i = 0; i < 60; i++)
                {
                    if (i % 5 == 0) continue; // label slot
                    if (i == value) continue; // active position

                    float a   = (i / 60f) * 360f - 90f;
                    float rad = Mathf.Deg2Rad * a;
                    float tx  = cx + Mathf.Cos(rad) * TickRingRadius;
                    float ty  = cy + Mathf.Sin(rad) * TickRingRadius;

                    p.fillColor = _cOnSurfaceVariant; // subordinate to labels
                    p.BeginPath();
                    p.Arc(new Vector2(tx, ty), 1.5f, 0f, 360f);
                    p.Fill();
                }
            }
        }

        private void OnClockPointerDown(PointerDownEvent evt)
        {
            // Capture so subsequent Move events keep arriving even when the
            // cursor leaves the clock face — gives a continuous drag.
            _clockFace.CapturePointer(evt.pointerId);
            UpdateValueFromPointer(evt.localPosition);
        }

        private void OnClockPointerMove(PointerMoveEvent evt)
        {
            if (!_clockFace.HasPointerCapture(evt.pointerId)) return;
            UpdateValueFromPointer(evt.localPosition);
        }

        private void OnClockPointerUp(PointerUpEvent evt)
        {
            if (_clockFace.HasPointerCapture(evt.pointerId))
                _clockFace.ReleasePointer(evt.pointerId);
        }

        // Maps a localPosition on the clock face to the nearest hour/minute
        // slot and writes it into _pendingTime. Snaps to:
        //   hour mode   → 12 slots (30° each)
        //   minute mode → 60 slots (6° each)
        // No auto-advance to minute selection on hour pick — user toggles via
        // the hour / minute segments of the display label. Auto-advance was a
        // one-way trap because there was no way back to hour mode.
        private void UpdateValueFromPointer(Vector3 localPosition)
        {
            float w  = _clockFace.layout.width;
            float h  = _clockFace.layout.height;
            if (w < 1f || h < 1f) return;

            float cx   = w / 2f;
            float cy   = h / 2f;
            float dx   = localPosition.x - cx;
            float dy   = localPosition.y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            // Down ignored outside a generous click radius; during an in-flight
            // drag (HasPointerCapture) we still update, otherwise the arm would
            // freeze when the cursor leaves the face.
            if (dist > ClockRadius * 1.6f) return;

            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90f;
            if (angle < 0) angle += 360f;

            if (_selectingMinutes)
            {
                int minutes = Mathf.RoundToInt(angle / 6f) % 60;
                _pendingTime = new TimeSpan(_pendingTime.Hours, minutes, 0);
            }
            else
            {
                int hour12 = Mathf.RoundToInt(angle / 30f) % 12;
                bool isPm  = _pendingTime.Hours >= 12;
                int actualHour = isPm ? (hour12 + 12) % 24 : hour12;
                if (actualHour == 0 && isPm) actualHour = 12;
                _pendingTime = new TimeSpan(actualHour, _pendingTime.Minutes, 0);
            }

            UpdateDisplay();
        }

        // ------------------------------------------------------------------ //
        //  Inline style helpers                                                //
        // ------------------------------------------------------------------ //

        private static void ApplyAmPmBtnStyle(Label btn)
        {
            btn.style.flexGrow        = 1f;
            btn.style.height          = 40f;
            btn.style.unityTextAlign  = TextAnchor.MiddleCenter;
            btn.style.fontSize        = 14f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        // Time-display segment (hour or minute) — 96×80dp clickable label with
        // a rounded-rect background that's tinted in ApplyTimeSegmentColour
        // based on which segment is currently the active dial editor.
        private static void ApplyTimeSegmentStyle(Label seg)
        {
            seg.style.width                       = 96f;
            seg.style.height                      = 80f;
            seg.style.fontSize                    = 57f;
            seg.style.unityTextAlign              = TextAnchor.MiddleCenter;
            seg.style.unityFontStyleAndWeight     = FontStyle.Normal;
            seg.style.borderTopLeftRadius         = 8f;
            seg.style.borderTopRightRadius        = 8f;
            seg.style.borderBottomLeftRadius      = 8f;
            seg.style.borderBottomRightRadius     = 8f;
        }
    }
}
