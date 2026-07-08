using System;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Drives one <see cref="TutorialInstance"/>'s steps in order. Lifecycle: constructed <see cref="RunnerPhase.Pending"/>,
    /// <see cref="Begin"/> moves it to <see cref="RunnerPhase.Active"/>, and it settles in <see cref="RunnerPhase.Ended"/>
    /// with a <see cref="TutorialOutcome"/>. Each tick it (1) confirms the run's preconditions still hold,
    /// (2) gates the current step on its achievability check — deferring while the target is missing or
    /// aborting the run if it is unreachable — then (3) advances the step, promoting to the next on a
    /// normal finish. External <see cref="Skip"/> / <see cref="NotifyShutdown"/> stop it cleanly, and any
    /// step fault ends the run as <see cref="TutorialOutcome.Faulted"/>.
    /// </summary>
    public sealed class TutorialRunner
    {
        private readonly TutorialInstance _tutorial;
        private readonly Func<bool> _preconditionsHold;

        private int _index;
        private bool _stepEntered;

        public RunnerPhase Phase { get; private set; }
        public TutorialOutcome? Outcome { get; private set; }

        public TutorialId Id => _tutorial.Id;
        public int StepIndex => _index;
        public int StepCount => _tutorial.Steps.Count;

        /// <summary>The step currently being driven, or null when pending/ended.</summary>
        public ITutorialStep CurrentStep =>
            Phase == RunnerPhase.Active && _index < _tutorial.Steps.Count ? _tutorial.Steps[_index] : null;

        /// <summary>Raised when a step's <see cref="ITutorialStep.Begin"/> is invoked.</summary>
        public event Action<ITutorialStep> StepStarted;

        /// <summary>Raised once when the run reaches <see cref="RunnerPhase.Ended"/>.</summary>
        public event Action<TutorialRunner> Ended;

        public TutorialRunner(TutorialInstance tutorial, Func<bool> preconditionsHold = null)
        {
            _tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
            _preconditionsHold = preconditionsHold;
            Phase = RunnerPhase.Pending;
        }

        /// <summary>Transitions from pending to active. An empty tutorial ends immediately as completed.</summary>
        public void Begin()
        {
            if (Phase != RunnerPhase.Pending)
                throw new InvalidOperationException("TutorialRunner can only begin once, from the pending phase.");

            if (!PreconditionsHold())
            {
                Stop(TutorialOutcome.Invalidated);
                return;
            }

            Phase = RunnerPhase.Active;
            _index = 0;
            _stepEntered = false;

            if (_tutorial.Steps.Count == 0)
                Stop(TutorialOutcome.Completed);
        }

        /// <summary>
        /// Advances the active run by one time slice. Steps that finish instantly (or on timeout) chain
        /// into the next step within the same tick, so a run of zero-duration steps doesn't stall a frame
        /// each; the loop is bounded by the step count as a safety net against a pathological step.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (Phase != RunnerPhase.Active)
                return;

            if (!PreconditionsHold())
            {
                CancelCurrent(StepCancelReason.RunAborted);
                Stop(TutorialOutcome.Invalidated);
                return;
            }

            // The guard bounds a single tick against pathological readiness verdicts (a step that keeps
            // asking to skip/rewind) and instant-finishing steps chaining forward. Two visits per step
            // are enough headroom for a readiness re-route followed by a real entry.
            int guard = (_tutorial.Steps.Count + 1) * 2;
            while (Phase == RunnerPhase.Active && guard-- > 0)
            {
                ITutorialStep step = _tutorial.Steps[_index];

                if (!_stepEntered)
                {
                    StepReadiness readiness = step.CheckReadiness();
                    switch (readiness)
                    {
                        case StepReadiness.Deferred:
                            return;

                        case StepReadiness.Unreachable:
                            Stop(TutorialOutcome.Invalidated);
                            return;

                        case StepReadiness.SkipAndAdvance:
                            if (AdvanceIndex())
                                return; // reached the end → completed
                            continue;   // re-evaluate the now-current step this same tick

                        case StepReadiness.GoBackOne:
                            if (_index > 0)
                                _index--;
                            _stepEntered = false;
                            continue;   // re-evaluate the earlier step

                        case StepReadiness.Ready:
                        default:
                            break;
                    }

                    step.Begin();
                    _stepEntered = true;
                    StepStarted?.Invoke(step);

                    if (step.Faulted)
                    {
                        Stop(TutorialOutcome.Faulted);
                        return;
                    }
                }

                StepStatus status = step.Advance(deltaSeconds);
                if (status == StepStatus.Running)
                    return;

                if (step.Faulted)
                {
                    Stop(TutorialOutcome.Faulted);
                    return;
                }

                if (step.Cancelled)
                {
                    // A step that cancelled itself on timeout gets to decide the run's fate: its own
                    // TimeoutOutcome wins, falling back to the tutorial's default. AbortRun ends the run
                    // as Invalidated; Advance (the default) just moves on to the next step.
                    if (step.CancellationReason == StepCancelReason.TimedOut)
                    {
                        StepTimeoutOutcome outcome = step.TimeoutOutcome ?? _tutorial.DefaultTimeoutOutcome;
                        if (outcome == StepTimeoutOutcome.AbortRun)
                        {
                            Stop(TutorialOutcome.Invalidated);
                            return;
                        }
                    }
                }
                else
                {
                    step.Complete();
                    if (step.Faulted)
                    {
                        Stop(TutorialOutcome.Faulted);
                        return;
                    }
                }

                if (AdvanceIndex())
                    return;
            }
        }

        /// <summary>Player-initiated exit: cancels the current step and ends the run as skipped.</summary>
        public void Skip()
        {
            if (Phase != RunnerPhase.Active)
                return;
            CancelCurrent(StepCancelReason.UserSkipped);
            Stop(TutorialOutcome.Skipped);
        }

        /// <summary>App-teardown exit: cancels the current step and ends the run as shutdown.</summary>
        public void NotifyShutdown()
        {
            if (Phase != RunnerPhase.Active)
                return;
            CancelCurrent(StepCancelReason.Shutdown);
            Stop(TutorialOutcome.Shutdown);
        }

        /// <summary>Move to the next step; returns true (and ends the run as Completed) if there is none.</summary>
        private bool AdvanceIndex()
        {
            _index++;
            _stepEntered = false;
            if (_index >= _tutorial.Steps.Count)
            {
                Stop(TutorialOutcome.Completed);
                return true;
            }
            return false;
        }

        private bool PreconditionsHold() => _preconditionsHold == null || _preconditionsHold();

        private void CancelCurrent(StepCancelReason reason)
        {
            if (_stepEntered && _index < _tutorial.Steps.Count)
                _tutorial.Steps[_index].Cancel(reason);
        }

        private void Stop(TutorialOutcome outcome)
        {
            Phase = RunnerPhase.Ended;
            Outcome = outcome;
            Ended?.Invoke(this);
        }
    }
}
