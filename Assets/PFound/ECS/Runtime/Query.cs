using System;

namespace PFound.ECS
{
    /// <summary>Opaque, cacheable handle to a registered query. Store in a system field.</summary>
    public readonly struct QueryId
    {
        public readonly int Index;
        internal QueryId(int index) { Index = index; }
    }

    /// <summary>
    /// Zero-allocation view over a query's matched entities. Backed by the world's cached
    /// buffer — valid until the next structural change; copy out if you need to retain it.
    /// </summary>
    public readonly struct QueryResult
    {
        private readonly Entity[] _items;
        private readonly int _count;

        internal QueryResult(Entity[] items, int count) { _items = items; _count = count; }

        public int Count => _count;
        public Entity this[int i] => _items[i];

        public Enumerator GetEnumerator() => new Enumerator(_items, _count);

        public struct Enumerator
        {
            private readonly Entity[] _items;
            private readonly int _count;
            private int _i;

            internal Enumerator(Entity[] items, int count) { _items = items; _count = count; _i = -1; }

            public bool MoveNext() => ++_i < _count;
            public Entity Current => _items[_i];
        }
    }

    public delegate void RefAction<T1>(World world, Entity entity, ref T1 c1);
    public delegate void RefAction<T1, T2>(World world, Entity entity, ref T1 c1, ref T2 c2);
    public delegate void RefAction<T1, T2, T3>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3);

    /// <summary>Internal record of a registered query's signature and cached result.</summary>
    internal sealed class QueryData
    {
        public ComponentMask Include;
        public ComponentMask Exclude;
        public InteractionFilter Interaction;      // optional interaction-role predicate
        public Entity[] Cache = new Entity[8];
        public int Count;
        public int Version = -1;                   // structure version the cache was built at
        public int InteractionVersion = -1;        // interaction-store version the cache was built at
    }

    public readonly struct QueryBuilder<T1> where T1 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1>(_world, _include, ex, _interaction);
        }

        /// <summary>Restrict to entities that play <paramref name="role"/> in an interaction of <paramref name="type"/>.</summary>
        public QueryBuilder<T1> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1> AsInteractor() => new QueryBuilder<T1>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1> AsInteractee() => new QueryBuilder<T1>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2> where T1 : struct where T2 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2> AsInteractor() => new QueryBuilder<T1, T2>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2> AsInteractee() => new QueryBuilder<T1, T2>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3>
        where T1 : struct where T2 : struct where T3 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3> AsInteractor() => new QueryBuilder<T1, T2, T3>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3> AsInteractee() => new QueryBuilder<T1, T2, T3>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }
}
