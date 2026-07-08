using System;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Priority-queue based request service for <see cref="OutlineVolumeComponent"/>. The service
    /// does not track consumer objects; consumers own ticket lifecycle via
    /// <see cref="IOutlineTicket.Dispose"/>.
    /// </summary>
    /// <remarks>
    /// Mutually exclusive with Phase 6 PostProcess OutlineAdapter — both write to the same
    /// VolumeComponent and the last writer wins. Pick one path per project.
    /// </remarks>
    public interface IOutlineRequestService : IDisposable
    {
        /// <summary>
        /// Submits a request with the given priority. Same-priority collisions throw
        /// <see cref="System.InvalidOperationException"/>.
        /// </summary>
        IOutlineTicket Request(int priority, OutlineSpec spec);

        int ActiveCount { get; }
    }
}
