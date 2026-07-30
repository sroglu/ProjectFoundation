using UnityEngine;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// The real <see cref="ITweenTarget"/>: a Transform (anchored position when it is a RectTransform) plus an
    /// OPTIONAL CanvasGroup for fade. A target with no CanvasGroup simply has no fade surface — its Alpha reads 1 and
    /// ignores writes — so transform-only presets work without one. Thin by design; the easing lives in the player.
    /// </summary>
    public sealed class ComponentTweenTarget : ITweenTarget
    {
        private readonly Transform _transform;
        private readonly RectTransform _rect;
        private readonly CanvasGroup _canvasGroup;

        public ComponentTweenTarget(Transform transform, CanvasGroup canvasGroup = null)
        {
            _transform = transform;
            _rect = transform as RectTransform;
            _canvasGroup = canvasGroup;
        }

        public float Alpha
        {
            get => _canvasGroup ? _canvasGroup.alpha : 1f;
            set { if (_canvasGroup) _canvasGroup.alpha = value; }
        }

        public Vector3 Scale
        {
            get => _transform.localScale;
            set => _transform.localScale = value;
        }

        public Vector2 Position
        {
            get => _rect ? _rect.anchoredPosition : (Vector2)_transform.localPosition;
            set
            {
                if (_rect) _rect.anchoredPosition = value;
                else { var p = _transform.localPosition; _transform.localPosition = new Vector3(value.x, value.y, p.z); }
            }
        }

        public Vector3 EulerAngles
        {
            get => _transform.localEulerAngles;
            set => _transform.localEulerAngles = value;
        }
    }
}
