using PFound.GuidedOnboardingFlow.Core;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Shows a line of guidance in the dialog panel and waits for the player to continue (or auto-advances).
    /// </summary>
    public sealed class DialogStep : TutorialStepBase
    {
        private readonly TutorialRuntimeServices _services;
        private readonly string _message;

        public DialogStep(TutorialRuntimeServices services, string message, float? timeout = null)
        {
            _services = services;
            _message = message;
            Timeout = timeout;
        }

        protected override void OnBegin()
        {
            _services.Dialog.Show(_message);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (_services.AutoAdvance || _services.Dialog.ConsumeContinue())
                return StepStatus.Finished;
            return StepStatus.Running;
        }

        protected override void OnComplete() => _services.Dialog.Hide();

        protected override void OnCancel(StepCancelReason reason) => _services.Dialog.Hide();
    }
}
