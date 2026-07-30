using System;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Lifetime handle for an active post-process request. Calling <see cref="Release"/> or
    /// <see cref="IDisposable.Dispose"/> removes the underlying request on the next tick.
    /// Both calls are idempotent — subsequent calls are no-ops.
    /// </summary>
    public interface IRenderPostProcessTicket : IDisposable
    {
        /// <summary>End the request. Idempotent; double-Release is a no-op.</summary>
        void Release();

        /// <summary>True after the first <see cref="Release"/> / <see cref="IDisposable.Dispose"/> call.</summary>
        bool IsReleased { get; }
    }
}
