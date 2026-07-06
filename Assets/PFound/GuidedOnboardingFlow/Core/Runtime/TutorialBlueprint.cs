using System;
using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// A registered, not-yet-instantiated tutorial the manager can start on demand. Steps are produced
    /// lazily by <see cref="BuildSteps"/> so each run gets fresh step objects; the optional
    /// <see cref="Precondition"/> is handed to the runner as its "still valid" gate, and <see cref="Trigger"/>
    /// drives automatic starts.
    /// </summary>
    public sealed class TutorialBlueprint
    {
        public TutorialId Id { get; }
        public string DisplayName { get; }
        public TriggerMode Mode { get; }
        public ITutorialTrigger Trigger { get; }
        public Func<bool> Precondition { get; }

        private readonly Func<IReadOnlyList<ITutorialStep>> _buildSteps;

        public TutorialBlueprint(
            TutorialId id,
            string displayName,
            TriggerMode mode,
            Func<IReadOnlyList<ITutorialStep>> buildSteps,
            ITutorialTrigger trigger = null,
            Func<bool> precondition = null)
        {
            Id = id;
            DisplayName = displayName;
            Mode = mode;
            _buildSteps = buildSteps ?? throw new ArgumentNullException(nameof(buildSteps));
            Trigger = trigger;
            Precondition = precondition;
        }

        public TutorialInstance Instantiate()
        {
            return new TutorialInstance(Id, DisplayName, _buildSteps());
        }
    }
}
