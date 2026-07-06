using System;
using System.Collections.Generic;

namespace PFound.LoopScheduler
{
    /// <summary>
    /// Engine-independent ordered callback list for a single loop phase. Re-entrancy-safe:
    /// registrations/removals requested while invoking are deferred to after the pass (and a
    /// callback added mid-invoke waits for the next frame). Supports one-shot entries and
    /// owner-tagged entries the host can prune by liveness. Pure C# — testable without Unity.
    /// </summary>
    internal sealed class PhaseCallbacks
    {
        private struct Entry { public Action Callback; public object Owner; public bool Once; }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _pendingAdd = new List<Entry>();
        private readonly List<Action> _pendingRemove = new List<Action>();
        private bool _invoking;

        public int Count => _entries.Count;

        public void Add(Action callback, object owner, bool once)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var entry = new Entry { Callback = callback, Owner = owner, Once = once };
            if (_invoking) _pendingAdd.Add(entry);
            else _entries.Add(entry);
        }

        public void Remove(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (_invoking) _pendingRemove.Add(callback);
            else RemoveNow(callback);
        }

        /// <summary>Runs each callback in registration order; one-shot entries are dropped afterward.</summary>
        public void Invoke()
        {
            _invoking = true;
            int count = _entries.Count; // entries added during the pass wait for next frame
            for (int i = 0; i < count; i++)
            {
                var entry = _entries[i];
                entry.Callback();
                if (entry.Once) _pendingRemove.Add(entry.Callback);
            }
            _invoking = false;
            Flush();
        }

        /// <summary>Drops entries whose owner the host reports dead (e.g. a destroyed Unity object).</summary>
        public void PruneDeadOwners(Func<object, bool> isDead)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var owner = _entries[i].Owner;
                if (owner != null && isDead(owner)) _entries.RemoveAt(i);
            }
        }

        private void RemoveNow(Action callback)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Callback == callback) { _entries.RemoveAt(i); return; }
        }

        private void Flush()
        {
            for (int i = 0; i < _pendingRemove.Count; i++) RemoveNow(_pendingRemove[i]);
            _pendingRemove.Clear();
            if (_pendingAdd.Count > 0) { _entries.AddRange(_pendingAdd); _pendingAdd.Clear(); }
        }
    }
}
