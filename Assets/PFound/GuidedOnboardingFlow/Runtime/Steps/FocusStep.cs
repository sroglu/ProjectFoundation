using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Focuses the player on a tagged UI element: dims the screen with a spotlight over it, floats the
    /// hand toward it, and opens a click-through hole. Completes when the player interacts through the
    /// hole (or immediately under auto-advance). Its achievability check defers while the anchor is not
    /// yet registered, so the runner waits for a screen to spawn the target rather than failing.
    /// </summary>
    public sealed class FocusStep : TutorialStepBase
    {
        private readonly TutorialRuntimeServices _services;
        private readonly string _anchorTag;

        private RectTransform _target;

        public FocusStep(TutorialRuntimeServices services, string anchorTag, float? timeout = null)
        {
            _services = services;
            _anchorTag = anchorTag;
            Timeout = timeout;
        }

        protected override StepReadiness OnCheckReadiness()
        {
            return _services.Anchors.TryResolve(_anchorTag, out _target)
                ? StepReadiness.Ready
                : StepReadiness.Deferred;
        }

        protected override void OnBegin()
        {
            _services.InputBlocker.Enable();
            _services.InputBlocker.SetHole(_target);
            _services.Highlight.Highlight(_target);
            _services.Hand.PointAt(_target);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            if (_services.AutoAdvance || _services.InputBlocker.ConsumeInteraction())
                return StepStatus.Finished;
            return StepStatus.Running;
        }

        protected override void OnComplete() => Teardown();

        protected override void OnCancel(StepCancelReason reason) => Teardown();

        private void Teardown()
        {
            _services.Hand.Hide();
            _services.Highlight.Clear();
            _services.InputBlocker.Disable();
        }
    }
}
