using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    /// <summary>Batch event handler: receives every event of a type queued this round in one call.</summary>
    public delegate void SpanAction<T>(Span<T> events) where T : struct;

    public sealed partial class World
    {
        private EventManager _events;

        /// <summary>Typed publish/subscribe event hub with deferred dispatch.</summary>
        public EventManager Events => _events ??= new EventManager();
    }

    /// <summary>
    /// Typed event hub. <see cref="Publish{T}"/> queues an event by value; <see cref="Dispatch"/>
    /// delivers everything queued so far to its subscribers, in publish order, then empties the
    /// queue. Events published while a handler runs are held for the next <see cref="Dispatch"/>,
    /// so dispatch always terminates. Per-type channels avoid boxing. Main-thread only.
    /// </summary>
    public sealed class EventManager
    {
        private interface IEventChannel { void Dispatch(); void Clear(); int Pending { get; } }

        private sealed class EventChannel<T> : IEventChannel where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private readonly List<SpanAction<T>> _batchHandlers = new List<SpanAction<T>>();
            private readonly List<T> _queue = new List<T>();
            private T[] _batch = new T[8]; // scratch copy handed to batch handlers as a Span

            public int Pending => _queue.Count;

            public IDisposable Subscribe(Action<T> handler)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _handlers.Add(handler);
                return new Subscription(this, handler);
            }

            public IDisposable SubscribeBatch(SpanAction<T> handler)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _batchHandlers.Add(handler);
                return new BatchSubscription(this, handler);
            }

            public void Publish(in T evt) => _queue.Add(evt);

            public void Dispatch()
            {
                // Snapshot the count so events published by handlers this round wait for the
                // next Dispatch (they land at index >= count and survive the RemoveRange).
                int count = _queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var evt = _queue[i];
                    for (int h = 0; h < _handlers.Count; h++) _handlers[h](evt);
                }

                // Batch delivery: every event of this type this round, in one Span call.
                if (_batchHandlers.Count > 0 && count > 0)
                {
                    if (_batch.Length < count) _batch = new T[count];
                    for (int i = 0; i < count; i++) _batch[i] = _queue[i];
                    var span = new Span<T>(_batch, 0, count);
                    for (int h = 0; h < _batchHandlers.Count; h++) _batchHandlers[h](span);
                }

                _queue.RemoveRange(0, count);
            }

            public void Clear() { _handlers.Clear(); _batchHandlers.Clear(); _queue.Clear(); }

            private void Remove(Action<T> handler) => _handlers.Remove(handler);
            private void RemoveBatch(SpanAction<T> handler) => _batchHandlers.Remove(handler);

            private sealed class Subscription : IDisposable
            {
                private EventChannel<T> _channel;
                private readonly Action<T> _handler;
                public Subscription(EventChannel<T> channel, Action<T> handler) { _channel = channel; _handler = handler; }
                public void Dispose() { _channel?.Remove(_handler); _channel = null; }
            }

            private sealed class BatchSubscription : IDisposable
            {
                private EventChannel<T> _channel;
                private readonly SpanAction<T> _handler;
                public BatchSubscription(EventChannel<T> channel, SpanAction<T> handler) { _channel = channel; _handler = handler; }
                public void Dispose() { _channel?.RemoveBatch(_handler); _channel = null; }
            }
        }

        // Keyed by event Type, not ComponentType id — events are not components and must not
        // consume the 256-slot component id space.
        private readonly Dictionary<Type, IEventChannel> _channels = new Dictionary<Type, IEventChannel>();

        /// <summary>Total queued-but-undispatched events across all event types.</summary>
        public int PendingCount
        {
            get { int n = 0; foreach (var c in _channels.Values) n += c.Pending; return n; }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct => Channel<T>().Subscribe(handler);

        /// <summary>Subscribes a batch handler: on <see cref="Dispatch"/> it receives all queued
        /// events of type <typeparamref name="T"/> as a single <see cref="Span{T}"/>.</summary>
        public IDisposable SubscribeBatch<T>(SpanAction<T> handler) where T : struct => Channel<T>().SubscribeBatch(handler);

        public void Publish<T>(in T evt) where T : struct => Channel<T>().Publish(evt);

        /// <summary>Delivers all events queued so far to their subscribers, then empties the queues.</summary>
        public void Dispatch()
        {
            foreach (var channel in _channels.Values) channel.Dispatch();
        }

        internal void Clear()
        {
            foreach (var channel in _channels.Values) channel.Clear();
            _channels.Clear();
        }

        private EventChannel<T> Channel<T>() where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out var c)) return (EventChannel<T>)c;
            var channel = new EventChannel<T>();
            _channels[typeof(T)] = channel;
            return channel;
        }
    }
}
