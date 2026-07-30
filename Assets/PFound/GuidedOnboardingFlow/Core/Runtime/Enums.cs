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
    /// <see cref="Unreachable"/> — the target can no longer appear, give up on the run;
    /// <see cref="SkipAndAdvance"/> — this step is moot right now, drop it and move to the next
    /// (e.g. the thing it would teach is already done); <see cref="GoBackOne"/> — a precondition
    /// established by the previous step lapsed, rewind one step and re-run it.
    /// </summary>
    public enum StepReadiness
    {
        Ready,
        Deferred,
        Unreachable,
        SkipAndAdvance,
        GoBackOne
    }

    /// <summary>
    /// What the run should do when a step's optional timeout budget is spent. <see cref="Advance"/> —
    /// treat the timeout as "good enough", tear the step down and move to the next step;
    /// <see cref="AbortRun"/> — the step was load-bearing, so a timeout means the run can no longer be
    /// completed and it ends as <see cref="TutorialOutcome.Invalidated"/>.
    /// </summary>
    public enum StepTimeoutOutcome
    {
        Advance,
        AbortRun
    }

    /// <summary>
    /// How often a tutorial is allowed to play. <see cref="OnceAccount"/> — once, ever, for this
    /// player (persisted across launches); <see cref="OnceSession"/> — once per app session (forgotten
    /// on restart); <see cref="Repeatable"/> — never marked complete, so it can fire again and again.
    /// </summary>
    public enum ReplayPolicy
    {
        OnceAccount,
        OnceSession,
        Repeatable
    }

    /// <summary>Severity of a diagnostic line so hosts can route it to the right console channel.</summary>
    public enum LogSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// How chatty the manager's diagnostics are. <see cref="Errors"/> — only problems (dup/empty ids,
    /// faults); <see cref="Lifecycle"/> — also start/step/end lines; <see cref="Verbose"/> — everything,
    /// including per-tick decisions. Errors and warnings are emitted at every level.
    /// </summary>
    public enum LogVerbosity
    {
        Errors,
        Lifecycle,
        Verbose
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
