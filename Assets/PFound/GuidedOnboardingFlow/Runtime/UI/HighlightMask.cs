using System;
using UnityEngine;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Dims the whole screen and punches a soft spotlight over the highlighted target. The dimming and the
    /// cutout are done in one pass by the spotlight shader: the material is fed the target's rect in
    /// viewport space plus a softness, and the shader keeps that region bright while darkening the rest.
    /// The cutout can also be shaped by a named mask sprite (rounded/circular/custom) and grown by extra
    /// per-anchor padding on top of the base padding.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HighlightMask : MonoBehaviour, IHighlightMask
    {
        private static readonly int CenterId = Shader.PropertyToID("_CutoutCenter");
        private static readonly int SizeId = Shader.PropertyToID("_CutoutSize");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int ShapeId = Shader.PropertyToID("_CutoutShape");     // 0 = rect, 1 = sprite
        private static readonly int MaskTexId = Shader.PropertyToID("_CutoutMask");

        [SerializeField] private Image _overlay;
        [SerializeField] private Camera _uiCamera;
        [Range(0f, 0.5f)] [SerializeField] private float _softness = 0.02f;
        [SerializeField] private Vector2 _padding = new Vector2(16f, 16f);
        [Tooltip("Named mask sprites a focus step / anchor can select by name to shape the cutout.")]
        [SerializeField] private MaskSpriteEntry[] _maskSprites;

        private Material _material;
        private RectTransform _target;
        private Vector2 _extraPadding;

        private void Awake()
        {
            if (_overlay != null)
            {
                _material = Instantiate(_overlay.material);
                _overlay.material = _material;
            }
            Clear();
        }

        public void Highlight(RectTransform target) => Highlight(target, null, Vector2.zero);

        public void Highlight(RectTransform target, string maskSpriteName, Vector2 padding)
        {
            _target = target;
            _extraPadding = padding;
            ApplyMaskShape(maskSpriteName);
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

        private void ApplyMaskShape(string maskSpriteName)
        {
            if (_material == null)
                return;

            if (!string.IsNullOrEmpty(maskSpriteName) && TryFindMask(maskSpriteName, out Sprite sprite))
            {
                _material.SetFloat(ShapeId, 1f);
                if (sprite.texture != null)
                    _material.SetTexture(MaskTexId, sprite.texture);
            }
            else
            {
                _material.SetFloat(ShapeId, 0f);
            }
        }

        private void Reproject()
        {
            if (_material == null || _target == null)
                return;

            Vector3[] corners = new Vector3[4];
            _target.GetWorldCorners(corners);

            Vector2 min = ToViewport(corners[0]);
            Vector2 max = ToViewport(corners[2]);

            // Pad the cutout so the highlight breathes around the element; anchors add their own padding.
            Vector2 pad = _padding + _extraPadding;
            Vector2 padVp = new Vector2(pad.x / Screen.width, pad.y / Screen.height);
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

        private bool TryFindMask(string maskName, out Sprite sprite)
        {
            sprite = null;
            if (_maskSprites == null)
                return false;
            for (int i = 0; i < _maskSprites.Length; i++)
            {
                MaskSpriteEntry entry = _maskSprites[i];
                if (entry != null && entry.sprite != null && string.Equals(entry.name, maskName, StringComparison.Ordinal))
                {
                    sprite = entry.sprite;
                    return true;
                }
            }
            return false;
        }

        [Serializable]
        public sealed class MaskSpriteEntry
        {
            public string name;
            public Sprite sprite;
        }
    }
}
