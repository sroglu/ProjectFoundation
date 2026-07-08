using System;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Lifetime handle for a warmup session begun via
    /// <see cref="IShaderWarmupController.BeginSession(WarmupBatch[])"/>. Consumer polls
    /// <see cref="Progress"/> per frame (or checks <see cref="IsComplete"/>) and calls
    /// <see cref="Cancel"/> / <see cref="IDisposable.Dispose"/> to stop early.
    /// </summary>
    public interface IShaderWarmupSession : IDisposable
    {
        /// <summary>
        /// Aggregate progress across all batches in this session, weighted by each batch's
        /// <c>variantCount</c>. Range [0, 1]. Monotonically non-decreasing across consecutive reads.
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// True once all batches finish warming up OR <see cref="Cancel"/> /
        /// <see cref="IDisposable.Dispose"/> was called. Terminal — never flips back to false.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Stop further warmup work for this session. Idempotent — subsequent calls are no-ops.
        /// Partial warmup state remains in the underlying collections (Unity has no "un-warm").
        /// </summary>
        void Cancel();
    }
}
