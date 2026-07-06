using PFound.TweenPresetLibrary.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// A pointer graphic that parks near a target and loops a "tap" bob to draw attention. The bob is a
    /// ping-pong over a TweenPresetLibrary easing curve, so it shares the project's motion vocabulary
    /// without pulling in a full tween runtime.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialHand : MonoBehaviour, ITutorialHand
    {
        [SerializeField] private Ease _ease = Ease.InOutSine;
        [SerializeField] private float _period = 0.8f;
        [SerializeField] private Vector2 _travel = new Vector2(0f, 24f);
        [SerializeField] private Vector2 _offset = new Vector2(28f, -28f);

        private RectTransform _self;
        private RectTransform _target;
        private float _phase;

        private void Awake()
        {
            _self = (RectTransform)transform;
            Hide();
        }

        public void PointAt(RectTransform target)
        {
            _target = target;
            _phase = 0f;
            gameObject.SetActive(target != null);
        }

        public void Hide()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_target == null)
                return;

            _phase += Time.unscaledDeltaTime / Mathf.Max(0.01f, _period);
            float saw = _phase - Mathf.Floor(_phase);
            float pingPong = saw < 0.5f ? saw * 2f : (1f - saw) * 2f;
            float eased = EaseEvaluator.Evaluate(_ease, pingPong);

            Vector2 basePos = (Vector2)_target.position + _offset;
            _self.position = basePos + _travel * eased;
        }
    }
}
