using System;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Internal ticket impl. The service removes it from the priority queue by reference on Dispose.
    /// </summary>
    internal sealed class BlurTicket : IBlurTicket
    {
        private readonly BlurRequestService _service;
        private BlurSpec _current;
        private bool _disposed;

        public BlurTicket(BlurRequestService service, int priority, BlurSpec spec)
        {
            _service = service;
            Priority = priority;
            _current = spec;
        }

        public int Priority { get; }
        public BlurSpec Current => _current;
        public bool IsActive => !_disposed;

        public void UpdateSpec(BlurSpec spec)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BlurTicket));
            _current = spec;
            _service.NotifySpecUpdated();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.Release(this);
        }
    }
}
