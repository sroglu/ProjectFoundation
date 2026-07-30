using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// UI Toolkit sink. Unity 6's <c>style.backgroundImage</c> setter normalizes any
    /// <c>Background</c> whose only populated field is <c>renderTexture</c> to
    /// <c>StyleKeyword.Null</c> (silently dropping the RT). The supported path for runtime
    /// RT-into-UIToolkit is the <see cref="Image"/> element with its <c>image</c> property,
    /// which accepts any <see cref="Texture"/> (including <see cref="RenderTexture"/>).
    ///
    /// This sink attaches an absolute-positioned, stretch-to-fill <see cref="Image"/> child
    /// to the target VisualElement on <see cref="Bind"/> and removes it on <see cref="Unbind"/>.
    /// </summary>
    public sealed class VisualElementSink : IRenderContextSink
    {
        internal const string ChildName = "__renderContextImage";

        private readonly VisualElement _target;
        private Image _attached;
        private bool _bound;

        public VisualElementSink(VisualElement target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public void Bind(RenderTexture rt)
        {
            if (_target == null) return;
            if (_bound)
            {
                if (_attached != null) _attached.image = rt;
                return;
            }

            _attached = new Image
            {
                name = ChildName,
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore,
            };
            _attached.image = rt;
            _attached.style.position = Position.Absolute;
            _attached.style.left = 0;
            _attached.style.right = 0;
            _attached.style.top = 0;
            _attached.style.bottom = 0;
            _target.Add(_attached);
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_target != null && _attached != null)
                _target.Remove(_attached);
            _attached = null;
            _bound = false;
        }
    }
}
