using System;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Stacked-camera render-to-texture service. Resolve via <c>PFound.DependencyContainer</c>.
    /// <c>Acquire</c> validates the descriptor, leases a pooled composite entry, and binds the sink.
    /// </summary>
    public interface IRenderContextService : IDisposable
    {
        IRenderContextHandle Acquire(RenderContextDescriptor descriptor, IRenderContextAnchor anchor);
    }
}
