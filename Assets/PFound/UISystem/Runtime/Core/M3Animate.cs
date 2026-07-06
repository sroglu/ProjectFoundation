using System;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Lightweight schedule-based animation utility for M3 components.
    /// Uses <see cref="IVisualElementScheduledItem"/> to lerp a float value
    /// over a given duration, calling an update callback each frame.
    /// </summary>
    public static class M3Animate
    {
        private const long FrameIntervalMs = 16; // ~60 fps

        /// <summary>
        /// Animates a float value from <paramref name="from"/> to <paramref name="to"/>
        /// over <paramref name="durationMs"/> milliseconds with ease-out easing.
        /// </summary>
        /// <param name="owner">The VisualElement whose scheduler drives the animation.</param>
        /// <param name="from">Start value.</param>
        /// <param name="to">End value.</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <param name="onUpdate">Called each frame with the current interpolated value (0→1 progress mapped through easing).</param>
        /// <param name="onComplete">Optional callback when animation finishes.</param>
        /// <returns>The scheduled item (can be used to pause/cancel).</returns>
        public static IVisualElementScheduledItem Float(
            VisualElement owner,
            float from,
            float to,
            float durationMs,
            Action<float> onUpdate,
            Action onComplete = null)
        {
            long startTime = CurrentTimeMs(owner);
            float duration = Math.Max(1f, durationMs);

            var item = owner.schedule.Execute(() =>
            {
                float elapsed = CurrentTimeMs(owner) - startTime;
                float t = Math.Min(elapsed / duration, 1f);

                // Ease-out cubic: 1 - (1-t)^3
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);

                float value = from + (to - from) * eased;
                onUpdate?.Invoke(value);

                if (t >= 1f)
                    onComplete?.Invoke();
            }).Every(FrameIntervalMs).Until(() =>
            {
                float elapsed = CurrentTimeMs(owner) - startTime;
                return elapsed >= duration;
            });

            return item;
        }

        private static long CurrentTimeMs(VisualElement owner)
        {
            return owner.panel?.contextType == ContextType.Player
                ? (long)(UnityEngine.Time.unscaledTime * 1000f)
                : System.Diagnostics.Stopwatch.GetTimestamp() * 1000L / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
