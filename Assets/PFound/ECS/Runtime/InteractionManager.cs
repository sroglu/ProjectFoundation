using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    public sealed partial class World
    {
        private InteractionManager _interactions;

        /// <summary>Directed entity↔entity relationships grouped by an interaction-type id.</summary>
        public InteractionManager Interactions => _interactions ??= new InteractionManager();
    }

    /// <summary>A directed relationship between two entities for one interaction type.</summary>
    public readonly struct Interaction
    {
        public readonly Entity Interactor;
        public readonly Entity Interactee;
        public Interaction(Entity interactor, Entity interactee) { Interactor = interactor; Interactee = interactee; }
    }

    /// <summary>
    /// Zero-copy view over a contiguous run of interactions. Backed by manager-owned buffers;
    /// valid until the next mutating call (Start/Clear) or the next filtered query that reuses
    /// the shared scratch. Copy out if you need to retain it.
    /// </summary>
    public readonly struct InteractionView
    {
        private readonly Interaction[] _items;
        private readonly int _count;
        internal InteractionView(Interaction[] items, int count) { _items = items; _count = count; }

        public int Count => _count;
        public Interaction this[int i] => _items[i];

        public Enumerator GetEnumerator() => new Enumerator(_items, _count);

        public struct Enumerator
        {
            private readonly Interaction[] _items;
            private readonly int _count;
            private int _i;
            internal Enumerator(Interaction[] items, int count) { _items = items; _count = count; _i = -1; }
            public bool MoveNext() => ++_i < _count;
            public Interaction Current => _items[_i];
        }
    }

    /// <summary>
    /// Stores interactions per type id. Interactions are transient — the owner clears them each
    /// frame (owner-managed lifecycle). Main-thread only; no internal locking.
    ///
    /// The pure-core surface returns a managed <see cref="InteractionView"/>; a Unity adapter may
    /// expose the same data as a <c>NativeArray&lt;Interaction&gt;</c>.
    /// </summary>
    public sealed class InteractionManager
    {
        private sealed class Bucket
        {
            public Interaction[] Items = new Interaction[8];
            public int Count;

            public void Add(in Interaction interaction)
            {
                if (Count == Items.Length) Array.Resize(ref Items, Items.Length * 2);
                Items[Count++] = interaction;
            }

            public void Clear() => Count = 0;
        }

        private readonly Dictionary<int, Bucket> _buckets = new Dictionary<int, Bucket>();
        private static readonly Interaction[] s_empty = new Interaction[0];

        // Shared scratch for filtered queries (OfInteractor); single-level, do not nest.
        private Interaction[] _scratch = new Interaction[8];

        public void Start(int interactionType, Entity interactor, Entity interactee)
        {
            if (!_buckets.TryGetValue(interactionType, out var bucket))
            {
                bucket = new Bucket();
                _buckets[interactionType] = bucket;
            }
            bucket.Add(new Interaction(interactor, interactee));
        }

        /// <summary>All interactions of the given type.</summary>
        public InteractionView Get(int interactionType)
        {
            if (_buckets.TryGetValue(interactionType, out var bucket))
                return new InteractionView(bucket.Items, bucket.Count);
            return new InteractionView(s_empty, 0);
        }

        /// <summary>Interactions of the given type started by <paramref name="interactor"/>.</summary>
        public InteractionView OfInteractor(int interactionType, Entity interactor)
        {
            if (!_buckets.TryGetValue(interactionType, out var bucket))
                return new InteractionView(s_empty, 0);

            int c = 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket.Items[i].Interactor == interactor)
                {
                    if (c == _scratch.Length) Array.Resize(ref _scratch, _scratch.Length * 2);
                    _scratch[c++] = bucket.Items[i];
                }
            }
            return new InteractionView(_scratch, c);
        }

        /// <summary>Invokes <paramref name="callback"/> for each interactee of the interactor.</summary>
        public void ForEachInteractee(int interactionType, Entity interactor, Action<Entity> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!_buckets.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
                if (bucket.Items[i].Interactor == interactor)
                    callback(bucket.Items[i].Interactee);
        }

        /// <summary>Clears every interaction of every type (typical per-frame reset).</summary>
        public void Clear()
        {
            foreach (var bucket in _buckets.Values) bucket.Clear();
        }

        /// <summary>Clears only the interactions of one type.</summary>
        public void Clear(int interactionType)
        {
            if (_buckets.TryGetValue(interactionType, out var bucket)) bucket.Clear();
        }
    }
}
