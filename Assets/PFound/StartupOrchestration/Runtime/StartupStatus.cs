using UnityEngine;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// One step's instantaneous report: its <see cref="WaitReason"/> and clamped 0..1 progress.
    /// </summary>
    public readonly struct StartupStatus
    {
        public readonly WaitReason Reason;
        public readonly float      Progress01;

        public StartupStatus(WaitReason reason, float progress01)
        {
            Reason     = reason;
            Progress01 = Mathf.Clamp01(progress01);
        }
    }
}
