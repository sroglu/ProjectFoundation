using System;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Engine-free logging seam. The core never references UnityEngine, so it routes warnings and
    /// errors through settable delegates. The Unity assembly wires these to the engine console at
    /// startup; unset, they are silent (tests can capture them instead).
    /// </summary>
    public static class LocalizationLog
    {
        public static Action<string> OnWarning;
        public static Action<string> OnError;

        public static void Warn(string message) => OnWarning?.Invoke(message);
        public static void Error(string message) => OnError?.Invoke(message);
    }
}
