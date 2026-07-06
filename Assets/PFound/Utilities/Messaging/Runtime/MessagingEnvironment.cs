using System;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// Pluggable seams that let the engine-free dispatch core stay compilable and
    /// unit-testable under plain csc/mono while gaining engine-aware behavior when
    /// hosted inside Unity.
    ///
    /// Two hooks exist:
    ///   * <see cref="LivenessOf"/> decides whether a listener's guard object is still
    ///     usable. The default treats a non-null managed reference as alive (plain
    ///     objects are never "destroyed"). The Unity host replaces it with a
    ///     fake-null aware probe for <c>UnityEngine.Object</c> guards.
    ///   * <see cref="ReportFault"/> is where the protected raise path forwards
    ///     swallowed listener exceptions. Defaults to standard error; the Unity host
    ///     redirects it to the player log.
    /// </summary>
    public static class MessagingEnvironment
    {
        /// <summary>
        /// Returns true while the supplied guard object is still valid. A null guard
        /// means "no guard" and is always considered alive.
        /// </summary>
        public static Func<object, bool> LivenessOf = DefaultLiveness;

        /// <summary>
        /// Sink for exceptions caught during a protected raise.
        /// </summary>
        public static Action<Exception> ReportFault = DefaultReportFault;

        private static bool DefaultLiveness(object guard)
        {
            // In the pure runtime a live reference is simply a non-null one.
            return guard != null;
        }

        private static void DefaultReportFault(Exception error)
        {
            Console.Error.WriteLine("[Messaging] listener threw during protected raise: " + error);
        }

        /// <summary>
        /// True when the guard is present and no longer usable. A null guard is never
        /// stale (it represents a target-less callback such as a static method).
        /// </summary>
        internal static bool IsStale(object guard)
        {
            if (guard == null)
            {
                return false;
            }

            return !LivenessOf(guard);
        }
    }
}
