using Unity.Mathematics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Kunai
{
    /// <summary>
    /// Input backend facade for the debug tool. Reads pointer / scroll / touch / keyboard through
    /// whichever input backend the host project enabled — the new Input System when
    /// <c>ENABLE_INPUT_SYSTEM</c> is defined (Active Input Handling = "Input System Package" or "Both"),
    /// otherwise the legacy <c>UnityEngine.Input</c> manager. Kunai code never touches
    /// <c>UnityEngine.Input</c> directly so the overlay works under either setting (Constitution: the
    /// tool must run in any project regardless of its input configuration).
    ///
    /// All screen coordinates returned here use Unity's native bottom-left origin (same as the legacy
    /// <c>Input.mousePosition</c>); callers flip Y themselves where they need top-left coords.
    /// </summary>
    internal static class KuiInput
    {
        // ---- Pointer (mouse OR primary touch — matches legacy behavior where mouse APIs mirrored touch) ----

        public static float2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var p = Pointer.current;
                if (p == null) return float2.zero;
                var v = p.position.ReadValue();
                return new float2(v.x, v.y);
#else
                var v = Input.mousePosition;
                return new float2(v.x, v.y);
#endif
            }
        }

        public static bool PointerDown
        {
#if ENABLE_INPUT_SYSTEM
            get => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#else
            get => Input.GetMouseButtonDown(0);
#endif
        }

        public static bool PointerHeld
        {
#if ENABLE_INPUT_SYSTEM
            get => Pointer.current != null && Pointer.current.press.isPressed;
#else
            get => Input.GetMouseButton(0);
#endif
        }

        public static bool PointerUp
        {
#if ENABLE_INPUT_SYSTEM
            get => Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
#else
            get => Input.GetMouseButtonUp(0);
#endif
        }

        public static float ScrollY
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                // New Input System reports ~120 units per wheel notch; legacy reported ~1. Normalize so
                // scroll-driven UI keeps the legacy feel.
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        // ---- Explicit touch channel (primary touch only — Kunai uses touch[0]) ----

        public static int TouchCount
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var ts = Touchscreen.current;
                if (ts == null) return 0;
                int count = 0;
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var phase = touches[i].phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                        phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                        phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                        count++;
                }
                return count;
#else
                return Input.touchCount;
#endif
            }
        }

        public static float2 PrimaryTouchPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var ts = Touchscreen.current;
                if (ts == null) return float2.zero;
                var v = ts.primaryTouch.position.ReadValue();
                return new float2(v.x, v.y);
#else
                var v = Input.GetTouch(0).position;
                return new float2(v.x, v.y);
#endif
            }
        }

        public static float2 PrimaryTouchDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var ts = Touchscreen.current;
                if (ts == null) return float2.zero;
                var v = ts.primaryTouch.delta.ReadValue();
                return new float2(v.x, v.y);
#else
                var v = Input.GetTouch(0).deltaPosition;
                return new float2(v.x, v.y);
#endif
            }
        }

        /// <summary>Primary touch phase as a legacy <see cref="UnityEngine.TouchPhase"/> int (Began=0…).</summary>
        public static int PrimaryTouchPhase
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var ts = Touchscreen.current;
                if (ts == null) return (int)UnityEngine.TouchPhase.Canceled;
                return (int)ToLegacyPhase(ts.primaryTouch.phase.ReadValue());
#else
                return (int)Input.GetTouch(0).phase;
#endif
            }
        }

        // ---- Text input (per-frame typed characters, legacy Input.inputString equivalent) ----

        /// <summary>
        /// Returns the characters typed since the last call and clears the internal buffer (consume
        /// semantics — call once per frame). Mirrors legacy <c>Input.inputString</c>; under the new
        /// Input System it drains the <c>Keyboard.onTextInput</c> stream.
        /// </summary>
        public static string ConsumeTypedString()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureTextSubscription();
            if (_typed.Length == 0) return string.Empty;
            var s = _typed.ToString();
            _typed.Clear();
            return s;
#else
            return Input.inputString;
#endif
        }

        // ---- Keyboard ----

        public static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = ToKey(key);
            return k != Key.None && Keyboard.current != null && Keyboard.current[k].isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = ToKey(key);
            return k != Key.None && Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = ToKey(key);
            return k != Key.None && Keyboard.current != null && Keyboard.current[k].wasReleasedThisFrame;
#else
            return Input.GetKeyUp(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static readonly System.Text.StringBuilder _typed = new System.Text.StringBuilder();
        static Keyboard _subscribedKeyboard;

        // Re-targets the onTextInput subscription when the active keyboard changes (device hot-swap,
        // first access). Idempotent — safe to call every frame.
        static void EnsureTextSubscription()
        {
            var kb = Keyboard.current;
            if (kb == _subscribedKeyboard) return;
            if (_subscribedKeyboard != null) _subscribedKeyboard.onTextInput -= OnTextInput;
            _subscribedKeyboard = kb;
            if (kb != null) kb.onTextInput += OnTextInput;
        }

        static void OnTextInput(char c) => _typed.Append(c);

        static UnityEngine.TouchPhase ToLegacyPhase(UnityEngine.InputSystem.TouchPhase p)
        {
            switch (p)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:      return UnityEngine.TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved:      return UnityEngine.TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Stationary: return UnityEngine.TouchPhase.Stationary;
                case UnityEngine.InputSystem.TouchPhase.Ended:      return UnityEngine.TouchPhase.Ended;
                default:                                            return UnityEngine.TouchPhase.Canceled;
            }
        }

        // Maps the KeyCodes Kunai actually uses (toggle keys + REPL/editing keys) to the new
        // Input System Key enum. Unmapped codes return Key.None (treated as "not pressed").
        static Key ToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.BackQuote:    return Key.Backquote;
                case KeyCode.F1:           return Key.F1;
                case KeyCode.F2:           return Key.F2;
                case KeyCode.Escape:       return Key.Escape;
                case KeyCode.Tab:          return Key.Tab;
                case KeyCode.Space:        return Key.Space;
                case KeyCode.Return:       return Key.Enter;
                case KeyCode.KeypadEnter:  return Key.NumpadEnter;
                case KeyCode.Backspace:    return Key.Backspace;
                case KeyCode.Home:         return Key.Home;
                case KeyCode.End:          return Key.End;
                case KeyCode.UpArrow:      return Key.UpArrow;
                case KeyCode.DownArrow:    return Key.DownArrow;
                case KeyCode.LeftArrow:    return Key.LeftArrow;
                case KeyCode.RightArrow:   return Key.RightArrow;
                case KeyCode.LeftControl:  return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftCommand:  return Key.LeftMeta;
                case KeyCode.RightCommand: return Key.RightMeta;
                case KeyCode.LeftShift:    return Key.LeftShift;
                case KeyCode.RightShift:   return Key.RightShift;
                case KeyCode.LeftAlt:      return Key.LeftAlt;
                case KeyCode.RightAlt:     return Key.RightAlt;
                case KeyCode.C:            return Key.C;
                case KeyCode.P:            return Key.P;
                default:                   return Key.None;
            }
        }
#endif
    }
}
