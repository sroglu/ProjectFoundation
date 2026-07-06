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
        /// tutorial can be fast-forwarded (auto-advance / auto-click).
        /// </summary>
        bool AutoAdvance { get; set; }

        event Action<TutorialId> TutorialStarted;
        event Action<TutorialId, TutorialOutcome> TutorialEnded;
        event Action<ITutorialStep> StepStarted;

        /// <summary>Start a tutorial unless one is already running; returns whether it started.</summary>
        bool TryStart(TutorialId id);

        /// <summary>Start a tutorial even if one is running, aborting the current run first.</summary>
        void ForceStart(TutorialId id);

        /// <summary>Skip (player-cancel) the active run, if any.</summary>
        void Skip();

        /// <summary>Restrict automatic eligibility to this set; pass an empty/null set to clear the whitelist.</summary>
        void SetRunSpecific(IEnumerable<TutorialId> ids);

        /// <summary>Advance the active run and, when idle, poll automatic triggers.</summary>
        void Tick(float deltaSeconds);
    }
}
