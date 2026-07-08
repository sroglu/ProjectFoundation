using System;

namespace PFound.Render.Effects.Outline
{
    internal sealed class OutlineTicket : IOutlineTicket
    {
        private readonly OutlineRequestService _service;
        private OutlineSpec _current;
        private bool _disposed;

        public OutlineTicket(OutlineRequestService service, int priority, OutlineSpec spec)
        {
            _service = service;
            Priority = priority;
            _current = spec;
        }

        public int Priority { get; }
        public OutlineSpec Current => _current;
        public bool IsActive => !_disposed;

        public void UpdateSpec(OutlineSpec spec)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OutlineTicket));
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
