using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// UI Toolkit backend anchor. Wraps a <see cref="VisualElement"/>; reports its resolved style
    /// width/height as integer pixels. NaN (pre-first-layout) or DisplayStyle.None collapses to 0.
    /// </summary>
    public sealed class VisualElementAnchor : IRenderContextAnchor
    {
        private readonly VisualElement _target;

        public VisualElementAnchor(VisualElement target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public object Target => _target;

        public int PreferredWidth
        {
            get
            {
                if (_target == null) return 0;
                if (_target.resolvedStyle.display == DisplayStyle.None) return 0;
                float w = _target.resolvedStyle.width;
                if (float.IsNaN(w) || w <= 0f) return 0;
                return Mathf.RoundToInt(w);
            }
        }

        public int PreferredHeight
        {
            get
            {
                if (_target == null) return 0;
                if (_target.resolvedStyle.display == DisplayStyle.None) return 0;
                float h = _target.resolvedStyle.height;
                if (float.IsNaN(h) || h <= 0f) return 0;
                return Mathf.RoundToInt(h);
            }
        }

        public bool TargetAlive => _target != null && _target.panel != null;

        public IRenderContextSink CreateSink() => new VisualElementSink(_target);
    }
}
