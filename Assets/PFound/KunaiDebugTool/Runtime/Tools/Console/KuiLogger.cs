using System;
using System.Diagnostics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// The single, lightweight logging API for Kunai Debug Tool.
    ///
    /// <para>Every call mirrors to BOTH:</para>
    /// <list type="bullet">
    ///   <item>The Unity Editor / player Console (via <c>UnityEngine.Debug.Log*</c>),</item>
    ///   <item>The in-game Kunai Console overlay (direct buffer enqueue, no string parse).</item>
    /// </list>
    ///
    /// <para><b>Performance</b> — designed for the <i>lightweight</i> Kunai contract:</para>
    /// <list type="bullet">
    ///   <item><c>KuiLogger.Info("msg")</c> — uncategorised, single Debug.Log call, zero string concat.</item>
    ///   <item><c>KuiLogger.Info("msg", "Cat")</c> — one <see cref="string.Concat(string, string, string, string)"/>
    ///     to build the <c>[Cat] msg</c> form for the Console line, plus a single direct buffer enqueue.
    ///     The category-detection parser <b>never runs</b> on this path.</item>
    /// </list>
    ///
    /// <para><b>Discouraged path</b> — pre-formatting the prefix yourself and calling
    /// <c>UnityEngine.Debug.Log("[Cat] msg")</c>. Kunai's hook will still capture it,
    /// but the capture path runs <see cref="KuiCategoryParser"/> (regex/scan + substring)
    /// to extract the category — this is strictly more allocation than calling
    /// <c>KuiLogger.Info("msg", "Cat")</c>. The auto-capture path exists for
    /// third-party packages and legacy code where you don't control the call site;
    /// in <i>your own new code</i>, prefer <c>KuiLogger</c>.</para>
    ///
    /// <para><b>Existing <c>Debug.Log("plain")</c> calls</b> are still captured and
    /// surface in the Kunai overlay as uncategorised entries — no refactor required.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// using Kunai;
    ///
    /// // One-liner, no category
    /// KuiLogger.Info("Player spawned");
    /// KuiLogger.Warn("low memory");
    /// KuiLogger.Error("save failed");
    ///
    /// // With category — preferred for filterable signals
    /// KuiLogger.Info("HP=100",          "Player");
    /// KuiLogger.Warn("retry 1/3",       "Network");
    /// KuiLogger.Error("invalid token",  "Auth");
    /// KuiLogger.Exception("rpc failed", ex, "Network");
    /// </code>
    /// </example>
    public static class KuiLogger
    {
        // --- Public API ----------------------------------------------------

        [HideInCallstack]
        public static void Info(string message, string category = null)
        {
            ValidateCategoryOrNull(category);
            KuiConsole.EnqueueDirect(KuiLogLevel.Info, category, message ?? string.Empty);
        }

        [HideInCallstack]
        public static void Warn(string message, string category = null)
        {
            ValidateCategoryOrNull(category);
            KuiConsole.EnqueueDirect(KuiLogLevel.Warning, category, message ?? string.Empty);
        }

        [HideInCallstack]
        public static void Error(string message, string category = null)
        {
            ValidateCategoryOrNull(category);
            KuiConsole.EnqueueDirect(KuiLogLevel.Error, category, message ?? string.Empty);
        }

        [HideInCallstack]
        public static void Exception(string message, Exception ex, string category = null)
        {
            ValidateCategoryOrNull(category);
            KuiConsole.EnqueueDirect(KuiLogLevel.Exception, category, message ?? string.Empty, ex);
        }

        /// <summary>
        /// Verbose log — surfaces in the Kunai overlay only, NOT in the Unity Console.
        /// Gated by the <c>KUNAI_VERBOSE</c> compile symbol via
        /// <see cref="ConditionalAttribute"/> — when undefined, <b>every call site
        /// is removed by the compiler</b> (including the argument expressions —
        /// no string interpolation cost survives in production builds).
        /// </summary>
        [Conditional("KUNAI_VERBOSE"), HideInCallstack]
        public static void Verbose(string message, string category = null)
            => VerboseImpl(message, category);

        // Non-Conditional inner so other API layers (e.g. KuiLog.Verbose) can dispatch
        // here without losing the body to compile-time stripping. KuiLogger.cs itself
        // does NOT define KUNAI_VERBOSE; without this indirection, any call to the
        // [Conditional] Verbose from within this assembly is dropped at compile time,
        // leaving wrapper bodies empty and surfacing as silent no-ops at runtime.
        [HideInCallstack]
        internal static void VerboseImpl(string message, string category)
        {
            ValidateCategoryOrNull(category);
            KuiConsole.EnqueueVerbose(category, message ?? string.Empty);
        }

        // --- Per-class tagged logger factory -------------------------------

        /// <summary>
        /// Bind a logger to the calling type's name (<c>nameof(T)</c>). Use as a
        /// per-class field to keep call sites terse and the tag in sync with the
        /// class via IDE rename.
        /// <code>
        /// static readonly KuiLog Log = KuiLogger.For&lt;MyClass&gt;();
        /// // ...
        /// Log.Info("ready");
        /// </code>
        /// </summary>
        public static KuiLog For<T>()
        {
            // The auto-derived category must satisfy the same ≤31-char rule Info() enforces, otherwise
            // a long type name (e.g. EnvironmentColorGradingController = 33) makes every log call throw.
            // Truncate so For<T>() can never produce a category its own log methods would reject.
            var name = typeof(T).Name;
            return new(name.Length > 31 ? name.Substring(0, 31) : name);
        }

        /// <summary>
        /// Bind a logger to an explicit tag. Prefer <see cref="For{T}"/> when the
        /// tag should match a class name; use this overload for runtime-chosen
        /// tags (e.g. a sub-system name decided at construction time).
        /// </summary>
        public static KuiLog For(string tag) => new(tag);

        // --- Validation ----------------------------------------------------

        // Validation rules mirror the auto-detect grammar (R4) — anything
        // accepted as a "[Foo]" prefix is also accepted as a category here.
        // Throw for clearly-broken inputs; null/empty is allowed = uncategorised.
        static void ValidateCategoryOrNull(string category)
        {
            if (category == null) return;
            if (category.Length == 0)
                throw new ArgumentException("Category, if provided, must be non-empty.", nameof(category));
            if (category.Length > 31)
                throw new ArgumentException("Category must be ≤ 31 chars.", nameof(category));
            if (category.IndexOf('[') >= 0)
                throw new ArgumentException("Category must not contain '['.", nameof(category));
        }
    }

    /// <summary>
    /// Tag-bound logger created via <see cref="KuiLogger.For{T}"/>. Forwards every
    /// call to <see cref="KuiLogger"/> with the captured tag as category. Zero
    /// allocation per call (struct, no boxing through interface).
    /// </summary>
    public readonly struct KuiLog
    {
        readonly string _tag;
        internal KuiLog(string tag) => _tag = tag;

        [HideInCallstack] public void Info(string message)  => KuiLogger.Info(message, _tag);
        [HideInCallstack] public void Warn(string message)  => KuiLogger.Warn(message, _tag);
        [HideInCallstack] public void Error(string message) => KuiLogger.Error(message, _tag);
        [HideInCallstack] public void Exception(string message, Exception ex) => KuiLogger.Exception(message, ex, _tag);

        // Verbose: gated at THIS call site too via [Conditional]. Without this
        // attribute the wrapper would still run when KUNAI_VERBOSE is undefined,
        // even though the inner KuiLogger.Verbose call would be dropped — the
        // string interpolation cost would survive. Marking both layers ensures
        // production builds drop the entire `Log.Verbose($"…")` expression at
        // the caller's site.
        [Conditional("KUNAI_VERBOSE"), HideInCallstack]
        public void Verbose(string message) => KuiLogger.VerboseImpl(message, _tag);
    }
}
