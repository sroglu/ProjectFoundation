using UnityEngine.LowLevel;

namespace PFound.LoopScheduler
{
    /// <summary>Helpers for inspecting the PlayerLoop after <see cref="LoopScheduler"/> injection.</summary>
    public static class LoopSchedulerTools
    {
        /// <summary>True if <paramref name="system"/> is the LoopScheduler phase pipeline injected into the PlayerLoop.</summary>
        public static bool IsCustomLoop(this PlayerLoopSystem system) => system.type == typeof(LoopScheduler);
    }
}
