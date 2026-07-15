using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal class KuiInputHandler
    {
        public KuiInputState State;
        public bool IsOverOverlay;
        public int ActiveWidgetId;
        public int HotWindowIndex = -1;

        readonly KuiSettings _settings;
        float _lastTapTime;
        int _tapCount;
        float2 _lastTapPos;

        // Tap gesture tracking (persists across frames, drives State.IsTap / PressPosition).
        bool _pressActive;
        float2 _pressPos;
        float _maxTravel;

        public KuiInputHandler(KuiSettings settings)
        {
            _settings = settings;
        }

        public void ReadInput()
        {
            float screenH = Screen.height;
            var mp = KuiInput.PointerPosition;

            State = new KuiInputState
            {
                MousePosition = new float2(mp.x, screenH - mp.y),
                MouseDown = KuiInput.PointerDown,
                MouseHeld = KuiInput.PointerHeld,
                MouseUp = KuiInput.PointerUp,
                ScrollDelta = KuiInput.ScrollY
            };

            if (KuiInput.TouchCount > 0)
            {
                var pos = KuiInput.PrimaryTouchPosition;
                var delta = KuiInput.PrimaryTouchDelta;
                State.TouchPosition = new float2(pos.x, screenH - pos.y);
                // Touch delta uses bottom-left origin too; flip Y so positive
                // delta means "finger moved DOWN the screen" in our top-left coords.
                State.TouchDelta = new float2(delta.x, -delta.y);
                State.TouchPhase = KuiInput.PrimaryTouchPhase;
                State.TouchCount = KuiInput.TouchCount;
            }
            else
            {
                State.TouchCount = 0;
                State.TouchDelta = float2.zero;
            }

            UpdateTapGesture();
        }

        // Shared tap/click gesture model. A tap = press + release with the pointer
        // travelling less than the drag threshold. Press/release EDGES (not the held
        // state) drive it, so: (a) a held pointer yields no tap until release — never a
        // stream of taps mid-hold; (b) a drag that travels past the threshold yields no
        // tap on release, even when the content it dragged over couldn't move. Works for
        // mouse and touch alike — MousePosition / MouseDown / MouseUp mirror the primary
        // touch under both input backends (see KuiInput).
        void UpdateTapGesture()
        {
            if (State.MouseDown)
            {
                _pressActive = true;
                _pressPos = State.MousePosition;
                _maxTravel = 0f;
            }
            else if (_pressActive && State.MouseHeld)
            {
                _maxTravel = math.max(_maxTravel, math.distance(State.MousePosition, _pressPos));
            }

            State.PressPosition = _pressPos;

            if (State.MouseUp)
            {
                _maxTravel = math.max(_maxTravel, math.distance(State.MousePosition, _pressPos));
                State.IsTap = _pressActive && _maxTravel <= KuiDPI.Px(_settings.TapDragThresholdPx);
                _pressActive = false;
            }
        }

        public void DetectToggle(ref bool isVisible, List<KuiWindowState> windows)
        {
            if (KuiInput.GetKeyDown(_settings.ToggleKey) || KuiInput.GetKeyDown(_settings.ToggleKeyAlt))
            {
                isVisible = !isVisible;
                return;
            }

            // Touch / mobile toggle: N completed taps in the top-left ToggleRegionPx
            // square within ToggleTapWindow seconds. Mouse double-click also works, so
            // testers can trigger it from a desktop build without a keyboard.
            //
            // Count the RELEASE edge (State.IsTap), not the press: a held pointer
            // produces no tap until it lifts, so holding in the corner can NEVER flip
            // visibility repeatedly. Both the press and the release must land inside the
            // region so a swipe that starts elsewhere and ends here isn't counted.
            if (!State.IsTap) return;

            float region = KuiDPI.Px(_settings.ToggleRegionPx);
            bool inRegion =
                State.PressPosition.x < region && State.PressPosition.y < region &&
                State.MousePosition.x < region && State.MousePosition.y < region;
            if (!inRegion) return;

            float2 tapPos = State.MousePosition;

            // While open, a tap that lands ON a window is UI interaction (e.g. the scale slider in the
            // top-left Toolbox, which overlaps the toggle region), NOT a toggle gesture — ignore it so
            // the overlay doesn't close itself mid-interaction. Current-frame check (no stale state).
            if (isVisible && IsOverAnyWindow(tapPos, windows)) return;

            float now = Time.unscaledTime;
            if (now - _lastTapTime < _settings.ToggleTapWindow &&
                math.distance(_lastTapPos, tapPos) < region * 0.6f)
            {
                _tapCount++;
                if (_tapCount >= _settings.ToggleTapCount)
                {
                    isVisible = !isVisible;
                    _tapCount = 0;
                }
            }
            else
            {
                _tapCount = 1;
            }
            _lastTapTime = now;
            _lastTapPos = tapPos;
        }

        static bool IsOverAnyWindow(float2 p, List<KuiWindowState> windows)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                var s = windows[i];
                if (!s.IsVisible || s.IsMinimized) continue;
                if (IsPointInRect(p, s.Rect)) return true;
            }
            return false;
        }

        public void UpdateWindowInteraction(List<KuiWindowState> states, KuiSettings settings)
        {
            IsOverOverlay = false;
            HotWindowIndex = -1;

            for (int i = states.Count - 1; i >= 0; i--)
            {
                var s = states[i];
                if (!s.IsVisible || s.IsMinimized) continue;

                if (IsPointInRect(State.MousePosition, s.Rect))
                {
                    IsOverOverlay = true;
                    HotWindowIndex = i;

                    if (State.MouseDown)
                    {
                        int maxZ = 0;
                        for (int j = 0; j < states.Count; j++)
                            if (states[j].ZOrder > maxZ) maxZ = states[j].ZOrder;
                        s.ZOrder = maxZ + 1;
                        states[i] = s;
                    }
                    break;
                }
            }

            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (!s.IsVisible) continue;
                if (s.IsMinimized)
                {
                    // Capture pointer over the bubble so taps don't leak to the game. Dragging the
                    // bubble and restoring it (via its button) are handled in KuiWindowManager.
                    var b = KuiWindowManager.BubbleRect(s);
                    if (State.MousePosition.x >= b.x && State.MousePosition.x <= b.x + b.z
                     && State.MousePosition.y >= b.y && State.MousePosition.y <= b.y + b.w)
                        IsOverOverlay = true;
                }
            }
        }

        static bool IsPointInRect(float2 point, Rect rect)
        {
            return point.x >= rect.x && point.x <= rect.x + rect.width
                && point.y >= rect.y && point.y <= rect.y + rect.height;
        }
    }
}
