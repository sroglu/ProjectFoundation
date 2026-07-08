using System;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Owner-managed handle returned by <see cref="IOutlineRequestService.Request"/>. Dispose
    /// removes this request from the priority queue; the next-highest priority (if any) takes
    /// over the volume state. Idempotent — calling Dispose twice is safe.
    /// </summary>
    public interface IOutlineTicket : IDisposable
    {
        int Priority { get; }
        OutlineSpec Current { get; }
        bool IsActive { get; }

        /// <summary>
        /// In-place update of the spec without changing priority. Triggers an immediate volume
        /// re-resolve. Throws after Dispose.
        /// </summary>
        void UpdateSpec(OutlineSpec spec);
    }
}
