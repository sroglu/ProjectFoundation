using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// <see cref="BackendKind.Indirect"/> backend state — issues one
    /// <c>Graphics.RenderMeshIndirect</c> dispatch per tick. The service-owned args buffer is
    /// pre-authored with mesh draw constants at register time; only the instance-count slot is
    /// written per tick from the post-cull visible count.
    /// </summary>
    /// <remarks>
    /// Phase 11 ships single-chunk-per-batch — one args entry written per tick. Multi-chunk
    /// pagination (for 500k+ instance counts split across many args entries) is deferred to a later
    /// patch.
    /// <para>
    /// Per research.md R1, the args layout is the Unity-defined
    /// <see cref="GraphicsBuffer.IndirectDrawIndexedArgs"/> (20 bytes / 5 uint32: indexCountPerInstance,
    /// instanceCount, startIndex, baseVertexIndex, startInstance). We allocate one entry and update
    /// only <c>instanceCount</c> (offset 4) per tick using a 1-element scratch
    /// <see cref="NativeArray{T}"/> reused across all ticks (zero managed alloc on the hot path).
    /// </para>
    /// </remarks>
    internal sealed class IndirectBackendState : IBackendState
    {
        private static readonly Bounds GenerousWorldBounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        private GraphicsBuffer _argsBuffer;
        private NativeArray<uint> _argsScratch; // 5-element scratch reused per tick (uint32 layout)

        private uint _indexCountPerInstance;
        private uint _startIndex;
        private uint _baseVertexIndex;

        internal IndirectBackendState(Mesh mesh, int subMeshIndex)
        {
            _indexCountPerInstance = mesh.GetIndexCount(subMeshIndex);
            _startIndex = mesh.GetIndexStart(subMeshIndex);
            _baseVertexIndex = (uint)mesh.GetBaseVertex(subMeshIndex);

            // GraphicsBuffer.IndirectDrawIndexedArgs.size = 20 bytes (5 × uint32).
            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            _argsScratch = new NativeArray<uint>(5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _argsScratch[0] = _indexCountPerInstance;
            _argsScratch[1] = 0; // instanceCount — written per tick
            _argsScratch[2] = _startIndex;
            _argsScratch[3] = _baseVertexIndex;
            _argsScratch[4] = 0; // startInstance
            _argsBuffer.SetData(_argsScratch);
        }

        public void Dispatch(in DispatchContext ctx)
        {
            if (ctx.VisibleCount <= 0) return;
            if (ctx.Descriptor.mesh == null || ctx.Descriptor.material == null) return;

            // Update only the instanceCount slot (offset 4 bytes / element index 1).
            _argsScratch[1] = (uint)ctx.VisibleCount;
            // GraphicsBuffer is allocated with stride=20 (one IndirectDrawIndexedArgs entry); a
            // partial write of a single uint at byte offset 4 violates the stride alignment. Upload
            // the full 20-byte args structure in one element-sized write — both sides match at 20B
            // total (5 × uint = 1 × IndirectDrawIndexedArgs). Still zero managed alloc.
            _argsBuffer.SetData(_argsScratch);

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

            Graphics.RenderMeshIndirect(rp, ctx.Descriptor.mesh, _argsBuffer);
        }

        public void DispatchRasterCmd(RasterCommandBuffer cmd, in DispatchContext ctx)
        {
            if (ctx.VisibleCount <= 0) return;
            if (ctx.Descriptor.mesh == null || ctx.Descriptor.material == null) return;

            _argsScratch[1] = (uint)ctx.VisibleCount;
            // GraphicsBuffer is allocated with stride=20 (one IndirectDrawIndexedArgs entry); a
            // partial write of a single uint at byte offset 4 violates the stride alignment. Upload
            // the full 20-byte args structure in one element-sized write — both sides match at 20B
            // total (5 × uint = 1 × IndirectDrawIndexedArgs). Still zero managed alloc.
            _argsBuffer.SetData(_argsScratch);

            cmd.DrawMeshInstancedIndirect(
                ctx.Descriptor.mesh,
                ctx.Descriptor.subMeshIndex,
                ctx.Descriptor.material,
                shaderPass: -1,
                _argsBuffer,
                argsOffset: 0,
                ctx.Descriptor.mpb);
        }

        public void Dispose()
        {
            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }
            if (_argsScratch.IsCreated) _argsScratch.Dispose();
        }

        // ---------------- Test seams ----------------

        internal uint ArgsIndexCountPerInstance => _indexCountPerInstance;
        internal uint ArgsStartIndex => _startIndex;
        internal uint ArgsBaseVertexIndex => _baseVertexIndex;
        internal NativeArray<uint> ArgsScratch => _argsScratch;
    }
}
