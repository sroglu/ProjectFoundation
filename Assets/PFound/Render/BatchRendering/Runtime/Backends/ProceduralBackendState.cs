using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// <see cref="BackendKind.Procedural"/> backend state — pass-through dispatcher for
    /// <c>Graphics.DrawProceduralIndirect</c>. Consumer owns the args buffer (and the
    /// vertex/index pipeline encoded in their custom shader).
    /// </summary>
    /// <remarks>
    /// Procedural is a niche backend: the consumer fully owns the args buffer authoring. The
    /// service does NOT mutate the args buffer (unlike <see cref="IndirectBackendState"/>) — it
    /// simply forwards the dispatch with the consumer-supplied <see cref="ComputeBuffer"/> from the
    /// instance source.
    /// <para>
    /// Phase 11 procedural + culling is NOT supported — frustum / distance cull need CPU matrices,
    /// which a procedural source doesn't expose. <see cref="CullingPolicy.None"/> is the only valid
    /// policy for procedural batches; the service treats other policies as None on procedural
    /// batches (cull is service-side, dispatch is consumer-side). This is a documented Phase 11
    /// limitation; per-instance procedural cull lands in a follow-up patch.
    /// </para>
    /// </remarks>
    internal sealed class ProceduralBackendState : IBackendState
    {
        private static readonly Bounds GenerousWorldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
        private readonly MeshTopology _topology;

        internal ProceduralBackendState(MeshTopology topology = MeshTopology.Triangles)
        {
            _topology = topology;
        }

        public void Dispatch(in DispatchContext ctx)
        {
            if (ctx.Descriptor.material == null) return;
            if (ctx.ComputeBuffer == null) return;

            // Legacy DrawProceduralIndirect accepts the consumer's ComputeBuffer of args directly;
            // the newer Graphics.RenderPrimitivesIndirect requires a GraphicsBuffer (separate type
            // in Unity 6). To keep the consumer surface simple (one ComputeBuffer through
            // ComputeBufferInstanceSource) we use the legacy path for Phase 11.
            try
            {
                Graphics.DrawProceduralIndirect(
                    ctx.Descriptor.material,
                    GenerousWorldBounds,
                    _topology,
                    ctx.ComputeBuffer,
                    argsOffset: 0,
                    ctx.Camera,
                    ctx.Descriptor.mpb,
                    ctx.Descriptor.castShadows,
                    ctx.Descriptor.receiveShadows,
                    ctx.Descriptor.layer);
            }
            catch (Exception)
            {
                // Defensive — procedural draws on misconfigured args buffers can throw at the
                // driver layer. Swallow + let the next tick attempt continue (consumer's
                // responsibility to fix the args buffer).
            }
        }

        public void DispatchRasterCmd(RasterCommandBuffer cmd, in DispatchContext ctx)
        {
            if (ctx.Descriptor.material == null) return;
            if (ctx.ComputeBuffer == null) return;

            // RasterCommandBuffer's DrawProceduralIndirect overload uses Matrix4x4 transform; we
            // pass identity since the per-primitive transforms are encoded in the consumer's
            // vertex pipeline.
            try
            {
                cmd.DrawProceduralIndirect(
                    Matrix4x4.identity,
                    ctx.Descriptor.material,
                    shaderPass: -1,
                    _topology,
                    ctx.ComputeBuffer,
                    argsOffset: 0,
                    ctx.Descriptor.mpb);
            }
            catch (Exception)
            {
                // Same defensive swallow as Dispatch.
            }
        }

        public void Dispose()
        {
            // Consumer owns the args buffer — nothing to release here.
        }
    }
}
