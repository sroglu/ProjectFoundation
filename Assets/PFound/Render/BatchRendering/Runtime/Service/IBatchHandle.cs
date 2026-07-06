using System;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Opaque per-registration handle returned by
    /// <see cref="IBatchRenderingService.RegisterBatch"/>. The consumer holds the handle for the
    /// batch's lifetime and disposes it at the matching close / unload / disable hook in their own
    /// code (per the owner-managed registration golden rule — CODING-STYLE.md §8).
    /// </summary>
    /// <remarks>
    /// All properties are safe to read after <see cref="Dispose"/> or after the owning service is
    /// disposed — they return the last observed value (or zero / default for never-ticked batches).
    /// <see cref="IsAlive"/> transitions to <c>false</c> on dispose and never returns to <c>true</c>;
    /// <see cref="IsDegraded"/> is sticky in the same way.
    /// </remarks>
    public interface IBatchHandle : IDisposable
    {
        /// <summary>
        /// <c>true</c> while the batch is registered. Becomes <c>false</c> immediately after
        /// <see cref="Dispose"/> or service dispose.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// <c>true</c> when the service has flagged this batch as no-op due to a detected issue.
        /// Sticky — once <c>true</c>, never returns to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Special case: when <see cref="DegradedReason"/> is
        /// <see cref="BatchDegradedReason.OcclusionStubActive"/> as the sole reason, this property
        /// may remain <c>false</c> — the batch still renders normally; only the occlusion path is
        /// skipped.
        /// </remarks>
        bool IsDegraded { get; }

        /// <summary>
        /// First detected degradation reason. <c>null</c> when no issue has been detected. Set on
        /// first detection and never overwritten.
        /// </summary>
        BatchDegradedReason? DegradedReason { get; }

        /// <summary>
        /// Snapshot of <c>source.Count</c> taken at the start of the most recent tick that processed
        /// this batch. <c>0</c> before the first tick or after disposal.
        /// </summary>
        int RegisteredInstanceCount { get; }

        /// <summary>
        /// Visible count after culling on the most recent tick. For multi-camera scenes this is the
        /// last camera processed in the tick (not summed across cameras). <c>0</c> before first tick
        /// or after disposal.
        /// </summary>
        int LastFrameVisibleCount { get; }
    }
}
