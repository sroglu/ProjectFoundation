using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Per-acquisition handle. <c>Dispose()</c> unbinds the sink (restoring anchor's pre-bind state),
    /// destroys content children, and returns the (RT, Camera GO, ContentRoot) composite entry to the pool.
    /// </summary>
    public interface IRenderContextHandle : IDisposable
    {
        RenderTexture Texture { get; }
        Camera Camera { get; }
        Transform ContentRoot { get; }
        bool IsAlive { get; }
        void Refresh();
    }
}
