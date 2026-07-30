using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Per-instance payload layout used by the <see cref="BackendKind.Indirect"/> backend.
    /// </summary>
    /// <remarks>
    /// Consumers authoring a <c>ComputeBuffer</c> for an indirect-backend batch MUST use this layout
    /// (stride = 80 bytes) unless their material reads a custom per-instance layout — in which case
    /// the batch's instance-source <c>stride</c> override is the authoritative value.
    /// <para>
    /// Layout:
    /// <list type="bullet">
    /// <item><description>0..63: <see cref="LocalToWorld"/> — world-space transform of the instance.</description></item>
    /// <item><description>64..79: <see cref="PerInstanceColor"/> — reserved; default white in Phase 11. Per-instance color / property use will land in a later phase.</description></item>
    /// </list>
    /// </para>
    /// The culling stage only reads <see cref="LocalToWorld"/>. <see cref="PerInstanceColor"/> exists so
    /// the stride is one cache line for the matrix plus one slot for a future scalar property packet
    /// without forcing a layout change.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct MeshInstanceData
    {
        /// <summary>
        /// World-space transform of the instance. Used by the culling stage.
        /// </summary>
        public float4x4 LocalToWorld;

        /// <summary>
        /// Reserved per-instance color slot. Default white in Phase 11; not consumed by the service.
        /// </summary>
        public float4 PerInstanceColor;
    }
}
