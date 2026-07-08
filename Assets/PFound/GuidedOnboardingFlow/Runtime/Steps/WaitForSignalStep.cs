using PFound.GuidedOnboardingFlow.Core;
using PFound.Signaling;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Pauses the tutorial until a specific PFound signal is emitted — e.g. "wait until the player closes
    /// this menu". Subscribes on begin and always detaches its listener on complete/cancel.
    /// </summary>
    public sealed class WaitForSignalStep<TSignal> : TutorialStepBase
        where TSignal : SignalBase
    {
        private readonly SignalTracker _signals;
        private readonly TutorialRuntimeServices _services;
        private bool _received;

        public WaitForSignalStep(SignalTracker signals, TutorialRuntimeServices services = null, float? timeout = null)
        {
            _signals = signals;
            _services = services;
            Timeout = timeout;
        }

        protected override void OnBegin()
        {
            _received = false;
            _signals.AddListener<TSignal>(OnSignal);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            // Under a scripted fast-forward we can't emit the game's signal for it, so self-complete once
            // the auto-advance delay has passed rather than stalling the whole run.
            if (_services != null && _services.AutoAdvance && Elapsed >= _services.AutoAdvanceDelaySeconds)
                return StepStatus.Finished;
            return _received ? StepStatus.Finished : StepStatus.Running;
        }

        protected override void OnComplete() => Detach();

        protected override void OnCancel(StepCancelReason reason) => Detach();

        private void OnSignal(SignalKey key) => _received = true;

        private void Detach() => _signals.RemoveListener<TSignal>(OnSignal);
    }
}
