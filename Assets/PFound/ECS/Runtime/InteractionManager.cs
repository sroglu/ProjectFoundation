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
        /// <summary>Manager-assigned id, unique among currently-live interactions.</summary>
        public readonly int Id;
        /// <summary>The interaction-type key (see <see cref="InteractionType"/>).</summary>
        public readonly int Type;
        public readonly Entity Interactor;
        public readonly Entity Interactee;

        internal Interaction(int id, int type, Entity interactor, Entity interactee)
        {
            Id = id; Type = type; Interactor = interactor; Interactee = interactee;
        }
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
    /// Stores directed interactions keyed by an interaction-type id, with bidirectional
    /// (interactor↔interactee) lookup, per-interaction completion state, a dirty-type set, and
    /// existence/role queries. Interactions are transient — the owner typically clears them each
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

            public void RemoveById(int id)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Items[i].Id == id)
                    {
                        Items[i] = Items[--Count];
                        Items[Count] = default;
                        return;
                    }
                }
            }

            public void Clear() => Count = 0;
        }

        // Composite dedupe/existence key: (type, interactor id, interactee id).
        private readonly struct Key : IEquatable<Key>
        {
            public readonly int Type;
            public readonly int Interactor;
            public readonly int Interactee;
            public Key(int type, int interactor, int interactee) { Type = type; Interactor = interactor; Interactee = interactee; }
            public bool Equals(Key o) => Type == o.Type && Interactor == o.Interactor && Interactee == o.Interactee;
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode() { unchecked { int h = Type; h = h * 397 ^ Interactor; h = h * 397 ^ Interactee; return h; } }
        }

        private readonly Dictionary<int, Bucket> _byType = new Dictionary<int, Bucket>();
        private readonly Dictionary<Key, int> _idByKey = new Dictionary<Key, int>();
        private readonly Dictionary<int, Interaction> _byId = new Dictionary<int, Interaction>();
        private readonly HashSet<int> _completed = new HashSet<int>();

        // Per-entity role membership: entity id → set of interaction types it plays that role in.
        private readonly Dictionary<int, HashSet<int>> _asInteractor = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, HashSet<int>> _asInteractee = new Dictionary<int, HashSet<int>>();

        private readonly HashSet<int> _dirtyTypes = new HashSet<int>();

        private static readonly Interaction[] s_empty = new Interaction[0];
        private Interaction[] _scratch = new Interaction[8]; // shared by filtered queries; single-level

        private int _nextId;
        private int _version; // bumped on every structural mutation; drives interaction-query staleness

        /// <summary>Monotonic stamp incremented on every Start/Complete/Clear — invalidates cached interaction queries.</summary>
        public int Version => _version;

        // ---- start / dedupe ----

        /// <summary>
        /// Records an interaction of <paramref name="interactionType"/> from
        /// <paramref name="interactor"/> to <paramref name="interactee"/>. A duplicate active
        /// interaction is a no-op; a duplicate that was completed is reactivated.
        /// </summary>
        public void Start(int interactionType, Entity interactor, Entity interactee)
        {
            var key = new Key(interactionType, interactor.Id, interactee.Id);
            if (_idByKey.TryGetValue(key, out int existingId))
            {
                if (_completed.Remove(existingId)) // was completed → reactivate
                {
                    MarkDirty(interactionType);
                    _version++;
                }
                return;
            }

            int id = _nextId++;
            var interaction = new Interaction(id, interactionType, interactor, interactee);

            Type(interactionType).Add(interaction);
            _idByKey[key] = id;
            _byId[id] = interaction;
            RoleSet(_asInteractor, interactor.Id).Add(interactionType);
            RoleSet(_asInteractee, interactee.Id).Add(interactionType);
            MarkDirty(interactionType);
            _version++;
        }

        private Bucket Type(int interactionType)
        {
            if (!_byType.TryGetValue(interactionType, out var bucket))
            {
                bucket = new Bucket();
                _byType[interactionType] = bucket;
            }
            return bucket;
        }

        private static HashSet<int> RoleSet(Dictionary<int, HashSet<int>> map, int entityId)
        {
            if (!map.TryGetValue(entityId, out var set))
            {
                set = new HashSet<int>();
                map[entityId] = set;
            }
            return set;
        }

        // ---- reads: all / by role ----

        /// <summary>All interactions of the given type.</summary>
        public InteractionView Get(int interactionType)
        {
            if (_byType.TryGetValue(interactionType, out var bucket))
                return new InteractionView(bucket.Items, bucket.Count);
            return new InteractionView(s_empty, 0);
        }

        /// <summary>Interactions of the given type started by <paramref name="interactor"/>.</summary>
        public InteractionView OfInteractor(int interactionType, Entity interactor)
            => Filter(interactionType, interactor, byInteractor: true);

        /// <summary>Interactions of the given type targeting <paramref name="interactee"/> (reverse lookup).</summary>
        public InteractionView OfInteractee(int interactionType, Entity interactee)
            => Filter(interactionType, interactee, byInteractor: false);

        private InteractionView Filter(int interactionType, Entity entity, bool byInteractor)
        {
            if (!_byType.TryGetValue(interactionType, out var bucket))
                return new InteractionView(s_empty, 0);

            int c = 0;
            for (int i = 0; i < bucket.Count; i++)
            {
                var it = bucket.Items[i];
                bool match = byInteractor ? it.Interactor == entity : it.Interactee == entity;
                if (match)
                {
                    if (c == _scratch.Length) Array.Resize(ref _scratch, _scratch.Length * 2);
                    _scratch[c++] = it;
                }
            }
            return new InteractionView(_scratch, c);
        }

        /// <summary>Invokes <paramref name="callback"/> for each interactee of the interactor.</summary>
        public void ForEachInteractee(int interactionType, Entity interactor, Action<Entity> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!_byType.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
                if (bucket.Items[i].Interactor == interactor)
                    callback(bucket.Items[i].Interactee);
        }

        /// <summary>Invokes <paramref name="callback"/> for each interactor targeting the interactee (reverse).</summary>
        public void ForEachInteractor(int interactionType, Entity interactee, Action<Entity> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!_byType.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
                if (bucket.Items[i].Interactee == interactee)
                    callback(bucket.Items[i].Interactor);
        }

        // ---- existence / role ----

        /// <summary>True if an interaction of that exact type/interactor/interactee exists.</summary>
        public bool Exists(int interactionType, Entity interactor, Entity interactee)
            => _idByKey.ContainsKey(new Key(interactionType, interactor.Id, interactee.Id));

        /// <summary>True if <paramref name="entity"/> plays either role in any interaction of the type.</summary>
        public bool HasInteractionOfType(int interactionType, Entity entity)
            => HasRole(interactionType, entity, InteractionRole.Interactor)
               || HasRole(interactionType, entity, InteractionRole.Interactee);

        /// <summary>True if <paramref name="entity"/> plays <paramref name="role"/> in an interaction of the type.</summary>
        public bool HasRole(int interactionType, Entity entity, InteractionRole role)
        {
            var map = role == InteractionRole.Interactor ? _asInteractor : _asInteractee;
            return map.TryGetValue(entity.Id, out var set) && set.Contains(interactionType);
        }

        // ---- completion state ----

        /// <summary>Marks an interaction complete (it is dropped on the owner's next per-frame clear).</summary>
        public void CompleteInteraction(in Interaction interaction)
        {
            if (_completed.Add(interaction.Id))
            {
                MarkDirty(interaction.Type);
                _version++;
            }
        }

        /// <summary>True if the interaction has been completed.</summary>
        public bool IsComplete(in Interaction interaction) => _completed.Contains(interaction.Id);

        /// <summary>Completes every interaction of the type started by <paramref name="interactor"/>.</summary>
        public void CompleteAllOfInteractor(int interactionType, Entity interactor)
        {
            if (!_byType.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
                if (bucket.Items[i].Interactor == interactor)
                    CompleteInteraction(bucket.Items[i]);
        }

        /// <summary>Completes every interaction of the given type.</summary>
        public void CompleteAllOfType(int interactionType)
        {
            if (!_byType.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
                CompleteInteraction(bucket.Items[i]);
        }

        // ---- dirty tracking ----

        private void MarkDirty(int interactionType) => _dirtyTypes.Add(interactionType);

        /// <summary>True if any interaction of the type changed since the last <see cref="ClearDirtyMask"/>.</summary>
        public bool IsDirty(int interactionType) => _dirtyTypes.Contains(interactionType);

        /// <summary>The interaction types touched (started/completed/cleared) since the last <see cref="ClearDirtyMask"/>.</summary>
        public IReadOnlyCollection<int> DirtyTypes => _dirtyTypes;

        /// <summary>Resets the dirty-type set (typically at the end of a frame).</summary>
        public void ClearDirtyMask() => _dirtyTypes.Clear();

        // ---- clear ----

        /// <summary>Clears every interaction of every type (typical per-frame reset).</summary>
        public void Clear()
        {
            _byType.Clear();
            _idByKey.Clear();
            _byId.Clear();
            _completed.Clear();
            _asInteractor.Clear();
            _asInteractee.Clear();
            _dirtyTypes.Clear();
            _nextId = 0;
            _version++;
        }

        /// <summary>Clears only the interactions of one type.</summary>
        public void Clear(int interactionType)
        {
            if (!_byType.TryGetValue(interactionType, out var bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
            {
                var it = bucket.Items[i];
                _idByKey.Remove(new Key(it.Type, it.Interactor.Id, it.Interactee.Id));
                _byId.Remove(it.Id);
                _completed.Remove(it.Id);
                if (_asInteractor.TryGetValue(it.Interactor.Id, out var inSet)) inSet.Remove(interactionType);
                if (_asInteractee.TryGetValue(it.Interactee.Id, out var outSet)) outSet.Remove(interactionType);
            }
            bucket.Clear();
            MarkDirty(interactionType);
            _version++;
        }
    }
}
