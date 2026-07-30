using System;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Wraps a <see cref="Func{IRenderContextService}"/>. Useful for tests, custom locators,
    /// or any "I have my own way to get the service" scenario without writing a full
    /// provider class. Use via convenience <c>RenderContextResolver.Use(() =&gt; ...)</c>.
    /// </summary>
    public sealed class DelegateRenderContextServiceProvider : IRenderContextServiceProvider
    {
        private readonly Func<IRenderContextService> _func;

        public DelegateRenderContextServiceProvider(Func<IRenderContextService> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public IRenderContextService GetService() => _func();
    }
}
