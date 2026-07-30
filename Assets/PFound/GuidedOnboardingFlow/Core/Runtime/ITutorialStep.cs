namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// One unit of guidance the runner drives to completion. A step is polled: the runner asks whether it
    /// is achievable, begins it, advances it each tick until it reports <see cref="StepStatus.Finished"/>,
    /// then either completes it (normal end) or cancels it (external interruption). Implementations should
    /// derive from <see cref="TutorialStepBase"/> so the try/catch + timeout bookkeeping is handled for them.
    /// </summary>
    public interface ITutorialStep
    {
        /// <summary>Optional wall-clock budget in seconds; null means "no limit".</summary>
        float? Timeout { get; }

        /// <summary>
        /// What the run should do if this step's <see cref="Timeout"/> elapses. Null means "use the
        /// tutorial's default" (<see cref="TutorialInstance.DefaultTimeoutOutcome"/>), so a step opts in
        /// to abort-on-timeout only when it is genuinely load-bearing.
        /// </summary>
        StepTimeoutOutcome? TimeoutOutcome { get; }

        /// <summary>Set once a step threw; signals the runner to end the run as faulted.</summary>
        bool Faulted { get; }

        /// <summary>Set once the step was cancelled (timeout / external) rather than finishing normally.</summary>
        bool Cancelled { get; }

        /// <summary>Why the step was cancelled, or null if it has not been cancelled.</summary>
        StepCancelReason? CancellationReason { get; }

        /// <summary>Pre-run gate letting a step defer or abort when its target is not present yet.</summary>
        StepReadiness CheckReadiness();

        /// <summary>Called once when the step becomes the active step.</summary>
        void Begin();

        /// <summary>Advances the step; return <see cref="StepStatus.Finished"/> when its goal is met.</summary>
        StepStatus Advance(float deltaSeconds);

        /// <summary>Called once after a normal finish so the step can tear down its affordances.</summary>
        void Complete();

        /// <summary>Called instead of <see cref="Complete"/> when the step is stopped externally.</summary>
        void Cancel(StepCancelReason reason);
    }
}
