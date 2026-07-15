using UnityEngine;

namespace Kunai
{
    public class KuiSettings
    {
        public KeyCode ToggleKey = KeyCode.BackQuote;
        public KeyCode ToggleKeyAlt = KeyCode.F1;
        public float SnapThreshold = 8f;
        public float? DpiScaleOverride = null;
        public int BaseFontSize = 14;

        // User-facing overlay size multiplier on top of the auto DPI scale (adjusted live by the
        // in-overlay scale control). All scale math lives in KuiDPI — this is just the stored value.
        public float UserScale = 1f;

        // Touch / mouse toggle gesture (mobile-friendly): N taps inside a small
        // top-left region within a time window flips KUI.IsVisible. Defaults
        // give a 120×120 logical-px target hit-tested with a double tap inside
        // 0.4 seconds — enough to reach reliably without false positives.
        public int   ToggleTapCount    = 3;
        public float ToggleTapWindow   = 0.4f;
        public float ToggleRegionPx    = 120f;

        // Max pointer travel (logical px, DPI-scaled at use) between press and
        // release for the gesture to still count as a tap/click rather than a
        // drag. Above this the release ends a drag and fires no click — so a
        // scroll swipe that can't move (content at its limit) isn't mistaken for
        // selecting the row under the finger.
        public float TapDragThresholdPx = 10f;

        // D6 Touch indicator: small ring follows the active touch / mouse
        // position. Default off — opt-in by setting to true at app start
        // (KUI.Settings.EnableTouchIndicator = true) so players don't see it.
        public bool EnableTouchIndicator = false;

        // Ring tuning — a 24 px outer radius reads on phone screens without
        // covering the touch target. 12 segments is enough for "round-ish"
        // at typical sizes; bump for crisper visuals at the cost of more rects.
        public float TouchIndicatorRadiusPx = 24f;
        public int   TouchIndicatorSegments = 12;
    }
}
