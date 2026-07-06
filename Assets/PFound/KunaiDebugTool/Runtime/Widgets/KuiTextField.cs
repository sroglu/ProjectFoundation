using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Per-widget state for <see cref="KUI.TextField"/>. Plain value type — no
    /// allocations, no IDisposable. Initialise via <c>default</c> and mutate by
    /// passing <c>ref</c> into <see cref="KUI.TextField"/>.
    /// </summary>
    public struct KuiTextFieldState
    {
        internal const int BufferCapacity = 256;

        // Lazily allocated on first use so a `default(KuiTextFieldState)` field
        // costs zero until the widget actually renders.
        internal char[] Buffer;
        internal int    Length;
        internal int    Cursor;

        // Mobile-only: the soft keyboard handle for the active focus session.
        // Cleared (with Active=false) when focus is lost.
        internal TouchScreenKeyboard Keyboard;

        /// <summary>Snapshot the current text. Allocates one string per call.</summary>
        public string GetText()
        {
            if (Buffer == null || Length == 0) return string.Empty;
            return new string(Buffer, 0, Length);
        }

        public void SetText(string value)
        {
            EnsureBuffer();
            if (string.IsNullOrEmpty(value))
            {
                Length = 0;
                Cursor = 0;
                return;
            }
            int n = value.Length < BufferCapacity ? value.Length : BufferCapacity;
            for (int i = 0; i < n; i++) Buffer[i] = value[i];
            Length = n;
            Cursor = n;
        }

        public void Clear()
        {
            Length = 0;
            Cursor = 0;
        }

        internal void EnsureBuffer()
        {
            if (Buffer == null) Buffer = new char[BufferCapacity];
        }
    }

    internal static class KuiTextField
    {
        public const float DefaultHeight = 24f;

        // Fixed monospace approximation. The bundled Nerd Font is roughly square
        // and we don't yet cache per-glyph advances out-of-job. Cursor placement
        // is therefore "close enough" — fine for a debug REPL.
        const float MonoCharWidthRatio = 0.55f;

        public static bool Draw(int widgetId, ref KuiTextFieldState state, KuiContext ctx, float width = 0f)
        {
            state.EnsureBuffer();

            float h = KuiDPI.Px(DefaultHeight);
            float4 rect = width > 0f
                ? ctx.Layout.NextRect(KuiDPI.Px(width), h)
                : ctx.Layout.NextRect(h);

            float4 clip  = ctx.Layout.CurrentClip;
            var    mouse = ctx.InputHandler.State.MousePosition;

            bool hovered = KuiHit.In(mouse, rect, clip);

            // MouseDown inside the rect acquires focus. Clicks outside any
            // focusable widget are caught at the KuiContext level (T018) and
            // call KuiInputFocus.Clear().
            if (hovered && ctx.InputHandler.State.MouseDown)
            {
                KuiInputFocus.TryAcquire(widgetId);
                OpenMobileKeyboardIfNeeded(ref state);
            }

            bool focused = KuiInputFocus.IsFocused(widgetId);

            // Background — slightly brighter when focused so the user sees what
            // owns the keyboard.
            Color32 bg = focused ? KuiStyles.Button : KuiStyles.SliderTrack;
            ctx.CommandBuffer.PushRect(rect, bg, clip);

            bool enterPressed = false;
            if (focused)
            {
                enterPressed = HandleKeyboardInput(ref state);
                PumpMobileKeyboard(ref state);
                if (enterPressed) KuiInputFocus.Clear();
            }
            else
            {
                CloseMobileKeyboardIfOpen(ref state);
            }

            float pad   = KuiDPI.Px(KuiStyles.Padding);
            float textX = rect.x + pad;
            float textY = rect.y + KuiDPI.Px(4f);
            float textW = rect.z - pad * 2f;

            if (state.Length > 0)
            {
                ctx.CommandBuffer.PushLabel(state.Buffer, 0, state.Length,
                    new float4(textX, textY, textW, h),
                    KuiStyles.Text, clip);
            }

            // Caret. Drawn only when focused; blink halves frame-time visibility.
            if (focused && (Time.unscaledTime % 1.0f) < 0.5f)
            {
                float charW = KuiDPI.Px(ctx.Settings.BaseFontSize) * MonoCharWidthRatio;
                float cx    = textX + state.Cursor * charW;
                float cw    = math.max(1f, KuiDPI.Px(1.5f));
                ctx.CommandBuffer.PushRect(
                    new float4(cx, rect.y + KuiDPI.Px(3f), cw, h - KuiDPI.Px(6f)),
                    KuiStyles.Text, clip);
            }

            return enterPressed;
        }

        // ---- Desktop input -----------------------------------------------------

        // Returns true exactly on the frame Enter was pressed.
        static bool HandleKeyboardInput(ref KuiTextFieldState state)
        {
            // Mobile soft keyboard owns input on touch devices; ignore Input.inputString
            // there so we don't double-apply chars (the keyboard hook diffs the buffer
            // each frame).
            if (TouchScreenKeyboard.isSupported) return HandleMobileEnter(ref state);

            string typed = KuiInput.ConsumeTypedString();
            if (!string.IsNullOrEmpty(typed))
            {
                for (int i = 0; i < typed.Length; i++)
                {
                    char c = typed[i];
                    switch (c)
                    {
                        case '\b':              // Backspace
                            BackspaceAtCursor(ref state);
                            break;
                        case '\n':
                        case '\r':              // Enter
                            return true;
                        default:
                            if (c < 32) break;  // ignore other control chars
                            InsertAtCursor(ref state, c);
                            break;
                    }
                }
            }

            // Backspace can also arrive via GetKeyDown when Input.inputString is
            // empty (e.g. when the OS suppresses the \b char).
            if (KuiInput.GetKeyDown(KeyCode.Backspace) && string.IsNullOrEmpty(typed))
                BackspaceAtCursor(ref state);

            if (KuiInput.GetKeyDown(KeyCode.LeftArrow)  && state.Cursor > 0)            state.Cursor--;
            if (KuiInput.GetKeyDown(KeyCode.RightArrow) && state.Cursor < state.Length) state.Cursor++;
            if (KuiInput.GetKeyDown(KeyCode.Home))                                       state.Cursor = 0;
            if (KuiInput.GetKeyDown(KeyCode.End))                                        state.Cursor = state.Length;

            // Enter as a hard-key fallback (some IMEs don't return \n in inputString).
            if (KuiInput.GetKeyDown(KeyCode.Return) || KuiInput.GetKeyDown(KeyCode.KeypadEnter))
                return true;

            // Escape clears focus without committing.
            if (KuiInput.GetKeyDown(KeyCode.Escape))
                KuiInputFocus.Clear();

            return false;
        }

        internal static void InsertAtCursor(ref KuiTextFieldState state, char c)
        {
            if (state.Length >= state.Buffer.Length) return;
            for (int i = state.Length; i > state.Cursor; i--) state.Buffer[i] = state.Buffer[i - 1];
            state.Buffer[state.Cursor] = c;
            state.Length++;
            state.Cursor++;
        }

        internal static void BackspaceAtCursor(ref KuiTextFieldState state)
        {
            if (state.Cursor == 0) return;
            for (int i = state.Cursor - 1; i < state.Length - 1; i++) state.Buffer[i] = state.Buffer[i + 1];
            state.Length--;
            state.Cursor--;
        }

        // ---- Mobile soft-keyboard hook (T012) ---------------------------------

        static void OpenMobileKeyboardIfNeeded(ref KuiTextFieldState state)
        {
            if (!TouchScreenKeyboard.isSupported) return;
            if (state.Keyboard != null && state.Keyboard.active) return;

            string seed = state.Length > 0 ? new string(state.Buffer, 0, state.Length) : string.Empty;
            state.Keyboard = TouchScreenKeyboard.Open(seed,
                TouchScreenKeyboardType.Default,
                autocorrection: false,
                multiline:      false,
                secure:         false,
                alert:          false);
        }

        static void CloseMobileKeyboardIfOpen(ref KuiTextFieldState state)
        {
            if (state.Keyboard == null) return;
            if (state.Keyboard.active) state.Keyboard.active = false;
            state.Keyboard = null;
        }

        // Diffs the soft keyboard's text against our buffer so external edits
        // (autocomplete, paste) flow through. Cursor is forced to end of text;
        // cursor positioning while a soft keyboard is open is platform-specific
        // and out of scope.
        static void PumpMobileKeyboard(ref KuiTextFieldState state)
        {
            if (state.Keyboard == null) return;

            string kb = state.Keyboard.text ?? string.Empty;

            int n = kb.Length < state.Buffer.Length ? kb.Length : state.Buffer.Length;
            bool changed = n != state.Length;
            for (int i = 0; i < n && !changed; i++)
                if (state.Buffer[i] != kb[i]) { changed = true; break; }

            if (changed)
            {
                for (int i = 0; i < n; i++) state.Buffer[i] = kb[i];
                state.Length = n;
                state.Cursor = n;
            }

            // Done / Enter on the soft keyboard closes it; treat that as Enter.
            if (state.Keyboard.status == TouchScreenKeyboard.Status.Done)
            {
                state.Keyboard = null;
                KuiInputFocus.Clear();
            }
            else if (state.Keyboard.status == TouchScreenKeyboard.Status.Canceled
                  || state.Keyboard.status == TouchScreenKeyboard.Status.LostFocus)
            {
                state.Keyboard = null;
                KuiInputFocus.Clear();
            }
        }

        // On mobile, Enter is delivered via TouchScreenKeyboard.Status.Done in
        // PumpMobileKeyboard — so HandleKeyboardInput never returns true here
        // unless a hardware Enter key was pressed (e.g. an external keyboard).
        static bool HandleMobileEnter(ref KuiTextFieldState state)
        {
            if (KuiInput.GetKeyDown(KeyCode.Return) || KuiInput.GetKeyDown(KeyCode.KeypadEnter))
                return true;
            return false;
        }
    }
}
