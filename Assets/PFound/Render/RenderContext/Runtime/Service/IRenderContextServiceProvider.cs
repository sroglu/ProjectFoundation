namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Strategy interface for how <see cref="RenderContextSinkBehaviour"/> resolves its
    /// <see cref="IRenderContextService"/>. Plug an implementation into
    /// <see cref="RenderContextResolver.Use(IRenderContextServiceProvider)"/> at host boot.
    ///
    /// <para>
    /// Decouples the wrapper from any specific resolution strategy — consumers can pick
    /// singleton (<see cref="SingletonRenderContextServiceProvider"/>), DependencyContainer-based
    /// lookup (<see cref="ContainerRenderContextServiceProvider"/>), a custom locator, or write
    /// their own provider.
    /// </para>
    /// </summary>
    public interface IRenderContextServiceProvider
    {
        /// <summary>Resolves the current <see cref="IRenderContextService"/>. Must not return null.</summary>
        IRenderContextService GetService();
    }
}
