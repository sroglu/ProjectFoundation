using System;
using System.Collections.Generic;
using PFound.DependencyContainer;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Registers <see cref="IRenderPostProcess"/> + built-in adapters with a
    /// <c>PFound.DependencyContainer</c>. Call once during your service-registry setup; the
    /// service auto-ticks via <c>PFound.LoopScheduler</c> BeforeRender.
    /// </summary>
    public static class RenderPostProcessRegistration
    {
        /// <summary>
        /// Constructs the service with the built-in adapters (Blur + Outline) plus any
        /// caller-supplied <paramref name="extraAdapters"/>, and registers it as an
        /// <see cref="IRenderPostProcess"/> singleton instance on <paramref name="registry"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="registry"/> is null.</exception>
        public static IRenderPostProcess Register(
            PFound.DependencyContainer.DependencyContainer registry,
            RenderPostProcessOptions options = null,
            params IRenderPostProcessAdapter[] extraAdapters)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var opts = options ?? RenderPostProcessOptions.Default;

            var adapters = new List<IRenderPostProcessAdapter>
            {
                new BlurAdapter(opts.BlurPolicy, opts.MaxConcurrentRequestsPerEffect, opts.WarnOnMissingVolumeComponent),
                new OutlineAdapter(opts.OutlinePolicy, opts.MaxConcurrentRequestsPerEffect, opts.WarnOnMissingVolumeComponent),
            };
            if (extraAdapters != null)
            {
                for (int i = 0; i < extraAdapters.Length; i++)
                {
                    if (extraAdapters[i] != null) adapters.Add(extraAdapters[i]);
                }
            }

            var service = new RenderPostProcessService(opts, adapters);
            registry.RegisterInstance<IRenderPostProcess>(service);
            return service;
        }
    }
}
