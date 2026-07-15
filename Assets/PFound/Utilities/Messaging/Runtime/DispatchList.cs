using System;
using System.Collections.Generic;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// Invokes a listener delegate. Implemented as a value type by the channels so a
    /// raise carries its payload without allocating a closure.
    /// </summary>
    internal interface IDispatch<TDelegate>
    {
        void Fire(TDelegate listener);
    }

    /// <summary>
    /// Engine-free ordered listener list shared by the channels. Handles stable
    /// priority ordering, once/persistent lifetimes, guard liveness, re-entrancy-safe
    /// structural edits, and the "detach the currently firing listener" case.
    ///
    /// Re-entrancy model: while a raise is on the stack, structural edits do not
    /// mutate the backing list in place. Removals tombstone their slot; additions land
    /// in a pending buffer. When the outermost raise unwinds, the list is compacted and
    /// pending additions are merged. This keeps every active iteration index valid and
    /// guarantees a listener added mid-raise is not seen by the raise that added it.
    /// </summary>
    internal sealed class DispatchList<TDelegate> where TDelegate : class
    {
        internal sealed class Slot
        {
            public TDelegate Listener;
            public int Order;
            public long Seq;      // stable tie-breaker for equal orders
            public bool Once;
            public object Guard;  // liveness anchor; null => no guard
            public bool Dead;     // tombstone, cleared during compaction
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private List<Slot> _pending;   // additions deferred while raising
        private long _nextSeq;
        private int _raiseDepth;
        private bool _needsSweep;
        private Slot _firing;          // the slot whose listener is executing right now

        /// <summary>Number of live listeners (tombstones and pending adds excluded).</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].Dead)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        public Slot Attach(TDelegate listener, int order, bool once, object guard)
        {
            if (listener == null)
            {
                throw new ArgumentNullException("listener");
            }

            Slot slot = new Slot
            {
                Listener = listener,
                Order = order,
                Seq = _nextSeq++,
                Once = once,
                Guard = guard,
                Dead = false,
            };

            if (_raiseDepth > 0)
            {
                if (_pending == null)
                {
                    _pending = new List<Slot>();
                }
                _pending.Add(slot);
            }
            else
            {
                InsertSorted(_slots, slot);
            }

            return slot;
        }

        private static void InsertSorted(List<Slot> list, Slot slot)
        {
            // Linear insert keeping (Order asc, Seq asc). Registration order is
            // preserved for equal orders because Seq is monotonic.
            int i = list.Count - 1;
            while (i >= 0)
            {
                Slot at = list[i];
                if (at.Order < slot.Order || (at.Order == slot.Order && at.Seq <= slot.Seq))
                {
                    break;
                }
                i--;
            }
            list.Insert(i + 1, slot);
        }

        /// <summary>Tombstones a specific slot handle. Returns true if it was live.</summary>
        public bool Detach(Slot slot)
        {
            if (slot == null || slot.Dead)
            {
                return false;
            }
            slot.Dead = true;
            AfterStructuralEdit();
            return true;
        }

        /// <summary>Tombstones the first live slot whose delegate equals <paramref name="listener"/>.</summary>
        public bool DetachFirst(TDelegate listener)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (!s.Dead && Equals(s.Listener, listener))
                {
                    return Detach(s);
                }
            }
            if (_pending != null)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    Slot s = _pending[i];
                    if (!s.Dead && Equals(s.Listener, listener))
                    {
                        s.Dead = true;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Tombstones every live slot anchored to <paramref name="guard"/> or whose delegate targets it.</summary>
        public int DetachByGuard(object guard)
        {
            if (guard == null)
            {
                return 0;
            }
            int removed = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (!s.Dead && Targets(s, guard))
                {
                    s.Dead = true;
                    removed++;
                }
            }
            if (_pending != null)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    Slot s = _pending[i];
                    if (!s.Dead && Targets(s, guard))
                    {
                        s.Dead = true;
                        removed++;
                    }
                }
            }
            if (removed > 0)
            {
                AfterStructuralEdit();
            }
            return removed;
        }

        private static bool Targets(Slot s, object guard)
        {
            if (ReferenceEquals(s.Guard, guard))
            {
                return true;
            }
            Delegate d = s.Listener as Delegate;
            return d != null && ReferenceEquals(d.Target, guard);
        }

        /// <summary>Tombstones the slot currently executing. Valid only from inside a listener.</summary>
        public bool DetachFiring()
        {
            return Detach(_firing);
        }

        /// <summary>Drops all listeners.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Dead = true;
            }
            if (_pending != null)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    _pending[i].Dead = true;
                }
            }
            AfterStructuralEdit();
        }

        /// <summary>Tombstones listeners whose guard has become stale (e.g. destroyed).</summary>
        public int Prune()
        {
            int removed = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (!s.Dead && MessagingEnvironment.IsStale(GuardOf(s)))
                {
                    s.Dead = true;
                    removed++;
                }
            }
            if (removed > 0)
            {
                AfterStructuralEdit();
            }
            return removed;
        }

        private static object GuardOf(Slot s)
        {
            if (s.Guard != null)
            {
                return s.Guard;
            }
            Delegate d = s.Listener as Delegate;
            return d != null ? d.Target : null;
        }

        /// <summary>
        /// Iterates live listeners in order and fires each. A stale-guard slot is
        /// dropped instead of fired. When <paramref name="protect"/> is set each call is
        /// wrapped so a throwing listener is reported and the rest still run; otherwise
        /// the exception propagates (structural state is still restored by the finally).
        /// </summary>
        public void Raise<TDispatch>(TDispatch dispatch, bool protect) where TDispatch : IDispatch<TDelegate>
        {
            Slot outerFiring = _firing;
            _raiseDepth++;
            try
            {
                // Snapshot the count so mid-raise additions (which go to _pending) are
                // never visited by this raise. Existing slots keep their indices because
                // removals only tombstone.
                int upper = _slots.Count;
                for (int i = 0; i < upper; i++)
                {
                    Slot s = _slots[i];
                    if (s.Dead)
                    {
                        continue;
                    }

                    if (MessagingEnvironment.IsStale(GuardOf(s)))
                    {
                        s.Dead = true;
                        _needsSweep = true;
                        continue;
                    }

                    if (s.Once)
                    {
                        // Retire before firing so a re-entrant raise won't fire it again.
                        s.Dead = true;
                        _needsSweep = true;
                    }

                    _firing = s;
                    if (protect)
                    {
                        try
                        {
                            dispatch.Fire(s.Listener);
                        }
                        catch (Exception error)
                        {
                            MessagingEnvironment.ReportFault(error);
                        }
                    }
                    else
                    {
                        dispatch.Fire(s.Listener);
                    }
                }
            }
            finally
            {
                _firing = outerFiring;
                _raiseDepth--;
                if (_raiseDepth == 0)
                {
                    Consolidate();
                }
            }
        }

        private void AfterStructuralEdit()
        {
            if (_raiseDepth > 0)
            {
                _needsSweep = true;
            }
            else
            {
                Consolidate();
            }
        }

        private void Consolidate()
        {
            // Compact tombstones.
            if (_needsSweep)
            {
                int w = 0;
                for (int r = 0; r < _slots.Count; r++)
                {
                    Slot s = _slots[r];
                    if (!s.Dead)
                    {
                        _slots[w++] = s;
                    }
                }
                _slots.RemoveRange(w, _slots.Count - w);
                _needsSweep = false;
            }

            // Merge deferred additions (drop any tombstoned before they went live).
            if (_pending != null && _pending.Count > 0)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    Slot s = _pending[i];
                    if (!s.Dead)
                    {
                        InsertSorted(_slots, s);
                    }
                }
                _pending.Clear();
            }
        }
    }
}
