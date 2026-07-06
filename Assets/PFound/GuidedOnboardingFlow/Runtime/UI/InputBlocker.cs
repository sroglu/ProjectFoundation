using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Full-screen transparent <see cref="Graphic"/> that catches raycasts. When a hole is set, a pointer
    /// press whose screen position falls inside the target's rect is treated as an interaction and passed
    /// through; presses elsewhere are simply absorbed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class InputBlocker : Graphic, IInputBlocker, IPointerClickHandler
    {
        private RectTransform _hole;
        private bool _interacted;

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_hole == null)
                return;
            if (RectTransformUtility.RectangleContainsScreenPoint(_hole, eventData.position, eventData.pressEventCamera))
                _interacted = true;
        }
    }
}
