using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// A registered, not-yet-instantiated tutorial the manager can start on demand. Steps are produced
    /// lazily by <see cref="BuildSteps"/> so each run gets fresh step objects; the optional
    /// <see cref="Precondition"/> is handed to the runner as its "still valid" gate, and the
    /// <see cref="Triggers"/> (combined with AND) drive automatic starts. <see cref="Replay"/> decides
    /// whether/how a completed run is remembered so it does not re-fire.
    /// </summary>
    public sealed class TutorialBlueprint
    {
        private static readonly ITutorialTrigger[] NoTriggers = Array.Empty<ITutorialTrigger>();

        public TutorialId Id { get; }
        public string DisplayName { get; }
        public TriggerMode Mode { get; }

        /// <summary>Every trigger must fire on the same poll (AND) for an automatic start.</summary>
        public IReadOnlyList<ITutorialTrigger> Triggers { get; }

        public Func<bool> Precondition { get; }

        /// <summary>How a completed run of this tutorial is remembered (defaults to <see cref="ReplayPolicy.OnceAccount"/>).</summary>
        public ReplayPolicy Replay { get; }

        /// <summary>Fallback timeout behavior for steps that don't set their own.</summary>
        public StepTimeoutOutcome DefaultTimeoutOutcome { get; }

        private readonly Func<IReadOnlyList<ITutorialStep>> _buildSteps;

        public TutorialBlueprint(
            TutorialId id,
            string displayName,
            TriggerMode mode,
            Func<IReadOnlyList<ITutorialStep>> buildSteps,
            ITutorialTrigger trigger = null,
            Func<bool> precondition = null,
            ReplayPolicy replay = ReplayPolicy.OnceAccount,
            StepTimeoutOutcome defaultTimeoutOutcome = StepTimeoutOutcome.Advance)
            : this(id, displayName, mode, buildSteps,
                   trigger == null ? NoTriggers : new[] { trigger },
                   precondition, replay, defaultTimeoutOutcome)
        {
        }

        public TutorialBlueprint(
            TutorialId id,
            string displayName,
            TriggerMode mode,
            Func<IReadOnlyList<ITutorialStep>> buildSteps,
            IReadOnlyList<ITutorialTrigger> triggers,
            Func<bool> precondition = null,
            ReplayPolicy replay = ReplayPolicy.OnceAccount,
            StepTimeoutOutcome defaultTimeoutOutcome = StepTimeoutOutcome.Advance)
        {
            Id = id;
            DisplayName = displayName;
            Mode = mode;
            _buildSteps = buildSteps ?? throw new ArgumentNullException(nameof(buildSteps));
            Triggers = triggers ?? NoTriggers;
            Precondition = precondition;
            Replay = replay;
            DefaultTimeoutOutcome = defaultTimeoutOutcome;
        }

        public TutorialInstance Instantiate()
        {
            return new TutorialInstance(Id, DisplayName, _buildSteps(), DefaultTimeoutOutcome);
        }

        /// <summary>Re-arm all triggers so a once-session / repeatable tutorial can fire again.</summary>
        public void ResetTriggers()
        {
            for (int i = 0; i < Triggers.Count; i++)
                Triggers[i]?.Reset();
        }
    }
}
