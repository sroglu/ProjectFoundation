using System;
using UnityEngine;
using UnityEngine.UI;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// uGUI sink. Captures <see cref="RawImage.texture"/> on first <see cref="Bind"/>,
    /// restores it on <see cref="Unbind"/>. Idempotent.
    /// </summary>
    public sealed class RawImageSink : IRenderContextSink
    {
        private readonly RawImage _target;
        private Texture _captured;
        private bool _bound;

        public RawImageSink(RawImage target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public void Bind(RenderTexture rt)
        {
            if (_target == null) return;
            if (!_bound)
            {
                _captured = _target.texture;
                _bound = true;
            }
            _target.texture = rt;
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_target != null) _target.texture = _captured;
            _captured = null;
            _bound = false;
        }
    }
}
