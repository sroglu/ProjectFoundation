using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Tiny engine-agnostic logging seam. The messaging core never talks to
    /// UnityEngine directly; a host (Unity, a test rig, a headless server) points
    /// these sinks wherever it wants. Left unset, logging is silently dropped —
    /// this is deliberate so the pure core can run under csc/mono with no console
    /// noise unless a host opts in.
    /// </summary>
    public static class NetLog
    {
        public static Action<string> Trace;
        public static Action<string> Notice;
        public static Action<string> Alarm;

        public static void OnTrace(string text) => Trace?.Invoke(text);
        public static void OnNotice(string text) => Notice?.Invoke(text);
        public static void OnAlarm(string text) => Alarm?.Invoke(text);
    }
}
