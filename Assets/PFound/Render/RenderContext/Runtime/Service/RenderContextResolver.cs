using System;
using PFound.DependencyContainer;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Strategy-agnostic resolver for <see cref="IRenderContextService"/>. Consumers configure
    /// the resolution strategy once at host boot via <see cref="Use(IRenderContextServiceProvider)"/>
    /// (or the convenience overloads); <see cref="RenderContextSinkBehaviour"/> calls
    /// <see cref="Resolve"/> in <c>OnEnable</c> regardless of which strategy is active.
    ///
    /// <para>
    /// This decouples the wrapper from any specific service-acquisition path — singleton,
    /// DependencyContainer lookup, custom locator, test mock — they all plug in via
    /// <see cref="IRenderContextServiceProvider"/>.
    /// </para>
    /// </summary>
    public static class RenderContextResolver
    {
        private static IRenderContextServiceProvider _provider;

        /// <summary>Plug in any <see cref="IRenderContextServiceProvider"/> implementation.</summary>
        public static void Use(IRenderContextServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Convenience: use a fixed singleton instance.</summary>
        public static void Use(IRenderContextService instance)
            => Use(new SingletonRenderContextServiceProvider(instance));

        /// <summary>Convenience: resolve from a <c>PFound.DependencyContainer</c> container on every call.</summary>
        public static void Use(PFound.DependencyContainer.DependencyContainer container)
            => Use(new ContainerRenderContextServiceProvider(container));

        /// <summary>Convenience: use a delegate-based resolver (lambda or method group).</summary>
        public static void Use(Func<IRenderContextService> resolver)
            => Use(new DelegateRenderContextServiceProvider(resolver));

        /// <summary>Clears the configured provider. Subsequent <see cref="Resolve"/> calls throw.</summary>
        public static void Clear() => _provider = null;

        /// <summary>True if a provider has been configured.</summary>
        public static bool IsConfigured => _provider != null;

        /// <summary>Service-locator accessor. Throws if no provider configured.</summary>
        internal static IRenderContextService Resolve()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException(
                    "RenderContextResolver is not configured. Call RenderContextResolver.Use(...) at host boot " +
                    "before any RenderContextSinkBehaviour enables. Pass a SingletonRenderContextServiceProvider " +
                    "for direct ownership, a ContainerRenderContextServiceProvider(container) for DependencyContainer " +
                    "lookup, or RenderContextRegistration.Register(container) for the all-in-one bootstrap.");
            }
            var service = _provider.GetService();
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"{_provider.GetType().Name}.GetService() returned null — provider implementation bug.");
            }
            return service;
        }
    }
}
