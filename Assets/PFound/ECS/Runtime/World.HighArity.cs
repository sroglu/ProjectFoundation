namespace PFound.ECS
{
    // High-arity (4..10) Query entry points and ForEach iterators on World,
    // extending the 1..3 forms in World.cs. Same include-mask + MatchInto pattern.
    public sealed partial class World
    {
        public QueryBuilder<T1, T2, T3, T4> Query<T1, T2, T3, T4>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id);
            return new QueryBuilder<T1, T2, T3, T4>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5> Query<T1, T2, T3, T4, T5>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6> Query<T1, T2, T3, T4, T5, T6>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id); inc.Set(ComponentType<T6>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7> Query<T1, T2, T3, T4, T5, T6, T7>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id); inc.Set(ComponentType<T6>.Id); inc.Set(ComponentType<T7>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Query<T1, T2, T3, T4, T5, T6, T7, T8>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id); inc.Set(ComponentType<T6>.Id); inc.Set(ComponentType<T7>.Id); inc.Set(ComponentType<T8>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9> Query<T1, T2, T3, T4, T5, T6, T7, T8, T9>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id); inc.Set(ComponentType<T6>.Id); inc.Set(ComponentType<T7>.Id); inc.Set(ComponentType<T8>.Id); inc.Set(ComponentType<T9>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this, inc, default, default);
        }

        public QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> Query<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>() where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct where T10 : struct
        {
            var inc = default(ComponentMask);
            inc.Set(ComponentType<T1>.Id); inc.Set(ComponentType<T2>.Id); inc.Set(ComponentType<T3>.Id); inc.Set(ComponentType<T4>.Id); inc.Set(ComponentType<T5>.Id); inc.Set(ComponentType<T6>.Id); inc.Set(ComponentType<T7>.Id); inc.Set(ComponentType<T8>.Id); inc.Set(ComponentType<T9>.Id); inc.Set(ComponentType<T10>.Id);
            return new QueryBuilder<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this, inc, default, default);
        }

        internal void ForEach<T1, T2, T3, T4>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5, T6>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5, T6> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>(); var p6 = Pool<T6>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id), ref p6.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5, T6, T7>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5, T6, T7> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>(); var p6 = Pool<T6>(); var p7 = Pool<T7>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id), ref p6.Get(e.Id), ref p7.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5, T6, T7, T8>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5, T6, T7, T8> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>(); var p6 = Pool<T6>(); var p7 = Pool<T7>(); var p8 = Pool<T8>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id), ref p6.Get(e.Id), ref p7.Get(e.Id), ref p8.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>(); var p6 = Pool<T6>(); var p7 = Pool<T7>(); var p8 = Pool<T8>(); var p9 = Pool<T9>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id), ref p6.Get(e.Id), ref p7.Get(e.Id), ref p8.Get(e.Id), ref p9.Get(e.Id));
            }
        }

        internal void ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in ComponentMask inc, in ComponentMask exc, in InteractionFilter filter, RefAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action) where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct where T8 : struct where T9 : struct where T10 : struct
        {
            var p1 = Pool<T1>(); var p2 = Pool<T2>(); var p3 = Pool<T3>(); var p4 = Pool<T4>(); var p5 = Pool<T5>(); var p6 = Pool<T6>(); var p7 = Pool<T7>(); var p8 = Pool<T8>(); var p9 = Pool<T9>(); var p10 = Pool<T10>();
            int c = MatchInto(inc, exc, filter, ref _scratch);
            for (int i = 0; i < c; i++)
            {
                var e = _scratch[i];
                action(this, e, ref p1.Get(e.Id), ref p2.Get(e.Id), ref p3.Get(e.Id), ref p4.Get(e.Id), ref p5.Get(e.Id), ref p6.Get(e.Id), ref p7.Get(e.Id), ref p8.Get(e.Id), ref p9.Get(e.Id), ref p10.Get(e.Id));
            }
        }

    }
}
