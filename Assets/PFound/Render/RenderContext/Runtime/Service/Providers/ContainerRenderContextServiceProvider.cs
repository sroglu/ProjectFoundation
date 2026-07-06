using System;
using PFound.DependencyContainer;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Resolves <see cref="IRenderContextService"/> via a <c>PFound.DependencyContainer</c>
    /// <see cref="DependencyContainer"/>. Every <see cref="GetService"/> call resolves from the
    /// container, so the service registered on it is honored on each access.
    /// </summary>
    public sealed class ContainerRenderContextServiceProvider : IRenderContextServiceProvider
    {
        private readonly PFound.DependencyContainer.DependencyContainer _container;

        public ContainerRenderContextServiceProvider(PFound.DependencyContainer.DependencyContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public IRenderContextService GetService() => _container.Get<IRenderContextService>();
    }
}
