using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// WorldSpace backend anchor. Wraps a <see cref="MeshRenderer"/>; reports the descriptor's
    /// authored <c>Width</c>/<c>Height</c> verbatim because there is no view-space "size" for a
    /// world-space mesh — the texture pixels are independent of the mesh's projected size on
    /// any one camera. Per <see cref="IExplicitSizeAnchor"/>, the service rejects zero-size
    /// descriptors at Acquire time for this anchor type.
    /// </summary>
    public sealed class MeshRendererAnchor : IRenderContextAnchor, IExplicitSizeAnchor
    {
        private readonly MeshRenderer _target;

        public MeshRendererAnchor(MeshRenderer target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public object Target => _target;

        // Width/Height are placeholder reads; the descriptor authoritatively sets RT dimensions
        // for this anchor (verified via IExplicitSizeAnchor enforcement in the service).
        public int PreferredWidth => 0;
        public int PreferredHeight => 0;

        public bool TargetAlive => _target != null;

        public IRenderContextSink CreateSink() => new MeshRendererSink(_target);
    }
}
