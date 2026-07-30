using System;

namespace PFound.ECS
{
    // High-arity (4..10) query delegates and builders. Same shape as the 1..3 forms
    // in Query.cs; split out to keep that file focused on the core types.
    public delegate void RefAction<T1, T2, T3, T4>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4);
    public delegate void RefAction<T1, T2, T3, T4, T5>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5);
    public delegate void RefAction<T1, T2, T3, T4, T5, T6>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5, ref T6 c6);
    public delegate void RefAction<T1, T2, T3, T4, T5, T6, T7>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5, ref T6 c6, ref T7 c7);
    public delegate void RefAction<T1, T2, T3, T4, T5, T6, T7, T8>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5, ref T6 c6, ref T7 c7, ref T8 c8);
    public delegate void RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5, ref T6 c6, ref T7 c7, ref T8 c8, ref T9 c9);
    public delegate void RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(World world, Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4, ref T5 c5, ref T6 c6, ref T7 c7, ref T8 c8, ref T9 c9, ref T10 c10);

    public readonly struct QueryBuilder<T1, T2, T3, T4> where T1 : struct where T2 : struct where T3 : struct where T4 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4> AsInteractor() => new QueryBuilder<T1, T2, T3, T4>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4> AsInteractee() => new QueryBuilder<T1, T2, T3, T4>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5, T6> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5, T6>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5, T6> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5, T6>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5, T6> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5, T6>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5, T6> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5, T6, T7> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5, T6, T7> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5, T6, T7, T8> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

    public readonly struct QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct where T10 : struct
    {
        private readonly World _world;
        private readonly ComponentMask _include;
        private readonly ComponentMask _exclude;
        private readonly InteractionFilter _interaction;

        internal QueryBuilder(World world, in ComponentMask include, in ComponentMask exclude, in InteractionFilter interaction)
        {
            _world = world; _include = include; _exclude = exclude; _interaction = interaction;
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> Without<TX>() where TX : struct
        {
            var ex = _exclude; ex.Set(ComponentType<TX>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_world, _include, ex, _interaction);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> WithInteraction(int type, InteractionRole role = InteractionRole.Interactor)
            => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_world, _include, _exclude, new InteractionFilter(type, role));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> AsInteractor() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactor));
        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> AsInteractee() => new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_world, _include, _exclude, new InteractionFilter(_interaction.Type, InteractionRole.Interactee));

        public QueryId Build() => _world.RegisterQuery(_include, _exclude, _interaction);

        public void ForEach(RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action) => _world.ForEach(_include, _exclude, _interaction, action);
    }

}
