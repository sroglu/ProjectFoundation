using System;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Entry point of the BatchRendering module. Construct concretely via <c>new BatchRenderingService()</c>;
    /// there is no static <c>Instance</c> and no required <c>PFound.DependencyContainer</c> registration.
    /// Consumers MAY register the instance on any container of their choosing — that is consumer-side
    /// wiring, outside this module's surface.
    /// </summary>
    /// <remarks>
    /// <b>Thread safety.</b> Main-thread only. All methods MUST be called from Unity's main thread.
    /// Burst cull jobs run on worker threads internally but are scheduled and completed within a
    /// single main-thread tick.
    /// <para>
    /// <b>Owner-managed batch lifecycle.</b> Per CODING-STYLE.md §8: the consumer who calls
    /// <see cref="RegisterBatch"/> is solely responsible for calling <see cref="IBatchHandle.Dispose"/>
    /// at the matching close / unload / disable hook in their own code. The service does NOT
    /// subscribe to <c>SceneManager</c> events, does NOT auto-clear, does NOT track scene-of-origin.
    /// Detected invalid references on tick → one-shot warning + degrade-to-no-op.
    /// </para>
    /// </remarks>
    public interface IBatchRenderingService : IDisposable
    {
        /// <summary>
        /// Registers a batch for per-frame per-camera cull-and-dispatch processing. Returns a handle
        /// the consumer holds for the batch lifetime.
        /// </summary>
        /// <param name="batch">Descriptor describing the mesh, material, instance source, culling,
        /// and backend.</param>
        /// <returns>An <see cref="IBatchHandle"/> the consumer disposes at the matching close hook.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <c>batch.mesh</c>, <c>batch.material</c>, or <c>batch.source</c> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <c>batch.subMeshIndex</c> is outside <c>[0, batch.mesh.subMeshCount)</c>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the service has already been disposed.
        /// </exception>
        IBatchHandle RegisterBatch(BatchRenderingBatch batch);
    }
}
