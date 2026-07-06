using UnityEngine;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Dims the whole screen and punches a soft spotlight over the highlighted target. The dimming and the
    /// cutout are done in one pass by the spotlight shader: the material is fed the target's rect in
    /// viewport space plus a softness, and the shader keeps that region bright while darkening the rest.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HighlightMask : MonoBehaviour, IHighlightMask
    {
        private static readonly int CenterId = Shader.PropertyToID("_CutoutCenter");
        private static readonly int SizeId = Shader.PropertyToID("_CutoutSize");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        [SerializeField] private Image _overlay;
        [SerializeField] private Camera _uiCamera;
        [Range(0f, 0.5f)] [SerializeField] private float _softness = 0.02f;
        [SerializeField] private Vector2 _padding = new Vector2(16f, 16f);

        private Material _material;
        private RectTransform _target;

        private void Awake()
        {
            if (_overlay != null)
            {
                _material = Instantiate(_overlay.material);
                _overlay.material = _material;
            }
            Clear();
        }

        public void Highlight(RectTransform target)
        {
            _target = target;
            gameObject.SetActive(target != null);
            Reproject();
        }

        public void Clear()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target != null)
                Reproject();
        }

        private void Reproject()
        {
            if (_material == null || _target == null)
                return;

            Vector3[] corners = new Vector3[4];
            _target.GetWorldCorners(corners);

            Vector2 min = ToViewport(corners[0]);
            Vector2 max = ToViewport(corners[2]);

            // Pad the cutout a little so the highlight breathes around the element.
            Vector2 padVp = new Vector2(_padding.x / Screen.width, _padding.y / Screen.height);
            min -= padVp;
            max += padVp;

            _material.SetVector(CenterId, (min + max) * 0.5f);
            _material.SetVector(SizeId, max - min);
            _material.SetFloat(SoftnessId, _softness);
        }

        private Vector2 ToViewport(Vector3 world)
        {
            Vector3 screen = _uiCamera != null
                ? _uiCamera.WorldToScreenPoint(world)
                : world; // Overlay canvas: world corners are already in screen space.
            return new Vector2(screen.x / Screen.width, screen.y / Screen.height);
        }
    }
}
