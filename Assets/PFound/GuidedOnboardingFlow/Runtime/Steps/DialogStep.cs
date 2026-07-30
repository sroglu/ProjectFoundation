using PFound.GuidedOnboardingFlow.Core;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Shows a line of guidance in the dialog panel with a configurable typewriter reveal and dismiss mode
    /// (tap / auto after N seconds / both), then waits for the player to advance it — or self-completes
    /// under auto-advance once the per-step delay has passed.
    /// </summary>
    public sealed class DialogStep : TutorialStepBase
    {
        private readonly TutorialRuntimeServices _services;
        private readonly string _message;
        private readonly float _typingSpeedCps;
        private readonly DialogDismissMode _dismissMode;
        private readonly float? _autoDismissSeconds;

        public DialogStep(
            TutorialRuntimeServices services,
            string message,
            float typingSpeedCps = 0f,
            DialogDismissMode dismissMode = DialogDismissMode.Tap,
            float? autoDismissSeconds = null,
            float? timeout = null,
            StepTimeoutOutcome? timeoutOutcome = null)
        {
            _services = services;
            _message = message;
            _typingSpeedCps = typingSpeedCps;
            _dismissMode = dismissMode;
            _autoDismissSeconds = autoDismissSeconds;
            Timeout = timeout;
            TimeoutOutcome = timeoutOutcome;
        }

        protected override void OnBegin()
        {
            _services.Dialog.Show(_message, _typingSpeedCps, _dismissMode, _autoDismissSeconds);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (_services.AutoAdvance && Elapsed >= _services.AutoAdvanceDelaySeconds)
                return StepStatus.Finished;
            if (_services.Dialog.ConsumeContinue())
                return StepStatus.Finished;
            return StepStatus.Running;
        }

        protected override void OnComplete() => _services.Dialog.Hide();

        protected override void OnCancel(StepCancelReason reason) => _services.Dialog.Hide();
    }
}
