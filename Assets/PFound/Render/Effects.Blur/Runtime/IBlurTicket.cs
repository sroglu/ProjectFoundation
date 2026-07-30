using System;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Owner-managed handle returned by <see cref="IBlurRequestService.Request"/>. Dispose
    /// removes this request from the priority queue; the next-highest priority (if any) takes
    /// over the volume state. Idempotent — calling Dispose twice is safe.
    /// </summary>
    public interface IBlurTicket : IDisposable
    {
        /// <summary>Priority assigned at <see cref="IBlurRequestService.Request"/> time. Immutable.</summary>
        int Priority { get; }

        /// <summary>Current spec (read-only snapshot).</summary>
        BlurSpec Current { get; }

        /// <summary>False after <see cref="IDisposable.Dispose"/>.</summary>
        bool IsActive { get; }

        /// <summary>
        /// In-place update of the spec without changing priority. Triggers an immediate volume
        /// re-resolve. Throws after Dispose.
        /// </summary>
        void UpdateSpec(BlurSpec spec);
    }
}
