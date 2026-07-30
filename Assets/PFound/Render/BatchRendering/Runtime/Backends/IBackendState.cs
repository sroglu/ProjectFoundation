using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Per-batch backend-specific cached state. Three concrete implementations in Phase 11:
    /// <c>ClassicBackendState</c> (T033), <c>IndirectBackendState</c> (T043), <c>ProceduralBackendState</c>
    /// (T063). Implementations release their owned native resources on <see cref="IDisposable.Dispose"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Dispatch"/> is the direct-draw path (called from
    /// <see cref="BatchRenderingService.OnBeforeRender"/>); it issues <c>Graphics.RenderMesh*</c>
    /// calls outside the URP RenderGraph. <see cref="DispatchRasterCmd"/> is the RenderGraph path
    /// (called from <c>BatchRenderingRenderGraphPass</c>); it records the same draws onto a
    /// <see cref="RasterCommandBuffer"/> so they run inside the URP graph at the feature's
    /// configured injection point.
    /// <para>
    /// Implementations MUST NOT retain references to <c>ctx.VisibleIndices</c> or <c>ctx.Matrices</c>
    /// across calls — those are service-owned and reused per tick.
    /// </para>
    /// </remarks>
    internal interface IBackendState : IDisposable
    {
        /// <summary>Issues the platform-appropriate direct draw call(s) for <paramref name="ctx"/>.
        /// Used by the default (non-RenderGraph) dispatch path.</summary>
        void Dispatch(in DispatchContext ctx);

        /// <summary>Records the platform-appropriate draw call(s) onto <paramref name="cmd"/>.
        /// Used when the batch's descriptor has <see cref="BatchRenderingBatch.participatesInRenderGraph"/>
        /// = <c>true</c> and the URP <c>BatchRenderingFeature</c> is active on the renderer asset.</summary>
        void DispatchRasterCmd(RasterCommandBuffer cmd, in DispatchContext ctx);
    }

    /// <summary>
    /// Per-tick dispatch payload passed by-ref to <see cref="IBackendState.Dispatch"/>. Carries the
    /// post-cull visibility slice + the source view + the descriptor's stable Unity references
    /// (mesh, material, MPB, layer, shadows, motion vectors).
    /// </summary>
    /// <remarks>
    /// Fields are populated by <see cref="BatchRenderingService"/> on each tick; the struct itself
    /// is not retained. Implementations should treat all <c>NativeArray</c> / <c>ComputeBuffer</c>
    /// references as live for the duration of the call only.
    /// </remarks>
    internal readonly struct DispatchContext
    {
        /// <summary>Active camera being processed this tick.</summary>
        public readonly Camera Camera;

        /// <summary>Post-cull visible count (use only the first <c>VisibleCount</c> entries of
        /// <see cref="VisibleIndices"/>).</summary>
        public readonly int VisibleCount;

        /// <summary>Indices into <see cref="Matrices"/> for the visible instances. Length may
        /// exceed <see cref="VisibleCount"/> — only the prefix is meaningful.</summary>
        public readonly NativeArray<int> VisibleIndices;

        /// <summary>CPU-side instance matrices for classic-backend gather. Empty when the source is
        /// GPU-side (indirect / procedural read from <see cref="ComputeBuffer"/>).</summary>
        public readonly NativeArray<float4x4> Matrices;

        /// <summary>GPU-side instance buffer for indirect / procedural backends. Null when the
        /// source is CPU-side.</summary>
        public readonly ComputeBuffer ComputeBuffer;

        /// <summary>Stride of the elements in <see cref="ComputeBuffer"/> (usually 80 = sizeof(MeshInstanceData)).</summary>
        public readonly int ComputeBufferStride;

        /// <summary>The original descriptor — backends read mesh / material / MPB / layer / shadow
        /// flags directly from here.</summary>
        public readonly BatchRenderingBatch Descriptor;

        public DispatchContext(
            Camera camera,
            int visibleCount,
            NativeArray<int> visibleIndices,
            NativeArray<float4x4> matrices,
            ComputeBuffer computeBuffer,
            int computeBufferStride,
            in BatchRenderingBatch descriptor)
        {
            Camera = camera;
            VisibleCount = visibleCount;
            VisibleIndices = visibleIndices;
            Matrices = matrices;
            ComputeBuffer = computeBuffer;
            ComputeBufferStride = computeBufferStride;
            Descriptor = descriptor;
        }
    }
}
