using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Focuses the player on a tagged UI element: dims the screen with a (optionally sprite-shaped)
    /// spotlight over it, floats the hand toward it with an optional hint caption, and opens a
    /// click-through hole. Completes when the player interacts through the hole. Its achievability check
    /// resolves the target from the anchor registry, falling back to <c>GameObject.Find</c> by tag, and
    /// defers while neither is present yet so the runner waits for a screen to spawn the target. Per-anchor
    /// offset/padding/mask hints are applied, and if the target is lost mid-step the run aborts rather than
    /// leaving a dangling overlay. Under auto-advance it synthesizes a real pointer click on the target
    /// after the per-step delay, so the game's own click handler fires.
    /// </summary>
    public sealed class FocusStep : TutorialStepBase
    {
        private readonly TutorialRuntimeServices _services;
        private readonly string _anchorTag;
        private readonly string _hint;
        private readonly string _maskSpriteName;

        private RectTransform _target;
        private Vector2 _offset;
        private Vector2 _padding;
        private string _effectiveMask;
        private bool _clicked;

        public FocusStep(
            TutorialRuntimeServices services,
            string anchorTag,
            string hint = null,
            string maskSpriteName = null,
            float? timeout = null,
            StepTimeoutOutcome? timeoutOutcome = null)
        {
            _services = services;
            _anchorTag = anchorTag;
            _hint = hint;
            _maskSpriteName = maskSpriteName;
            Timeout = timeout;
            TimeoutOutcome = timeoutOutcome;
        }

        protected override StepReadiness OnCheckReadiness()
        {
            return Resolve() ? StepReadiness.Ready : StepReadiness.Deferred;
        }

        protected override void OnBegin()
        {
            _clicked = false;
            _services.InputBlocker.Enable();
            _services.InputBlocker.SetHole(_target);
            _services.Highlight.Highlight(_target, _effectiveMask, _padding);
            _services.Hand.PointAt(_target, _hint, _offset);
        }

        protected override StepStatus OnAdvance(float deltaSeconds)
        {
            // Target destroyed mid-step (e.g. its screen closed): tear down our own affordances so nothing
            // is left dangling, then abort the run.
            if (_target == null)
            {
                Teardown();
                throw new TutorialTargetLostException(_anchorTag);
            }

            if (_services.AutoAdvance && Elapsed >= _services.AutoAdvanceDelaySeconds)
            {
                SynthesizeClick();
                return StepStatus.Finished;
            }

            if (_clicked || _services.InputBlocker.ConsumeInteraction())
                return StepStatus.Finished;
            return StepStatus.Running;
        }

        protected override void OnComplete() => Teardown();

        protected override void OnCancel(StepCancelReason reason) => Teardown();

        private bool Resolve()
        {
            // Preferred: a registered anchor, with its offset/padding/mask hints.
            if (_services.Anchors.TryResolveHandle(_anchorTag, out TutorialAnchorHandle handle))
            {
                _target = handle.Rect;
                _offset = handle.Offset;
                _padding = handle.Padding;
                _effectiveMask = !string.IsNullOrEmpty(handle.MaskSpriteName) ? handle.MaskSpriteName : _maskSpriteName;
                return true;
            }

            // Fallback: locate a live GameObject by name (tag) and use it as a plain target.
            GameObject found = GameObject.Find(_anchorTag);
            RectTransform rect = found != null ? found.transform as RectTransform : null;
            if (rect != null)
            {
                _target = rect;
                _offset = Vector2.zero;
                _padding = Vector2.zero;
                _effectiveMask = _maskSpriteName;
                return true;
            }

            _target = null;
            return false;
        }

        private void SynthesizeClick()
        {
            // Fire a real pointer click on the target so the game's own onClick / IPointerClickHandler runs.
            if (EventSystem.current == null || _target == null)
                return;
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, _target.position)
            };
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_target.gameObject);
            ExecuteEvents.Execute(handler != null ? handler : _target.gameObject, data, ExecuteEvents.pointerClickHandler);
            _clicked = true;
        }

        private void Teardown()
        {
            _services.Hand.Hide();
            _services.Highlight.Clear();
            _services.InputBlocker.Disable();
        }
    }

    /// <summary>Thrown when a focus step's target disappears mid-step, aborting the run instead of hanging.</summary>
    public sealed class TutorialTargetLostException : System.Exception
    {
        public TutorialTargetLostException(string tag)
            : base("Focus target '" + tag + "' was lost during the step.")
        {
        }
    }
}
