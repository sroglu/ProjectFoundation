using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// <see cref="BackendKind.Classic"/> backend state — chunks visible matrices into
    /// <c>Graphics.RenderMeshInstanced</c> calls of up to
    /// <see cref="BackendCapabilityProbe.ClassicPerCallCap"/> (1023) instances each.
    /// </summary>
    /// <remarks>
    /// Holds a single reusable <c>Matrix4x4[1023]</c> scratch array — gathered per dispatch from
    /// the post-cull visible-index slice. The matrix scratch is the only managed allocation owned
    /// by this state instance; it is reused across all ticks.
    /// <para>
    /// <see cref="RenderParams.worldBounds"/> is set to a generous 100k-unit world cube — Unity
    /// uses this for shadow-caster culling at the batch level; instance-level visibility is already
    /// handled by the service's cull jobs, so we err on the side of overdraw. A future patch can
    /// tighten this by computing per-tick bounds in the cull job.
    /// </para>
    /// </remarks>
    internal sealed class ClassicBackendState : IBackendState
    {
        private static readonly Bounds GenerousWorldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        private Matrix4x4[] _matrixScratch;

        internal ClassicBackendState()
        {
            _matrixScratch = new Matrix4x4[BackendCapabilityProbe.ClassicPerCallCap];
        }

        /// <summary>
        /// Computes the number of dispatch chunks required for <paramref name="visibleCount"/>
        /// instances at the given per-call cap. Exposed as a static helper so tests can verify the
        /// chunking math without invoking the actual Graphics API.
        /// </summary>
        internal static int ComputeChunkCount(int visibleCount, int perCallCap)
        {
            if (visibleCount <= 0) return 0;
            if (perCallCap <= 0) return 0;
            return (visibleCount + perCallCap - 1) / perCallCap;
        }

        public void Dispatch(in DispatchContext ctx)
        {
            if (ctx.VisibleCount <= 0) return;
            if (ctx.Descriptor.mesh == null || ctx.Descriptor.material == null) return;
            if (!ctx.Matrices.IsCreated || !ctx.VisibleIndices.IsCreated) return;

            var rp = new RenderParams(ctx.Descriptor.material)
            {
                layer = ctx.Descriptor.layer,
                renderingLayerMask = 1,
                shadowCastingMode = ctx.Descriptor.castShadows,
                receiveShadows = ctx.Descriptor.receiveShadows,
                motionVectorMode = ctx.Descriptor.motionVectors,
                worldBounds = GenerousWorldBounds,
                matProps = ctx.Descriptor.mpb,
                camera = ctx.Camera,
            };

            int perCall = BackendCapabilityProbe.ClassicPerCallCap;
            int remaining = ctx.VisibleCount;
            int offset = 0;
            var visibleIndices = ctx.VisibleIndices;
            var matrices = ctx.Matrices;

            while (remaining > 0)
            {
                int chunkSize = remaining < perCall ? remaining : perCall;

                // Gather visible matrices into the contiguous scratch buffer.
                for (int i = 0; i < chunkSize; i++)
                {
                    int srcIdx = visibleIndices[offset + i];
                    float4x4 m = matrices[srcIdx];
                    _matrixScratch[i] = (Matrix4x4)m;
                }

                Graphics.RenderMeshInstanced(rp, ctx.Descriptor.mesh, ctx.Descriptor.subMeshIndex, _matrixScratch, chunkSize);

                offset += chunkSize;
                remaining -= chunkSize;
            }
        }

        public void DispatchRasterCmd(RasterCommandBuffer cmd, in DispatchContext ctx)
        {
            if (ctx.VisibleCount <= 0) return;
            if (ctx.Descriptor.mesh == null || ctx.Descriptor.material == null) return;
            if (!ctx.Matrices.IsCreated || !ctx.VisibleIndices.IsCreated) return;

            int perCall = BackendCapabilityProbe.ClassicPerCallCap;
            int remaining = ctx.VisibleCount;
            int offset = 0;
            var visibleIndices = ctx.VisibleIndices;
            var matrices = ctx.Matrices;

            while (remaining > 0)
            {
                int chunkSize = remaining < perCall ? remaining : perCall;

                for (int i = 0; i < chunkSize; i++)
                {
                    int srcIdx = visibleIndices[offset + i];
                    float4x4 m = matrices[srcIdx];
                    _matrixScratch[i] = (Matrix4x4)m;
                }

                cmd.DrawMeshInstanced(
                    ctx.Descriptor.mesh,
                    ctx.Descriptor.subMeshIndex,
                    ctx.Descriptor.material,
                    shaderPass: -1,
                    _matrixScratch,
                    chunkSize,
                    ctx.Descriptor.mpb);

                offset += chunkSize;
                remaining -= chunkSize;
            }
        }

        public void Dispose()
        {
            _matrixScratch = null;
        }
    }
}
