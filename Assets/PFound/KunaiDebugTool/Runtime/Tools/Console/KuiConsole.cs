using System;
using System.Diagnostics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Captures Unity log events into a thread-safe ring buffer.
    /// Pair with <see cref="KuiConsoleWindow"/> to display them.
    ///
    /// Lifecycle:
    ///   KuiConsole.Initialize();                          // hook log events
    ///   KUI.RegisterWindow(new KuiConsoleWindow());       // mount UI
    ///   ...
    ///   KuiConsole.Shutdown();                            // unhook (idempotent)
    /// </summary>
    public static class KuiConsole
    {
        internal static KuiLogBuffer Buffer;
        internal static KuiCategoryRegistry Categories;
        static bool s_hooked;
        static readonly Stopwatch s_clock = Stopwatch.StartNew();

        // Re-entry guard — set by EnqueueDirect while it mirrors to UnityEngine.Debug,
        // checked by OnLogReceived to skip the echo. ThreadStatic so worker-thread
        // log calls don't leak into other threads' guards.
        [ThreadStatic] static bool t_emittingFromDirect;

        public static int LogCount     => Buffer?.Count ?? 0;
        public static int InfoCount    => Buffer?.InfoCount ?? 0;
        public static int WarningCount => Buffer?.WarningCount ?? 0;
        public static int ErrorCount   => Buffer?.ErrorCount ?? 0;

        public static void Initialize(int capacity = 5000)
        {
            if (s_hooked) return;
            Buffer = new KuiLogBuffer(capacity);
            Categories = new KuiCategoryRegistry();
            Application.logMessageReceivedThreaded += OnLogReceived;
            s_hooked = true;
        }

        public static void Shutdown()
        {
            if (!s_hooked) return;
            Application.logMessageReceivedThreaded -= OnLogReceived;
            s_hooked = false;
            Buffer?.Clear();
            Categories?.Clear();
            Buffer = null;
            Categories = null;
        }

        public static void Clear()
        {
            Buffer?.Clear();
            Categories?.Clear();
        }

        static void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            // Echo from EnqueueDirect's Debug.Log mirror — already enqueued
            // through the direct path, drop here to avoid duplicate entries.
            if (t_emittingFromDirect) return;
            if (Buffer == null) return;

            var level = type switch
            {
                LogType.Error     => KuiLogLevel.Error,
                LogType.Assert    => KuiLogLevel.Error,
                LogType.Warning   => KuiLogLevel.Warning,
                LogType.Exception => KuiLogLevel.Exception,
                _                 => KuiLogLevel.Info,
            };

            // Auto-detect [Foo] prefix at capture time (runs off main thread,
            // so it must be allocation-light and lock-free — KuiCategoryParser
            // is both). Registry insert happens later in Drain on the main
            // thread to avoid HashSet contention.
            //
            // Note: this parser path is the *third-party / legacy* capture
            // path. New code should call KuiLogger.Info(msg, "Category") which
            // goes through EnqueueDirect (no parse, single allocation).
            KuiCategoryParser.Parse(condition, out string category, out string message);

            // realtimeSinceStartup is main-thread-only; this callback may fire
            // from worker threads. Stopwatch is thread-safe.
            Buffer.Enqueue(new KuiLogEntry
            {
                TimeSinceStartup = (float)s_clock.Elapsed.TotalSeconds,
                Level            = level,
                Message          = message,
                StackTrace       = stackTrace,
                Category         = category,
            });
        }

        // Internal helper for KuiLogger — bypasses prefix parsing because the
        // category is supplied explicitly. Mirrors the entry to UnityEngine.Debug
        // so it also surfaces in the Unity Console (re-entry guard prevents
        // OnLogReceived from enqueueing the echo).
        internal static void EnqueueDirect(KuiLogLevel level, string category, string message, Exception ex = null)
        {
            // 1) Mirror to Unity Console. Concat is the cheapest format for the
            //    [Cat] prefix — formatString interpolation would allocate twice.
            var consoleText = string.IsNullOrEmpty(category)
                ? (message ?? string.Empty)
                : string.Concat("[", category, "] ", message ?? string.Empty);

            t_emittingFromDirect = true;
            try
            {
                switch (level)
                {
                    case KuiLogLevel.Warning:
                        UnityEngine.Debug.LogWarning(consoleText);
                        break;
                    case KuiLogLevel.Error:
                        UnityEngine.Debug.LogError(consoleText);
                        break;
                    case KuiLogLevel.Exception:
                        if (ex != null)
                        {
                            // Debug.LogException prints the stack of the exception
                            // (not the call site), which is what callers want.
                            // The accompanying message goes as a separate Error
                            // line so it's visible at-a-glance.
                            if (!string.IsNullOrEmpty(consoleText))
                                UnityEngine.Debug.LogError(consoleText);
                            UnityEngine.Debug.LogException(ex);
                        }
                        else
                        {
                            UnityEngine.Debug.LogError(consoleText);
                        }
                        break;
                    default:
                        UnityEngine.Debug.Log(consoleText);
                        break;
                }
            }
            finally
            {
                t_emittingFromDirect = false;
            }

            // 2) Direct buffer enqueue (already-structured fields, no parse).
            if (Buffer == null) return;
            string stack = ex != null ? ex.ToString() : null;
            Buffer.Enqueue(new KuiLogEntry
            {
                TimeSinceStartup = (float)s_clock.Elapsed.TotalSeconds,
                Level            = level,
                Message          = message ?? string.Empty,
                StackTrace       = stack,
                Category         = category,
            });
        }

        // Verbose path — Kunai overlay only, NOT mirrored to Unity Console.
        // Used by KuiLogger.Verbose / KuiLog.Verbose (both [Conditional("KUNAI_VERBOSE")]).
        // No re-entry guard needed: there is no Debug.Log echo to drop.
        internal static void EnqueueVerbose(string category, string message)
        {
            if (Buffer == null) return;
            Buffer.Enqueue(new KuiLogEntry
            {
                TimeSinceStartup = (float)s_clock.Elapsed.TotalSeconds,
                Level            = KuiLogLevel.Verbose,
                Message          = message ?? string.Empty,
                StackTrace       = null,
                Category         = category,
            });
        }
    }
}
