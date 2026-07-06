namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Adapter that wraps a backend-specific target (RawImage / VisualElement / MeshRenderer)
    /// and reports its effective pixel size + liveness to the watcher. Factory for the matching sink.
    /// </summary>
    public interface IRenderContextAnchor
    {
        object Target { get; }
        int PreferredWidth { get; }
        int PreferredHeight { get; }
        bool TargetAlive { get; }
        IRenderContextSink CreateSink();
    }
}
