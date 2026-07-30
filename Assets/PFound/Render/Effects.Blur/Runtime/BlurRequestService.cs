using System;
using System.Collections.Generic;
using PFound.Collections;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Default <see cref="IBlurRequestService"/> implementation. Backed by
    /// <see cref="PriorityQueue{TKey, TValue}"/> from <c>PFound.Collections</c>.
    /// Owner-managed: caller constructs with a volume reference and disposes when done.
    /// </summary>
    public sealed class BlurRequestService : IBlurRequestService
    {
        private readonly BlurStrengthVolumeComponent _volume;
        private readonly PriorityQueue<int, BlurTicket> _queue = new PriorityQueue<int, BlurTicket>();
        private readonly HashSet<int> _activePriorities = new HashSet<int>();
        private bool _disposed;

        public BlurRequestService(BlurStrengthVolumeComponent volume)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            _volume = volume;
        }

        public int ActiveCount => _queue.Count;

        public IBlurTicket Request(int priority, BlurSpec spec)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BlurRequestService));
            if (_activePriorities.Contains(priority))
            {
                throw new InvalidOperationException(
                    $"BlurRequestService: priority {priority} is already in use. Same-priority requests are forbidden — assign unique priorities (UI z-order recommended).");
            }

            var ticket = new BlurTicket(this, priority, spec);
            _queue.Add(priority, ticket);
            _activePriorities.Add(priority);
            Resolve();
            return ticket;
        }

        internal void Release(BlurTicket ticket)
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
                _volume.OverrideQualitySettings.value = false;
                _volume.OverrideQualitySettings.overrideState = true;
                return;
            }

            var top = _queue.Max.Current;
            _volume.Enable.value = true;
            _volume.Enable.overrideState = true;
            _volume.Strength.value = top.Strength;
            _volume.Strength.overrideState = true;
            _volume.OutputMode.value = top.OutputMode;
            _volume.OutputMode.overrideState = true;

            if (top.Downsample.HasValue || top.Iterations.HasValue)
            {
                _volume.OverrideQualitySettings.value = true;
                _volume.OverrideQualitySettings.overrideState = true;
                if (top.Downsample.HasValue)
                {
                    _volume.Downsample.value = top.Downsample.Value;
                    _volume.Downsample.overrideState = true;
                }
                if (top.Iterations.HasValue)
                {
                    _volume.Iterations.value = top.Iterations.Value;
                    _volume.Iterations.overrideState = true;
                }
            }
            else
            {
                _volume.OverrideQualitySettings.value = false;
                _volume.OverrideQualitySettings.overrideState = true;
            }
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
