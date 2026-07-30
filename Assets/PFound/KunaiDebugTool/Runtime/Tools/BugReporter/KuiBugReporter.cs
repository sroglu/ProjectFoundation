using System.Collections.Generic;

namespace Kunai
{
    /// <summary>
    /// Process-wide registry of <see cref="IBugReportSink"/> implementations.
    /// Typical pattern: register a sink at app start, never unregister.
    /// </summary>
    public static class KuiBugReporter
    {
        // List, not HashSet, so iteration order is registration order — gives
        // a sink at the head a chance to short-circuit before the rest fire.
        // Duplicate-instance protection is handled in RegisterSink.
        static readonly List<IBugReportSink> s_sinks = new();

        // Lock object for thread-safe register / unregister / dispatch.
        // The lock is held only briefly to copy the list into a local; the
        // actual sink calls run unlocked so a slow sink doesn't block others.
        static readonly object s_lock = new();

        public static void RegisterSink(IBugReportSink sink)
        {
            if (sink == null) return;
            lock (s_lock)
            {
                if (s_sinks.Contains(sink)) return;
                s_sinks.Add(sink);
            }
        }

        public static void UnregisterSink(IBugReportSink sink)
        {
            if (sink == null) return;
            lock (s_lock) s_sinks.Remove(sink);
        }

        // Snapshot the list under the lock then call sinks unlocked. Each
        // sink invocation is wrapped in try/catch — contract: "must not throw".
        // Failures route to KuiLogger so they show in Console without
        // polluting the calling thread's exception state.
        internal static void Dispatch(KuiBugReport report)
        {
            IBugReportSink[] snapshot;
            lock (s_lock)
            {
                if (s_sinks.Count == 0) return;
                snapshot = s_sinks.ToArray();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i].Send(report); }
                catch (System.Exception ex)
                {
                    KuiLogger.Error($"sink {snapshot[i].GetType().Name} threw: {ex.Message}", "BugReporter");
                }
            }
        }

        // Internal helper for tests.
        internal static int SinkCount
        {
            get { lock (s_lock) return s_sinks.Count; }
        }
    }
}
