using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Descriptor passed to <see cref="IBatchRenderingService.RegisterBatch"/>. Captured by value into
    /// the returned handle; mutations to the caller's copy after registration do not affect the
    /// registered batch.
    /// </summary>
    /// <remarks>
    /// Required fields: <see cref="mesh"/>, <see cref="material"/>, <see cref="source"/>. The service
    /// throws <see cref="ArgumentNullException"/> at <c>RegisterBatch</c> time if any of those three
    /// is null. <see cref="subMeshIndex"/> is validated against <c>mesh.subMeshCount</c>.
    /// <para>
    /// Sensible defaults: <see cref="subMeshIndex"/>=0, <see cref="culling"/>=<see cref="CullingPolicy.Default"/>,
    /// <see cref="castShadows"/>=<c>On</c>, <see cref="receiveShadows"/>=<c>true</c>,
    /// <see cref="motionVectors"/>=<c>Camera</c>, <see cref="participatesInRenderGraph"/>=<c>false</c>.
    /// Leaving <see cref="culling"/> as <c>default(CullingPolicy)</c> yields
    /// <see cref="CullingPolicy.None"/> — set explicitly to <see cref="CullingPolicy.Default"/> if
    /// the frustum cull is desired (a future patch may invert this default; pin explicitly today).
    /// </para>
    /// </remarks>
    public struct BatchRenderingBatch : IEquatable<BatchRenderingBatch>
    {
        /// <summary>Mesh whose vertices/indices the GPU consumes. Must be non-null at register time.</summary>
        public Mesh mesh;

        /// <summary>Material applied to every instance. Must be non-null; for the
        /// <see cref="BackendKind.Classic"/> backend, <c>material.enableInstancing</c> must be <c>true</c>.</summary>
        public Material material;

        /// <summary>Sub-mesh to draw from <see cref="mesh"/>. Default 0. Must be in
        /// <c>[0, mesh.subMeshCount)</c>.</summary>
        public int subMeshIndex;

        /// <summary>Optional per-batch property block forwarded to <c>Graphics.RenderMeshInstanced</c>
        /// on the <see cref="BackendKind.Classic"/> backend.</summary>
        public MaterialPropertyBlock mpb;

        /// <summary>Strategy supplying per-instance data. See <see cref="IBatchInstanceSource"/>.</summary>
        public IBatchInstanceSource source;

        /// <summary>Per-batch culling configuration. Use <see cref="CullingPolicy.Default"/> for
        /// frustum-only, <see cref="CullingPolicy.None"/> to skip all culling.</summary>
        public CullingPolicy culling;

        /// <summary>Which GPU instancing path to use. See <see cref="BackendKind"/>.</summary>
        public BackendKind backend;

        /// <summary>Unity layer index forwarded to <c>RenderParams.layer</c>.</summary>
        public int layer;

        /// <summary>Shadow casting mode forwarded to <c>RenderParams.shadowCastingMode</c>.</summary>
        public ShadowCastingMode castShadows;

        /// <summary>Whether instances receive shadows; forwarded to <c>RenderParams.receiveShadows</c>.</summary>
        public bool receiveShadows;

        /// <summary>Motion-vector generation mode; forwarded to
        /// <c>RenderParams.motionVectorMode</c>.</summary>
        public MotionVectorGenerationMode motionVectors;

        /// <summary>
        /// When <c>true</c>, batches are drawn inside the URP RenderGraph via
        /// <see cref="BatchRenderingFeature"/>'s pass; when <c>false</c> (default), batches are drawn
        /// via direct <c>Graphics.RenderMesh*</c> calls outside the graph.
        /// </summary>
        public bool participatesInRenderGraph;

        /// <inheritdoc/>
        public bool Equals(BatchRenderingBatch other)
        {
            return ReferenceEquals(mesh, other.mesh)
                && ReferenceEquals(material, other.material)
                && subMeshIndex == other.subMeshIndex
                && ReferenceEquals(mpb, other.mpb)
                && ReferenceEquals(source, other.source)
                && backend == other.backend
                && layer == other.layer
                && castShadows == other.castShadows
                && receiveShadows == other.receiveShadows
                && motionVectors == other.motionVectors
                && participatesInRenderGraph == other.participatesInRenderGraph;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is BatchRenderingBatch b && Equals(b);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = mesh != null ? mesh.GetHashCode() : 0;
                h = (h * 397) ^ (material != null ? material.GetHashCode() : 0);
                h = (h * 397) ^ subMeshIndex;
                h = (h * 397) ^ (source != null ? source.GetHashCode() : 0);
                h = (h * 397) ^ (int)backend;
                return h;
            }
        }
    }
}
