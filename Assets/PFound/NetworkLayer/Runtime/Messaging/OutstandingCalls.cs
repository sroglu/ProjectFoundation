using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Client-side correlation table: maps a call token to the promise the caller
    /// is awaiting, together with the wall time past which the call is abandoned.
    /// <see cref="ExpireDue"/> is driven from the peer's pump so timeouts fire on
    /// the caller's own thread with no timers. This is the client half of the two
    /// independent timeout mechanisms (the server runs its own watchdog).
    ///
    /// Every way a call leaves the table — settled, faulted, expired, or dropped —
    /// routes through <see cref="Closed"/>, giving the owning peer one place to stop
    /// its round-trip timer regardless of outcome.
    /// </summary>
    public sealed class OutstandingCalls
    {
        struct Slot
        {
            public TaskCompletionSource<Message> Promise;
            public long DeadlineMs;
        }

        readonly Dictionary<uint, Slot> _slots = new Dictionary<uint, Slot>();
        readonly List<uint> _expiredScratch = new List<uint>();

        /// <summary>Raised once for each token as it leaves the table, whatever the
        /// outcome. The peer uses it to close the call's round-trip measurement.</summary>
        public event Action<uint> Closed;

        void Close(uint token) => Closed?.Invoke(token);

        public void Open(uint token, TaskCompletionSource<Message> promise, long deadlineMs)
        {
            _slots.Add(token, new Slot { Promise = promise, DeadlineMs = deadlineMs });
        }

        /// <summary>Deliver a reply to its waiter. Unknown tokens (late/expired) are dropped.</summary>
        public bool Settle(uint token, Message reply)
        {
            if (!_slots.TryGetValue(token, out var slot))
                return false;
            _slots.Remove(token);
            Close(token);
            slot.Promise.TrySetResult(reply);
            return true;
        }

        /// <summary>Fault a specific call (e.g. remote refusal signaled by control reply).</summary>
        public bool Break(uint token, NetworkFault fault)
        {
            if (!_slots.TryGetValue(token, out var slot))
                return false;
            _slots.Remove(token);
            Close(token);
            slot.Promise.TrySetException(fault);
            return true;
        }

        public void ExpireDue(long nowMs)
        {
            _expiredScratch.Clear();
            foreach (var pair in _slots)
                if (nowMs >= pair.Value.DeadlineMs)
                    _expiredScratch.Add(pair.Key);

            foreach (var token in _expiredScratch)
            {
                var promise = _slots[token].Promise;
                _slots.Remove(token);
                Close(token);
                promise.TrySetException(new CallExpiredFault());
            }
        }

        /// <summary>Fault every outstanding call; used when the link drops.</summary>
        public void BreakAll(NetworkFault fault)
        {
            // Snapshot the tokens first: Close runs listeners that may touch the peer,
            // and we must not mutate the dictionary while enumerating it.
            _expiredScratch.Clear();
            foreach (var pair in _slots)
                _expiredScratch.Add(pair.Key);

            foreach (var token in _expiredScratch)
            {
                var promise = _slots[token].Promise;
                promise.TrySetException(fault);
            }
            _slots.Clear();

            foreach (var token in _expiredScratch)
                Close(token);
        }

        public int Count => _slots.Count;
    }
}
