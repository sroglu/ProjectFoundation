using PFound.TweenPresetLibrary.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// A pointer graphic that parks near a target and loops a "tap" bob to draw attention, optionally with
    /// a short hint caption beside it. The bob is a ping-pong over a TweenPresetLibrary easing curve, so it
    /// shares the project's motion vocabulary without pulling in a full tween runtime. A per-anchor offset
    /// nudges only the pointer (e.g. a "look up" arrow) without moving the highlight or click region.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialHand : MonoBehaviour, ITutorialHand
    {
        [SerializeField] private Ease _ease = Ease.InOutSine;
        [SerializeField] private float _period = 0.8f;
        [SerializeField] private Vector2 _travel = new Vector2(0f, 24f);
        [SerializeField] private Vector2 _baseOffset = new Vector2(28f, -28f);
        [Tooltip("Optional caption shown next to the pointer.")]
        [SerializeField] private Text _hintLabel;

        private RectTransform _self;
        private RectTransform _target;
        private Vector2 _anchorOffset;
        private float _phase;

        private void Awake()
        {
            _self = (RectTransform)transform;
            Hide();
        }

        public void PointAt(RectTransform target) => PointAt(target, null, Vector2.zero);

        public void PointAt(RectTransform target, string hint, Vector2 offset)
        {
            _target = target;
            _anchorOffset = offset;
            _phase = 0f;
            if (_hintLabel != null)
            {
                _hintLabel.text = hint ?? string.Empty;
                _hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            }
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

            Vector2 basePos = (Vector2)_target.position + _baseOffset + _anchorOffset;
            _self.position = basePos + _travel * eased;
        }
    }
}
