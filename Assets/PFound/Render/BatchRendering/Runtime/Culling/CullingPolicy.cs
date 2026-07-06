namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Per-batch culling configuration. Set on <see cref="BatchRenderingBatch.culling"/>.
    /// </summary>
    /// <remarks>
    /// Use the static sentinel <see cref="Default"/> for the default frustum-only pipeline, or
    /// <see cref="None"/> when the consumer fully owns culling (typical for pre-culled
    /// <c>ComputeBuffer</c>-backed batches on the <see cref="BackendKind.Indirect"/> /
    /// <see cref="BackendKind.Procedural"/> paths).
    /// <para>
    /// <see cref="occlusion"/> is a Phase 11 stub — setting it emits a one-shot warning and the flag
    /// has no rendering effect.
    /// </para>
    /// </remarks>
    public struct CullingPolicy
    {
        /// <summary>
        /// When <c>true</c>, the Burst frustum-cull stage runs and instances outside the active
        /// camera's six frustum planes are excluded.
        /// </summary>
        public bool frustum;

        /// <summary>
        /// Optional distance-culling configuration. When <c>distance.enabled</c> is <c>false</c>,
        /// the distance-cull job is not scheduled (zero per-instance distance cost).
        /// </summary>
        public DistanceCullingConfig distance;

        /// <summary>
        /// Phase 11 stub. Setting to <c>true</c> emits a one-shot
        /// <see cref="BatchDegradedReason.OcclusionStubActive"/> warning; the flag has no rendering
        /// effect until occlusion culling lands in a later phase.
        /// </summary>
        public bool occlusion;

        /// <summary>
        /// Default policy: frustum culling enabled, distance disabled, occlusion off.
        /// </summary>
        public static CullingPolicy Default => new CullingPolicy
        {
            frustum = true,
            distance = default,
            occlusion = false,
        };

        /// <summary>
        /// Skip-all policy: no culling stages run. Used when the consumer owns culling fully
        /// (e.g., GPU-side culling already done in a compute pipeline).
        /// </summary>
        public static CullingPolicy None => new CullingPolicy
        {
            frustum = false,
            distance = default,
            occlusion = false,
        };
    }

    /// <summary>
    /// Per-batch distance-culling configuration nested under <see cref="CullingPolicy.distance"/>.
    /// </summary>
    public struct DistanceCullingConfig
    {
        /// <summary>
        /// When <c>true</c>, the Burst distance-cull stage runs after the frustum cull and excludes
        /// instances whose distance from the active camera exceeds <see cref="maxDistance"/>.
        /// </summary>
        public bool enabled;

        /// <summary>
        /// Maximum world-space distance from the active camera position. Instances beyond this
        /// distance are culled. Ignored when <see cref="enabled"/> is <c>false</c>.
        /// </summary>
        public float maxDistance;
    }
}
