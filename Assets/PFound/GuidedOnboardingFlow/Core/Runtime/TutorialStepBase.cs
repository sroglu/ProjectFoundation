using System;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Base step that keeps concrete steps tiny: it wraps every lifecycle call in try/catch (turning a
    /// thrown exception into a <see cref="Faulted"/> flag the runner acts on) and enforces the per-step
    /// <see cref="Timeout"/> by cancelling the step once its elapsed budget is spent. Subclasses override
    /// the On* template hooks and never touch the flags directly.
    /// </summary>
    public abstract class TutorialStepBase : ITutorialStep
    {
        private float _elapsed;
        private bool _live;

        public float? Timeout { get; protected set; }
        public StepTimeoutOutcome? TimeoutOutcome { get; protected set; }
        public bool Faulted { get; private set; }
        public bool Cancelled { get; private set; }
        public StepCancelReason? CancellationReason { get; private set; }
        public Exception Fault { get; private set; }

        public StepReadiness CheckReadiness()
        {
            try
            {
                return OnCheckReadiness();
            }
            catch (Exception e)
            {
                Capture(e);
                return StepReadiness.Unreachable;
            }
        }

        public void Begin()
        {
            _elapsed = 0f;
            _live = true;
            try
            {
                OnBegin();
            }
            catch (Exception e)
            {
                Capture(e);
            }
        }

        public StepStatus Advance(float deltaSeconds)
        {
            if (!_live || Faulted)
                return StepStatus.Finished;

            _elapsed += deltaSeconds;
            if (Timeout.HasValue && _elapsed >= Timeout.Value)
            {
                Cancel(StepCancelReason.TimedOut);
                return StepStatus.Finished;
            }

            try
            {
                return OnAdvance(deltaSeconds);
            }
            catch (Exception e)
            {
                Capture(e);
                return StepStatus.Finished;
            }
        }

        public void Complete()
        {
            if (!_live)
                return;
            _live = false;
            try
            {
                OnComplete();
            }
            catch (Exception e)
            {
                Capture(e);
            }
        }

        public void Cancel(StepCancelReason reason)
        {
            if (!_live)
                return;
            _live = false;
            Cancelled = true;
            CancellationReason = reason;
            try
            {
                OnCancel(reason);
            }
            catch (Exception e)
            {
                Capture(e);
            }
        }

        /// <summary>Seconds elapsed since <see cref="Begin"/>; exposed for subclasses that pace themselves.</summary>
        protected float Elapsed => _elapsed;

        private void Capture(Exception e)
        {
            Faulted = true;
            Fault = e;
        }

        protected virtual StepReadiness OnCheckReadiness() => StepReadiness.Ready;

        protected abstract void OnBegin();

        protected abstract StepStatus OnAdvance(float deltaSeconds);

        protected virtual void OnComplete()
        {
        }

        protected virtual void OnCancel(StepCancelReason reason)
        {
        }
    }
}
