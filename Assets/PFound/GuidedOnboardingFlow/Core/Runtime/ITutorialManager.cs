using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// The DI-facing service that owns the single active run. It starts tutorials (politely or forcibly),
    /// skips the current one, restricts which tutorials may run this session, exposes the auto-advance
    /// mode used by UI steps, and broadcasts lifecycle events. Exactly one tutorial is active at a time;
    /// when several become eligible on the same tick the first-registered one wins (deterministic tie-break).
    /// </summary>
    public interface ITutorialManager
    {
        /// <summary>The live runner, or null when nothing is running.</summary>
        TutorialRunner Active { get; }

        bool IsRunning { get; }

        /// <summary>
        /// When true, interactive steps that would otherwise wait for the player self-complete, so a
        /// tutorial can be fast-forwarded (auto-advance / auto-click). Setting this is equivalent to
        /// <see cref="SetAutoAdvance"/> with a zero per-step delay.
        /// </summary>
        bool AutoAdvance { get; set; }

        /// <summary>
        /// In auto-advance mode, how long an interactive step waits before self-completing. Lets a scripted
        /// regression run play at a watchable pace and gives visual steps a beat to render; zero means
        /// "complete on the first tick".
        /// </summary>
        float AutoAdvanceDelaySeconds { get; }

        event Action<TutorialId> TutorialStarted;
        event Action<TutorialId, TutorialOutcome> TutorialEnded;
        event Action<ITutorialStep> StepStarted;

        /// <summary>Raised when the active run moves to a new step, carrying the tutorial id + step index.</summary>
        event Action<TutorialId, int> StepChanged;

        /// <summary>
        /// Start a tutorial unless one is already running; returns whether it started. Respects completion:
        /// a tutorial already recorded complete (and not <see cref="ReplayPolicy.Repeatable"/>) will not
        /// start — use <see cref="ForceStart"/> to bypass that.
        /// </summary>
        bool TryStart(TutorialId id);

        /// <summary>Start a tutorial even if one is running or already completed, aborting the current run first.</summary>
        void ForceStart(TutorialId id);

        /// <summary>Skip (player-cancel) the active run, if any.</summary>
        void Skip();

        /// <summary>Restrict automatic eligibility to this set; pass an empty/null set to clear the whitelist.</summary>
        void SetRunSpecific(IEnumerable<TutorialId> ids);

        /// <summary>Enable/disable auto-advance and set the per-step delay used before a step self-completes.</summary>
        void SetAutoAdvance(bool enabled, float perStepDelaySeconds);

        /// <summary>Advance the active run and, when idle, poll automatic triggers.</summary>
        void Tick(float deltaSeconds);
    }
}
