using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Strategy interface that abstracts where per-instance data lives. Three built-in
    /// implementations ship in Phase 11: <see cref="NativeArrayInstanceSource"/>,
    /// <see cref="ComputeBufferInstanceSource"/>, <see cref="TransformArrayInstanceSource"/>.
    /// Consumers may write custom sources by implementing this interface.
    /// </summary>
    /// <remarks>
    /// <b>Ownership rule.</b> An instance source NEVER disposes the consumer-owned underlying
    /// container (e.g., <c>NativeArray</c>, <c>ComputeBuffer</c>, <c>Transform[]</c>). The source
    /// holds only a reference. Sources MAY own ancillary buffers they allocated themselves
    /// (e.g., the persistent flatten <c>NativeArray</c> inside
    /// <see cref="TransformArrayInstanceSource"/>); those sources implement <see cref="System.IDisposable"/>.
    /// <para>
    /// <b>Mutually-exclusive accessors.</b> An implementation MUST return <c>true</c> from exactly
    /// one of <see cref="TryGetNativeArrayView"/> or <see cref="TryGetComputeBuffer"/>. Returning
    /// <c>false</c> from both is a contract violation; the service flags the batch with
    /// <see cref="BatchDegradedReason.InvalidSource"/> and emits a one-shot warning.
    /// </para>
    /// </remarks>
    public interface IBatchInstanceSource
    {
        /// <summary>
        /// Number of instances currently authored. The service snapshots this once at the start of
        /// each cull tick. Implementations MUST be stable within a tick; cross-tick changes are
        /// permitted.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// CPU-side view path. Returns <c>true</c> and writes a stable view of length
        /// <see cref="Count"/> into <paramref name="view"/> when the source has
        /// <c>NativeArray&lt;float4x4&gt;</c> instance data. Returns <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        /// The view MUST remain valid (not disposed, not resized) for the duration of the cull job —
        /// from this call until the corresponding <c>JobHandle.Complete()</c>.
        /// </remarks>
        bool TryGetNativeArrayView(out NativeArray<float4x4> view);

        /// <summary>
        /// GPU-side view path. Returns <c>true</c> and writes the <c>ComputeBuffer</c> + per-element
        /// <paramref name="stride"/> when the source is GPU-side. Returns <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        /// <paramref name="stride"/> MUST equal <c>sizeof(MeshInstanceData)</c> (= 80) for the
        /// default layout — see <see cref="MeshInstanceData"/>. Custom layouts are allowed when the
        /// matching backend is <see cref="BackendKind.Procedural"/>.
        /// </remarks>
        bool TryGetComputeBuffer(out ComputeBuffer buffer, out int stride);

        /// <summary>
        /// Called once per tick per batch, immediately before the cull job is scheduled. Sources
        /// that need to prepare data (e.g., the per-tick <c>Transform[]</c> flatten in
        /// <see cref="TransformArrayInstanceSource"/>) schedule that work here and return its
        /// <c>JobHandle</c> in <paramref name="producedHandle"/>. Static-data sources pass through
        /// <paramref name="dependency"/> unchanged.
        /// </summary>
        /// <param name="dependency">
        /// Dependency that any scheduled work MUST honor (chain with <c>JobHandle.CombineDependencies</c>
        /// when needed). For Phase 11 the service passes <c>default</c>.
        /// </param>
        /// <param name="producedHandle">
        /// Handle the cull job will use as its dependency. Pure-data sources return
        /// <paramref name="dependency"/> unchanged.
        /// </param>
        void OnTickBegin(JobHandle dependency, out JobHandle producedHandle);
    }
}
