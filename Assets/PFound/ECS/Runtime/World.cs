using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    /// <summary>
    /// The ECS world: owns entities, their component storage, and registered queries.
    /// Entity/component operations live here directly (no separate manager objects).
    /// Main-thread only; no internal locking by design.
    /// </summary>
    public sealed partial class World : IDisposable
    {
        // Entity table, indexed by entity id.
        private int[] _versions = new int[8];
        private ComponentMask[] _masks = new ComponentMask[8];
        private bool[] _alive = new bool[8];
        private int _nextId;
        private readonly Stack<int> _free = new Stack<int>();

        // Component pools, indexed by component type id.
        private readonly IComponentPool[] _pools = new IComponentPool[ComponentMask.MaxBits];

        // Query registry + a structure-version stamp that invalidates query caches.
        private readonly List<QueryData> _queries = new List<QueryData>();
        private int _structureVersion;

        // Scratch buffer reused by builder ForEach (single-level; do not nest ForEach calls).
        private Entity[] _scratch = new Entity[8];

        /// <summary>Frame delta time, set by the host loop before <see cref="Update"/>.</summary>
        public float DeltaTime;

        // ---- entity lifecycle ----

        public Entity Create()
        {
            int id;
            if (_free.Count > 0)
            {
                id = _free.Pop();
            }
            else
            {
                id = _nextId++;
                EnsureEntityCapacity(id);
            }
            _alive[id] = true;
            _masks[id].Reset();
            _structureVersion++;
            return new Entity(id, _versions[id]);
        }

        public void Destroy(Entity entity)
        {
            if (!IsAlive(entity)) return;
            int id = entity.Id;
            ref var mask = ref _masks[id];
            for (int t = 0; t < ComponentMask.MaxBits; t++)
            {
                if (mask.Get(t)) _pools[t].Remove(id);
            }
            mask.Reset();
            _alive[id] = false;
            _versions[id]++;
            _free.Push(id);
            _structureVersion++;
        }

        public void DestroyAll()
        {
            for (int id = 0; id < _nextId; id++)
            {
                if (_alive[id]) Destroy(new Entity(id, _versions[id]));
            }
        }

        /// <summary>True if the handle's version matches the live slot (not stale).</summary>
        public bool IsValid(Entity e) => e.Id >= 0 && e.Id < _versions.Length && _versions[e.Id] == e.Version;

        /// <summary>True if the entity is valid and not destroyed.</summary>
        public bool IsAlive(Entity e) => IsValid(e) && _alive[e.Id];

        private void EnsureEntityCapacity(int id)
        {
            if (id < _versions.Length) return;
            int n = _versions.Length;
            while (n <= id) n *= 2;
            Array.Resize(ref _versions, n);
            Array.Resize(ref _masks, n);
            Array.Resize(ref _alive, n);
        }

        private void CheckAlive(Entity e)
        {
            if (!IsAlive(e)) throw new InvalidOperationException(e + " is not alive.");
        }

        // ---- components ----

        private ComponentPool<T> Pool<T>() where T : struct
        {
            int t = ComponentType<T>.Id;
            var p = _pools[t];
            if (p == null)
            {
                var pool = new ComponentPool<T>(t);
                _pools[t] = pool;
                return pool;
            }
            return (ComponentPool<T>)p;
        }

        public void Add<T>(Entity e, in T value) where T : struct
        {
            CheckAlive(e);
            int t = ComponentType<T>.Id;
            bool had = _masks[e.Id].Get(t);
            Pool<T>().Add(e.Id, value);
            if (!had)
            {
                _masks[e.Id].Set(t);
                _structureVersion++;
            }
        }

        public bool AddIfMissing<T>(Entity e, in T value) where T : struct
        {
            if (Has<T>(e)) return false;
            Add(e, value);
            return true;
        }

        public void SetOrAdd<T>(Entity e, in T value) where T : struct => Add(e, value);

        public ref T Get<T>(Entity e) where T : struct
        {
            CheckAlive(e);
            if (!_masks[e.Id].Get(ComponentType<T>.Id))
                throw new InvalidOperationException(e + " has no " + typeof(T).Name + " component.");
            return ref Pool<T>().Get(e.Id);
        }

        public bool TryGet<T>(Entity e, out T value) where T : struct
        {
            if (Has<T>(e)) { value = Pool<T>().Get(e.Id); return true; }
            value = default;
            return false;
        }

        public bool Has<T>(Entity e) where T : struct =>
            IsAlive(e) && _masks[e.Id].Get(ComponentType<T>.Id);

        public void Remove<T>(Entity e) where T : struct
        {
            CheckAlive(e);
            int t = ComponentType<T>.Id;
            if (_masks[e.Id].Get(t))
            {
                Pool<T>().Remove(e.Id);
                _masks[e.Id].Clear(t);
                _structureVersion++;
            }
        }

        public bool RemoveIfExists<T>(Entity e) where T : struct
        {
            if (!Has<T>(e)) return false;
            Remove<T>(e);
            return true;
        }

        /// <summary>
        /// Bulk read: the packed dense array of every <typeparamref name="T"/> component as a
        /// <see cref="Span{T}"/>. Zero-copy over live storage — writes through the span mutate the
        /// components in place. The span is entity-agnostic (dense order, not entity-id order) and
        /// is invalidated by the next structural change to the <typeparamref name="T"/> pool.
        /// </summary>
        public Span<T> GetAllComponents<T>() where T : struct => Pool<T>().Values();

        // ---- queries ----

        public QueryBuilder<T1> Query<T1>() where T1 : struct
        {
            var inc = default(ComponentMask); inc.Set(ComponentType<T1>.Id);
            return new QueryBuilder<T1>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id);
            return new QueryBuilder<T1, T2>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : struct where T2 : struct where T3 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id);
            return new QueryBuilder<T1, T2, T3>(this, inc, default, default);
        }

        internal QueryId RegisterQuery(in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            // Dedupe identical signatures so repeated Build() calls don't leak queries.
            for (int i = 0; i < _queries.Count; i++)
            {
                if (_queries[i].Include.Equals(include) && _queries[i].Exclude.Equals(exclude)
                    && _queries[i].Interaction.Equals(interaction))
                    return new QueryId(i);
            }
            _queries.Add(new QueryData { Include = include, Exclude = exclude, Interaction = interaction });
            return new QueryId(_queries.Count - 1);
        }

        private void RebuildIfStale(QueryData q)
        {
            // Interaction-scoped queries also invalidate when the interaction store mutates.
            int iv = q.Interaction.Active && _interactions != null ? _interactions.Version : 0;
            if (q.Version == _structureVersion && q.InteractionVersion == iv) return;
            q.Count = 0;
            for (int id = 0; id < _nextId; id++)
            {
                if (!_alive[id]) continue;
                ref var m = ref _masks[id];
                if (m.ContainsAll(q.Include) && !m.ContainsAny(q.Exclude)
                    && MatchesInteraction(q.Interaction, id))
                {
                    if (q.Count == q.Cache.Length) Array.Resize(ref q.Cache, q.Cache.Length * 2);
                    q.Cache[q.Count++] = new Entity(id, _versions[id]);
                }
            }
            q.Version = _structureVersion;
            q.InteractionVersion = iv;
        }

        // True if the entity satisfies an (optional) interaction-role filter.
        private bool MatchesInteraction(in InteractionFilter filter, int entityId)
        {
            if (!filter.Active) return true;
            return Interactions.HasRole(filter.Type, new Entity(entityId, _versions[entityId]), filter.Role);
        }

        public QueryResult Entities(QueryId id)
        {
            var q = _queries[id.Index];
            RebuildIfStale(q);
            return new QueryResult(q.Cache, q.Count);
        }

        /// <summary>Forces query caches to rebuild on next access. For tests.</summary>
        public void FlushQueries() => _structureVersion++;

        internal int MatchInto(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, ref Entity[] buffer)
        {
            int c = 0;
            for (int id = 0; id < _nextId; id++)
            {
                if (!_alive[id]) continue;
                ref var m = ref _masks[id];
                if (m.ContainsAll(inc) && !m.ContainsAny(exc) && MatchesInteraction(filter, id))
                {
                    if (c == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
                    buffer[c++] = new Entity(id, _versions[id]);
                }
            }
            return c;
        }

        internal void ForEach<T1>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1> action)
            where T1 : struct
        {
            var p1 = Pool<T1>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2> action)
            where T1 : struct where T2 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3> action)
            where T1 : struct where T2 : struct where T3 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id));
            }
        }

        public void Dispose()
        {
            DisposeSystems();
            Array.Clear(_pools, 0, _pools.Length);
            _queries.Clear();
            _free.Clear();
            _nextId = 0;
            _events?.Clear();
            _interactions?.Clear();
            _systems.Clear();
            _byType.Clear();
            _ordered = Array.Empty<SystemBase>();
            _systemsDirty = false;
        }
    }
}
