using System;
using System.Collections.Generic;
using PFound.Collections;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Default <see cref="IOutlineRequestService"/> implementation. Backed by
    /// <see cref="PriorityQueue{TKey, TValue}"/>. Owner-managed: caller constructs with a volume
    /// reference and disposes when done.
    /// </summary>
    public sealed class OutlineRequestService : IOutlineRequestService
    {
        private readonly OutlineVolumeComponent _volume;
        private readonly PriorityQueue<int, OutlineTicket> _queue = new PriorityQueue<int, OutlineTicket>();
        private readonly HashSet<int> _activePriorities = new HashSet<int>();
        private bool _disposed;

        public OutlineRequestService(OutlineVolumeComponent volume)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            _volume = volume;
        }

        public int ActiveCount => _queue.Count;

        public IOutlineTicket Request(int priority, OutlineSpec spec)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OutlineRequestService));
            if (_activePriorities.Contains(priority))
            {
                throw new InvalidOperationException(
                    $"OutlineRequestService: priority {priority} is already in use. Same-priority requests are forbidden.");
            }

            var ticket = new OutlineTicket(this, priority, spec);
            _queue.Add(priority, ticket);
            _activePriorities.Add(priority);
            Resolve();
            return ticket;
        }

        internal void Release(OutlineTicket ticket)
        {
            if (_disposed) return;
            if (ticket == null) return;
            if (!_queue.Remove(ticket)) return;
            _activePriorities.Remove(ticket.Priority);
            Resolve();
        }

        internal void NotifySpecUpdated()
        {
            if (_disposed) return;
            Resolve();
        }

        private void Resolve()
        {
            if (_queue.IsEmpty)
            {
                _volume.Enable.value = false;
                _volume.Enable.overrideState = true;
                _volume.Strength.value = 0f;
                _volume.Strength.overrideState = true;
                return;
            }

            var top = _queue.Max.Current;
            _volume.Enable.value = true;
            _volume.Enable.overrideState = true;
            _volume.Strength.value = top.Strength;
            _volume.Strength.overrideState = true;
            _volume.EdgeColor.value = top.EdgeColor;
            _volume.EdgeColor.overrideState = true;
            _volume.Thickness.value = top.Thickness;
            _volume.Thickness.overrideState = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _volume.Enable.value = false;
            _volume.Enable.overrideState = true;
            _volume.Strength.value = 0f;
            _volume.Strength.overrideState = true;
        }
    }
}
