namespace Kunai
{
    /// <summary>
    /// Single-owner keyboard focus arbitrator. Multiple TextFields can exist
    /// (Console search, Commander prompt, Bug-report description). Without this
    /// every keystroke would route to all of them or to none.
    ///
    /// Widget ids are caller-supplied positive integers, must be stable across
    /// frames and unique within a frame. Id 0 is reserved for "no focus".
    /// </summary>
    internal static class KuiInputFocus
    {
        public const int NoFocus = 0;

        public static int FocusedWidgetId { get; private set; } = NoFocus;

        // Set to the current frame index whenever focus changes; used by KuiContext
        // to suppress click-outside clearing on the same frame focus was acquired
        // (otherwise the same MouseDown that acquires focus also clears it).
        public static int LastChangeFrame { get; private set; } = -1;

        /// <summary>
        /// Acquire focus for <paramref name="widgetId"/>. Pass a positive id; 0 is
        /// reserved. Returns true if focus is now held by this widget.
        ///
        /// Bumps <see cref="LastChangeFrame"/> on every successful call (even when
        /// the same widget reclaims focus) so KuiContext's end-of-frame
        /// click-outside-clear sees the touch and leaves focus alone.
        /// </summary>
        public static bool TryAcquire(int widgetId)
        {
            if (widgetId <= NoFocus) return false;
            FocusedWidgetId = widgetId;
            LastChangeFrame = UnityEngine.Time.frameCount;
            return true;
        }

        public static bool IsFocused(int widgetId)
        {
            return widgetId > NoFocus && FocusedWidgetId == widgetId;
        }

        public static void Clear()
        {
            if (FocusedWidgetId == NoFocus) return;
            FocusedWidgetId = NoFocus;
            LastChangeFrame = UnityEngine.Time.frameCount;
        }
    }
}
