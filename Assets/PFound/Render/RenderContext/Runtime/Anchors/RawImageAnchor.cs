using System;
using UnityEngine;
using UnityEngine.UI;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// uGUI backend anchor. Wraps a <see cref="RawImage"/>; reports its post-CanvasScaler
    /// effective pixel size via <see cref="RectTransformUtility.PixelAdjustRect"/>.
    /// </summary>
    public sealed class RawImageAnchor : IRenderContextAnchor
    {
        private readonly RawImage _target;

        public RawImageAnchor(RawImage target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public object Target => _target;

        public int PreferredWidth => GetSize().x;
        public int PreferredHeight => GetSize().y;

        public bool TargetAlive => _target != null;

        public IRenderContextSink CreateSink() => new RawImageSink(_target);

        private Vector2Int GetSize()
        {
            if (_target == null) return Vector2Int.zero;
            var canvas = _target.canvas;
            if (canvas == null)
            {
                var size = _target.rectTransform.rect.size;
                return new Vector2Int(Mathf.Max(0, Mathf.RoundToInt(size.x)), Mathf.Max(0, Mathf.RoundToInt(size.y)));
            }
            var rect = RectTransformUtility.PixelAdjustRect(_target.rectTransform, canvas);
            return new Vector2Int(Mathf.Max(0, Mathf.RoundToInt(rect.width)), Mathf.Max(0, Mathf.RoundToInt(rect.height)));
        }
    }
}
