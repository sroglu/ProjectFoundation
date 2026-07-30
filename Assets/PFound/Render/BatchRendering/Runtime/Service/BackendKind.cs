namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Selects which GPU instancing path a registered batch uses.
    /// </summary>
    /// <remarks>
    /// Set per-batch on <see cref="BatchRenderingBatch.backend"/>.
    /// <para>
    /// Selection guide:
    /// <list type="bullet">
    /// <item><description><see cref="Classic"/> — 100..5,000 CPU-authored instances. <c>Graphics.RenderMeshInstanced</c> chunked at 1023 per call. Requires <c>Material.enableInstancing == true</c>.</description></item>
    /// <item><description><see cref="Indirect"/> — 5,000..500,000 GPU-authored instances. <c>Graphics.RenderMeshIndirect</c>. Service writes culled count into the args buffer when culling is enabled.</description></item>
    /// <item><description><see cref="Procedural"/> — consumer-owned vertex / index pipeline. <c>Graphics.DrawProceduralIndirect</c> pass-through.</description></item>
    /// </list>
    /// <see cref="Indirect"/> and <see cref="Procedural"/> require <c>SystemInfo.supportsComputeShaders</c> + <c>SystemInfo.supportsIndirectArgumentsBuffer</c>; on unsupported platforms the batch is degraded with a one-shot warning.
    /// </para>
    /// </remarks>
    public enum BackendKind
    {
        /// <summary>
        /// <c>Graphics.RenderMeshInstanced</c>. Universally supported. Visible matrices are chunked at 1023 per draw call.
        /// </summary>
        Classic = 0,

        /// <summary>
        /// <c>Graphics.RenderMeshIndirect</c>. Reads instance data from a <c>ComputeBuffer</c>; service authors the args-buffer instance-count slot from culled output. Requires GPU compute + indirect-args support.
        /// </summary>
        Indirect = 1,

        /// <summary>
        /// <c>Graphics.DrawProceduralIndirect</c>. Consumer owns the vertex / index data and the args buffer. Service is a pass-through dispatcher. Requires GPU compute + indirect-args support.
        /// </summary>
        Procedural = 2,
    }
}
