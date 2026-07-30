using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests.Helpers
{
    /// <summary>
    /// Pure-C# anchor for headless tests. No Unity component dependency — explicit width/height,
    /// always-alive, no-op sink. Used by US4 (headless) + US5 (diagnostics) tests.
    /// </summary>
    public sealed class TestRenderContextAnchor : IRenderContextAnchor
    {
        private readonly object _target;
        public int PreferredWidth { get; set; }
        public int PreferredHeight { get; set; }
        public bool TargetAlive { get; set; } = true;

        public TestRenderContextAnchor(int width, int height, object target = null)
        {
            PreferredWidth = width;
            PreferredHeight = height;
            _target = target ?? new object();
        }

        public object Target => _target;
        public IRenderContextSink CreateSink() => new NoOpSink();

        private sealed class NoOpSink : IRenderContextSink
        {
            public void Bind(RenderTexture rt) { }
            public void Unbind() { }
        }
    }
}
