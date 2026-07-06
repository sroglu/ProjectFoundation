using System;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Returns a fixed pre-constructed <see cref="IRenderContextService"/> instance.
    /// Use when the host owns the service lifetime directly (no DependencyContainer):
    /// <code>RenderContextResolver.Use(new SingletonRenderContextServiceProvider(svc));</code>
    /// or via convenience <c>RenderContextResolver.Use(svc)</c>.
    /// </summary>
    public sealed class SingletonRenderContextServiceProvider : IRenderContextServiceProvider
    {
        private readonly IRenderContextService _service;

        public SingletonRenderContextServiceProvider(IRenderContextService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public IRenderContextService GetService() => _service;
    }
}
