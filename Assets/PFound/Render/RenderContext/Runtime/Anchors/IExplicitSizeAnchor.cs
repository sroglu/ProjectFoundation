namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Marker interface — anchors that implement this require <c>descriptor.Width &gt; 0</c>
    /// and <c>descriptor.Height &gt; 0</c> at <see cref="IRenderContextService.Acquire"/> time
    /// (e.g., <c>MeshRendererAnchor</c> — world-space rendering has no view-space "size" to auto-resolve).
    /// </summary>
    internal interface IExplicitSizeAnchor { }
}
