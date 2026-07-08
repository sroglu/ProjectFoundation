using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Full-screen transparent <see cref="Graphic"/> that catches raycasts. When a hole is set over a
    /// target, pointer press/release/click that actually land on the target (or one of its children) are
    /// re-dispatched to that object's own handlers — so the game's button really fires, complete with
    /// pressed-state tint — and latch a one-shot interaction the driving step consumes. A raycast hit-test
    /// (not a plain rect test) is used deliberately: rect pass-through drifts off the button the moment a
    /// step adds an anchor offset/padding. Pointer events elsewhere are absorbed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class InputBlocker : Graphic, IInputBlocker,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _hole;
        private bool _interacted;
        private readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>(8);

        protected override void Awake()
        {
            base.Awake();
            color = new Color(0f, 0f, 0f, 0f);
            raycastTarget = true;
        }

        // Nothing to draw: a zero-alpha, geometry-free graphic that still receives raycasts.
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();

        public void Enable()
        {
            _interacted = false;
            gameObject.SetActive(true);
        }

        public void Disable()
        {
            _hole = null;
            gameObject.SetActive(false);
        }

        public void SetHole(RectTransform target)
        {
            _hole = target;
            _interacted = false;
        }

        public bool ConsumeInteraction()
        {
            if (!_interacted)
                return false;
            _interacted = false;
            return true;
        }

        // Down + Up are forwarded so a Selectable's pressed-color tint fires while the player touches the
        // highlighted control; Click is what latches completion + drives the button's onClick.
        public void OnPointerDown(PointerEventData eventData) =>
            ForwardIfOnTarget(eventData, ExecuteEvents.pointerDownHandler, latch: false);

        public void OnPointerUp(PointerEventData eventData) =>
            ForwardIfOnTarget(eventData, ExecuteEvents.pointerUpHandler, latch: false);

        public void OnPointerClick(PointerEventData eventData) =>
            ForwardIfOnTarget(eventData, ExecuteEvents.pointerClickHandler, latch: true);

        private void ForwardIfOnTarget<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> dispatch, bool latch)
            where T : IEventSystemHandler
        {
            if (_hole == null || EventSystem.current == null)
                return;

            _raycastBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, _raycastBuffer);
            for (int i = 0; i < _raycastBuffer.Count; i++)
            {
                GameObject hit = _raycastBuffer[i].gameObject;
                if (hit == null)
                    continue;
                if (hit == _hole.gameObject || hit.transform.IsChildOf(_hole))
                {
                    // Re-run the real handler so the game's own logic fires, then walk up to the nearest
                    // object that actually handles this event (mirrors EventSystem dispatch on the target).
                    GameObject handler = ExecuteEvents.GetEventHandler<T>(hit);
                    ExecuteEvents.Execute(handler != null ? handler : _hole.gameObject, eventData, dispatch);
                    if (latch)
                        _interacted = true;
                    return;
                }
            }
            // Inside the blocker but missed the target → absorb (dim-background tap).
        }
    }
}
