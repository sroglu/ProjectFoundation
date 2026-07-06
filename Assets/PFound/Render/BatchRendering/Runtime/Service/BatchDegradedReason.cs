namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Reason a registered batch has been flagged degraded (no draws issued, handle still valid for <c>Dispose</c>).
    /// </summary>
    /// <remarks>
    /// Set on first detected issue and never overwritten (sticky). Consumers fix the underlying cause and
    /// re-register a new batch — per the owner-managed registration golden rule (CODING-STYLE.md §8),
    /// the service does NOT auto-recover degraded batches.
    /// <para>
    /// Special case: <see cref="OcclusionStubActive"/> is informational only; the handle's
    /// <c>IsDegraded</c> may remain <c>false</c> when this is the sole reason — the batch still renders
    /// with frustum / distance culling, only the occlusion path is skipped. If a harder reason is also
    /// present, that reason wins and <c>IsDegraded</c> is <c>true</c>.
    /// </para>
    /// </remarks>
    public enum BatchDegradedReason
    {
        /// <summary>
        /// Classic backend was requested but the supplied <c>Material.enableInstancing</c> is <c>false</c>.
        /// Toggle the importer flag on the material asset and re-register the batch.
        /// </summary>
        MissingEnableInstancing,

        /// <summary>
        /// Indirect or Procedural backend was requested but the host platform lacks
        /// <c>SystemInfo.supportsComputeShaders</c> + <c>SystemInfo.supportsIndirectArgumentsBuffer</c>.
        /// Fall back to <see cref="BackendKind.Classic"/> manually.
        /// </summary>
        BackendUnsupported,

        /// <summary>
        /// <c>CullingPolicy.occlusion = true</c> was set, but occlusion culling is not implemented in
        /// Phase 11. The flag is silently treated as no-op; other culling stages still run.
        /// </summary>
        OcclusionStubActive,

        /// <summary>
        /// The <see cref="IBatchInstanceSource"/> returned <c>false</c> from both
        /// <c>TryGetNativeArrayView</c> and <c>TryGetComputeBuffer</c>, or the underlying container is
        /// no longer valid (consumer disposed a <c>NativeArray</c> or <c>ComputeBuffer</c> without first
        /// disposing the batch handle — owner-managed contract violation).
        /// </summary>
        InvalidSource,

        /// <summary>
        /// The <c>Mesh</c> referenced by the batch's descriptor was destroyed externally
        /// (owner-managed contract violation).
        /// </summary>
        MeshDestroyed,

        /// <summary>
        /// The <c>Material</c> referenced by the batch's descriptor was destroyed externally
        /// (owner-managed contract violation).
        /// </summary>
        MaterialDestroyed,
    }
}
