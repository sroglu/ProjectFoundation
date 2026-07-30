using System.Collections.Generic;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// Combined view of all startup steps, emitted per tick to the loading screen.
    /// <see cref="Overall01"/> is smoothed monotonic non-decreasing by the orchestrator.
    /// <see cref="DominantReason"/> is the step furthest from completion (lowest progress;
    /// ties broken by most-recent report) — it drives the status line.
    /// </summary>
    public readonly struct StartupAggregate
    {
        public readonly float      Overall01;
        public readonly WaitReason DominantReason;
        public readonly IReadOnlyList<(string source, WaitReason reason, float progress)> PerStep;

        public StartupAggregate(
            float overall01,
            WaitReason dominantReason,
            IReadOnlyList<(string source, WaitReason reason, float progress)> perStep)
        {
            Overall01      = overall01;
            DominantReason = dominantReason;
            PerStep        = perStep;
        }
    }
}
