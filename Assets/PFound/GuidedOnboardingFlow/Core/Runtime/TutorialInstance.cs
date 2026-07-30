using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// A runnable tutorial: its identity, a human label, and the ordered steps to drive. This is the
    /// resolved shape the runner consumes — authoring assets and DI produce it, but the engine-free core
    /// only ever sees this plain object.
    /// </summary>
    public sealed class TutorialInstance
    {
        public TutorialId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ITutorialStep> Steps { get; }

        /// <summary>
        /// What a step timeout does when the step itself doesn't specify (its
        /// <see cref="ITutorialStep.TimeoutOutcome"/> is null). Authoring sets this per tutorial so a
        /// whole flow can default to "abort on any timeout" without annotating every step.
        /// </summary>
        public StepTimeoutOutcome DefaultTimeoutOutcome { get; }

        public TutorialInstance(
            TutorialId id,
            string displayName,
            IReadOnlyList<ITutorialStep> steps,
            StepTimeoutOutcome defaultTimeoutOutcome = StepTimeoutOutcome.Advance)
        {
            if (steps == null)
                throw new ArgumentNullException(nameof(steps));
            Id = id;
            DisplayName = displayName;
            Steps = steps;
            DefaultTimeoutOutcome = defaultTimeoutOutcome;
        }
    }
}
