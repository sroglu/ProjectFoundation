using System;
using UnityEngine;

namespace Kunai
{
    public static class KUI
    {
        public static KuiSettings Settings => KuiContext.Instance?.Settings;

        public static void Initialize(Texture2D fontAtlas, TextAsset fontMetrics)
        {
            KuiContext.Create(fontAtlas, fontMetrics);
            KuiDPI.UpdateScale(KuiContext.Instance.Settings);
        }

        public static void Shutdown()
        {
            KuiContext.Destroy();
        }

        public static bool IsVisible
        {
            get => KuiContext.Instance?.IsVisible ?? false;
            set { if (KuiContext.Instance != null) KuiContext.Instance.IsVisible = value; }
        }

        /// <summary>
        /// Wall-clock duration of the most recent KuiContext.Tick in nanoseconds.
        /// Useful for `&lt;1ms CPU` budget validation. 0 if Kunai has never ticked.
        /// </summary>
        public static long LastTickDurationNs => KuiContext.LastTickDurationNs;

        public static void RegisterWindow(KuWindow window)
        {
            KuiContext.Instance?.RegisterWindow(window);
        }

        public static void UnregisterWindow(KuWindow window)
        {
            KuiContext.Instance?.UnregisterWindow(window);
        }

        // --- Widgets (only valid inside KuWindow.OnRenderUI) ---

        public static void Label(string text)
        {
            Label(text, KuiStyles.Text);
        }

        public static void Label(string text, Color32 color)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float height = KuiDPI.Px(ctx.Settings.BaseFontSize + 4f);
            var rect = ctx.Layout.NextRect(height);
            ctx.CommandBuffer.PushLabel(text, rect, color, ctx.Layout.CurrentClip);
        }

        /// <summary>
        /// Zero-allocation label: renders the first <paramref name="count"/> chars of a
        /// caller-owned <c>char[]</c> (e.g. one assembled with <see cref="KuiTextBuilder"/>)
        /// straight through the glyph path, never creating a string. Use for readouts that
        /// refresh every frame/tick.
        /// </summary>
        public static void Label(char[] text, int count)
        {
            Label(text, count, KuiStyles.Text);
        }

        public static void Label(char[] text, int count, Color32 color)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float height = KuiDPI.Px(ctx.Settings.BaseFontSize + 4f);
            var rect = ctx.Layout.NextRect(height);
            ctx.CommandBuffer.PushLabel(text, 0, count, rect, color, ctx.Layout.CurrentClip);
        }

        // ---- Zero-GC dynamic text ---------------------------------------------
        // The systemic way to draw label + numbers in ANY window (built-in or a
        // developer's custom window) WITHOUT allocating a string per frame. Prefer
        // these over $"..."/string concat, which allocate on every render.
        static readonly char[] s_textBuffer = new char[1024];

        /// <summary>
        /// Returns a fresh, allocation-free builder over KUI's shared buffer. Chain
        /// <c>.Add(...)</c> and pass to <see cref="Label(KuiTextBuilder)"/>. Build and consume
        /// immediately — the buffer is reused by the next <c>Text()</c> call.
        /// <code>KUI.Label(KUI.Text().Add("FPS: ").Add(fps).Add(" / ").Add(ms).Add(" ms"));</code>
        /// </summary>
        public static KuiTextBuilder Text() => new KuiTextBuilder(s_textBuffer);

        public static void Label(KuiTextBuilder text) => Label(text, KuiStyles.Text);

        public static void Label(KuiTextBuilder text, Color32 color)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float height = KuiDPI.Px(ctx.Settings.BaseFontSize + 4f);
            var rect = ctx.Layout.NextRect(height);
            ctx.CommandBuffer.PushLabel(text.Buffer, 0, text.Length, rect, color, ctx.Layout.CurrentClip);
        }

        // One-call convenience for the most common "label + number [+ suffix]" case — zero-GC.
        public static void Label(string prefix, long value)                => Label(Text().Add(prefix).Add(value));
        public static void Label(string prefix, long value, string suffix) => Label(Text().Add(prefix).Add(value).Add(suffix));
        public static void Label(string prefix, float value)                => Label(Text().Add(prefix).Add(value));
        public static void Label(string prefix, float value, string suffix) => Label(Text().Add(prefix).Add(value).Add(suffix));

        public static void Rect(float width, float height, Color32 color)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            var rect = ctx.Layout.NextRect(KuiDPI.Px(width), KuiDPI.Px(height));
            ctx.CommandBuffer.PushRect(rect, color, ctx.Layout.CurrentClip);
        }

        public static void Separator()
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float h = KuiDPI.Px(KuiStyles.SeparatorHeight);
            var rect = ctx.Layout.NextRect(h);
            ctx.CommandBuffer.PushRect(rect, KuiStyles.Separator, ctx.Layout.CurrentClip);
        }

        public static bool Button(string text)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return false;
            return KuiButton.Draw(text, ctx);
        }

        /// <summary>
        /// Button with explicit width — pass inside <see cref="BeginGroup"/> for
        /// proper horizontal slotting.
        /// </summary>
        public static bool Button(string text, float width)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return false;
            return KuiButton.Draw(text, ctx, width);
        }

        public static bool Toggle(bool value, string label)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return value;
            return KuiToggle.Draw(value, label, ctx);
        }

        /// <summary>
        /// Toggle with explicit width — pass inside <see cref="BeginGroup"/> for
        /// proper horizontal slotting.
        /// </summary>
        public static bool Toggle(bool value, string label, float width)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return value;
            return KuiToggle.Draw(value, label, ctx, width);
        }

        public static float Slider(float value, float min, float max)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return value;
            return KuiSlider.Draw(value, min, max, ctx);
        }

        /// <summary>
        /// Single-line text input. <paramref name="widgetId"/> MUST be unique
        /// per visible widget within a frame (otherwise focus jumps).
        /// Returns <c>true</c> exactly on the frame Enter is pressed.
        /// <paramref name="width"/> = 0 means "use remaining row width".
        /// </summary>
        public static bool TextField(int widgetId, ref KuiTextFieldState state, float width = 0f)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return false;
            return KuiTextField.Draw(widgetId, ref state, ctx, width);
        }

        /// <summary>
        /// Horizontally-scrollable strip of toggleable chips. Returns the index
        /// of the chip toggled this frame, or -1.
        /// <paramref name="widgetId"/> MUST be unique per visible widget within
        /// a frame. <paramref name="labels"/> and <paramref name="states"/> MUST
        /// have equal length. <paramref name="height"/> = 0 uses default row height.
        /// </summary>
        public static int ChipStrip(int widgetId,
                                    System.Collections.Generic.IReadOnlyList<string> labels,
                                    System.Collections.Generic.IList<bool> states,
                                    ref Vector2 scroll,
                                    float height = 0f)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return -1;
            return KuiChipStrip.Draw(widgetId, labels, states, ref scroll, ctx, height);
        }

        // BeginCollapsible/EndCollapsible — header always renders; the caller
        // gates child rendering on the returned bool.
        // Pattern:
        //   if (KUI.BeginCollapsible(id, "Cheats", ref _expanded)) {
        //       KUI.Toggle(...);
        //   }
        //   KUI.EndCollapsible();
        public static bool BeginCollapsible(int widgetId, string title, ref bool expanded)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return false;
            expanded = KuiCollapsibleSection.DrawHeader(widgetId, title, expanded, ctx);
            return expanded;
        }

        // No matching state to clean up — present for symmetry and to leave a
        // hook for future "indent / outdent" behaviour.
        public static void EndCollapsible() { }

        /// <summary>
        /// Open a scroll viewport. <b>Must</b> be paired with <see cref="EndScroll"/>.
        /// Pattern:
        /// <code>
        /// var sh = KUI.BeginScroll(ref _scroll);
        /// // ... widgets ...
        /// KUI.EndScroll(ref _scroll, sh);
        /// </code>
        /// </summary>
        public static KuiScrollHandle BeginScroll(ref Vector2 scrollPos)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return default;

            var windowRect = ctx.CurrentWindowRect.height > 0
                ? ctx.CurrentWindowRect
                : new UnityEngine.Rect(0, 0, Screen.width, Screen.height);

            float pad = KuiDPI.Px(KuiStyles.Padding);
            float viewX = ctx.Layout.ContentStartX;
            float viewY = ctx.Layout.CursorY;
            float viewW = ctx.Layout.ContentWidth;
            float viewH = windowRect.y + windowRect.height - viewY - pad;

            var viewportRect = new Unity.Mathematics.float4(viewX, viewY, viewW, viewH);
            return KuiScrollImpl.Begin(ctx, ref scrollPos, viewportRect);
        }

        public static void EndScroll(ref Vector2 scrollPos, KuiScrollHandle handle)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;
            KuiScrollImpl.End(ctx, ref scrollPos, handle);
        }

        // Returns the concrete struct (NOT IDisposable) so `using (KUI.BeginGroup())`
        // disposes without boxing the scope onto the heap every call.
        public static GroupScope BeginGroup()
        {
            var ctx = KuiContext.Instance;
            ctx?.Layout.BeginGroup();
            return new GroupScope();
        }

        public readonly struct GroupScope : IDisposable
        {
            public void Dispose() => KuiContext.Instance?.Layout.EndGroup();
        }
    }
}
