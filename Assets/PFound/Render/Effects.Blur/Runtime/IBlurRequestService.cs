using System;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Priority-queue based request service for <see cref="BlurStrengthVolumeComponent"/>.
    /// Multiple consumers submit ranked requests; the service resolves the top-priority entry
    /// to the underlying volume each time the queue changes. The service does not track consumer
    /// objects; consumers own ticket lifecycle via <see cref="IBlurTicket.Dispose"/>.
    /// </summary>
    /// <remarks>
    /// Mutually exclusive with Phase 6 PostProcess BlurAdapter — both write to the same
    /// VolumeComponent and the last writer wins. Pick one path per project.
    /// </remarks>
    public interface IBlurRequestService : IDisposable
    {
        /// <summary>
        /// Submits a request with the given priority. Same-priority collisions throw
        /// <see cref="System.InvalidOperationException"/> — assign unique priorities
        /// (UI z-order recommended).
        /// </summary>
        IBlurTicket Request(int priority, BlurSpec spec);

        /// <summary>Number of active (non-disposed) tickets currently in the queue.</summary>
        int ActiveCount { get; }
    }
}
