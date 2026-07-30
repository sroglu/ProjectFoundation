using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.DebugTools
{
    /// <summary>
    /// Throttles repeated error logging so a fault that recurs every frame does not flood the console.
    /// Each distinct <c>key</c> is allowed through at most once per time window; suppressed hits are
    /// counted so the caller can surface a "(+N suppressed)" note when the window reopens.
    /// </summary>
    public sealed class LogRateLimiter
    {
        private readonly struct Entry
        {
            public readonly float LastEmitTime;
            public readonly int SuppressedSince;

            public Entry(float lastEmitTime, int suppressedSince)
            {
                LastEmitTime = lastEmitTime;
                SuppressedSince = suppressedSince;
            }
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly float _windowSeconds;

        public LogRateLimiter(float windowSeconds = 1f)
        {
            _windowSeconds = windowSeconds;
        }

        /// <summary>Reports whether a message under <paramref name="key"/> should be emitted at
        /// <paramref name="now"/> (defaults to <see cref="Time.unscaledTime"/>). When it returns true,
        /// <paramref name="suppressedCount"/> tells how many hits were swallowed since the previous
        /// emission for that key.</summary>
        public bool ShouldEmit(string key, out int suppressedCount, float now = float.NaN)
        {
            if (float.IsNaN(now)) now = Time.unscaledTime;

            if (!_entries.TryGetValue(key, out var entry))
            {
                _entries[key] = new Entry(now, 0);
                suppressedCount = 0;
                return true;
            }

            if (now - entry.LastEmitTime >= _windowSeconds)
            {
                suppressedCount = entry.SuppressedSince;
                _entries[key] = new Entry(now, 0);
                return true;
            }

            _entries[key] = new Entry(entry.LastEmitTime, entry.SuppressedSince + 1);
            suppressedCount = 0;
            return false;
        }

        /// <summary>Convenience overload that discards the suppressed count.</summary>
        public bool ShouldEmit(string key) => ShouldEmit(key, out _);

        /// <summary>Forgets the throttle state for a single key.</summary>
        public void Reset(string key) => _entries.Remove(key);

        /// <summary>Forgets all throttle state.</summary>
        public void Clear() => _entries.Clear();
    }
}
