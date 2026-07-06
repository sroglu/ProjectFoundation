namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// The three observable positions in a run's life: it exists but has not begun, it is currently
    /// driving steps, or it has stopped for good. Derived from first principles as the minimal set that
    /// still lets callers gate "can I start / is it live / is it over".
    /// </summary>
    public enum RunnerPhase
    {
        Pending,
        Active,
        Ended
    }

    /// <summary>
    /// Why a run reached its terminal <see cref="RunnerPhase.Ended"/> phase. Each member answers a
    /// distinct "how did this stop" question so callers (persistence, analytics, UI) can branch:
    /// finished every step (<see cref="Completed"/>); the app is tearing down (<see cref="Shutdown"/>);
    /// the entry conditions stopped holding mid-run (<see cref="Invalidated"/>); a step threw and the run
    /// could not continue (<see cref="Faulted"/>); the player chose to leave (<see cref="Skipped"/>).
    /// </summary>
    public enum TutorialOutcome
    {
        Completed,
        Shutdown,
        Invalidated,
        Faulted,
        Skipped
    }

    /// <summary>
    /// The verdict of a step's pre-run achievability check. <see cref="Ready"/> — proceed now;
    /// <see cref="Deferred"/> — the target is not present yet, hold and re-ask next tick;
    /// <see cref="Unreachable"/> — the target can no longer appear, give up on the run.
    /// </summary>
    public enum StepReadiness
    {
        Ready,
        Deferred,
        Unreachable
    }

    /// <summary>Whether a step is still working or has reached its end for this tick.</summary>
    public enum StepStatus
    {
        Running,
        Finished
    }

    /// <summary>
    /// Why a step was told to stop before completing on its own. Distinct from a normal finish so a step
    /// can tear down differently: it ran out of time (<see cref="TimedOut"/>); the player skipped the
    /// whole tutorial (<see cref="UserSkipped"/>); the app is shutting down (<see cref="Shutdown"/>); the
    /// run's entry conditions lapsed (<see cref="RunAborted"/>).
    /// </summary>
    public enum StepCancelReason
    {
        TimedOut,
        UserSkipped,
        Shutdown,
        RunAborted
    }

    /// <summary>
    /// How a tutorial is allowed to begin. <see cref="Manual"/> — only via an explicit Start call;
    /// <see cref="Automatic"/> — its trigger is polled and may fire on its own.
    /// </summary>
    public enum TriggerMode
    {
        Manual,
        Automatic
    }
}
