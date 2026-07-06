using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Per-backend binding adapter. Captures pre-bind state on Bind, restores on Unbind.
    /// Unbind is idempotent (no-op if not bound).
    /// </summary>
    public interface IRenderContextSink
    {
        void Bind(RenderTexture rt);
        void Unbind();
    }
}
