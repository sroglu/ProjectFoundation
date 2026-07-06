using System;
using PFound.DependencyContainer;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Convenience bootstrap for the most common host wiring: construct a
    /// <see cref="RenderContextService"/>, register it as a singleton on a
    /// <c>PFound.DependencyContainer</c>, AND configure <see cref="RenderContextResolver"/>
    /// to resolve via that container. One call covers the typical host setup.
    ///
    /// <para>
    /// Hosts that need a different strategy (pure singleton, custom locator, test mock)
    /// call <see cref="RenderContextResolver.Use(IRenderContextServiceProvider)"/> directly
    /// — <see cref="Register"/> is the container-flavored shortcut, not the only path.
    /// </para>
    /// </summary>
    public static class RenderContextRegistration
    {
        /// <summary>
        /// Constructs the service, registers it on <paramref name="container"/>, and points
        /// <see cref="RenderContextResolver"/> at the container. Returns the service instance
        /// for chained host configuration. Must be called BEFORE <c>container.Build()</c>.
        /// </summary>
        public static IRenderContextService Register(PFound.DependencyContainer.DependencyContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            var service = new RenderContextService();
            container.RegisterInstance<IRenderContextService>(service);
            RenderContextResolver.Use(container);
            return service;
        }
    }
}
