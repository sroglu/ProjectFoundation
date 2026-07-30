using System;
using PFound.ECS;

// Characterization tests for PFound.ECS core, ported as behavior assertions from the
// source ECS test intent. Standalone runner (no NUnit dependency) so it compiles + runs
// under mono/csc without Unity. Exit code 0 = all pass.
internal static class CoreCharacterizationTests
{
    private struct Position { public float X, Y, Z; }
    private struct Velocity { public float X, Y, Z; }
    private struct Health { public int Value; }
    private struct Tag { }

    private static void Check(bool cond, string name) => TestKit.Check(cond, name);
    private static void Throws<TEx>(Action a, string name) where TEx : Exception => TestKit.Throws<TEx>(a, name);

    public static void Run()
    {
        EntityLifecycle();
        ComponentOps();
        DestroyAndRecycle();
        Queries();
        ForEachIteration();
    }

    private static void EntityLifecycle()
    {
        var w = new World();
        var e = w.Create();
        Check(e.IsValid, "created entity is valid handle");
        Check(w.IsAlive(e), "created entity is alive");
        Check(w.IsValid(e), "created entity passes IsValid");
        Check(!w.IsAlive(Entity.Invalid), "Entity.Invalid is not alive");

        var e2 = w.Create();
        Check(e.Id != e2.Id, "distinct entities get distinct ids");
    }

    private static void ComponentOps()
    {
        var w = new World();
        var e = w.Create();

        Check(!w.Has<Position>(e), "no component before add");
        w.Add(e, new Position { X = 1, Y = 2, Z = 3 });
        Check(w.Has<Position>(e), "has component after add");
        Check(w.Get<Position>(e).Y == 2, "get returns stored value");

        // ref mutation persists
        w.Get<Position>(e).X = 42;
        Check(w.Get<Position>(e).X == 42, "ref get mutates in place");

        // TryGet
        Check(w.TryGet<Position>(e, out var p) && p.X == 42, "TryGet returns value when present");
        Check(!w.TryGet<Velocity>(e, out _), "TryGet false when absent");

        // Get on a live entity that lacks the component fails fast with a clear error.
        Throws<InvalidOperationException>(() => { var _ = w.Get<Velocity>(e); }, "Get throws when component absent");

        // AddIfMissing
        Check(w.AddIfMissing(e, new Health { Value = 10 }), "AddIfMissing adds when missing");
        Check(!w.AddIfMissing(e, new Health { Value = 99 }), "AddIfMissing no-op when present");
        Check(w.Get<Health>(e).Value == 10, "AddIfMissing did not overwrite");

        // SetOrAdd overwrites
        w.SetOrAdd(e, new Health { Value = 77 });
        Check(w.Get<Health>(e).Value == 77, "SetOrAdd overwrites existing");

        // Remove
        w.Remove<Position>(e);
        Check(!w.Has<Position>(e), "remove clears component");
        Check(w.Has<Health>(e), "remove leaves other components intact");
        Check(!w.RemoveIfExists<Position>(e), "RemoveIfExists false when absent");
        Check(w.RemoveIfExists<Health>(e), "RemoveIfExists true when present");
    }

    private static void DestroyAndRecycle()
    {
        var w = new World();
        var e = w.Create();
        w.Add(e, new Position { X = 5 });
        w.Destroy(e);

        Check(!w.IsAlive(e), "destroyed entity is not alive");
        Check(!w.IsValid(e), "destroyed entity handle is stale (version bumped)");
        Throws<InvalidOperationException>(() => { var _ = w.Get<Position>(e); }, "Get on destroyed throws");
        Throws<InvalidOperationException>(() => w.Add(e, new Velocity()), "Add on destroyed throws");

        // Id recycling: a new entity reuses the freed id but with a new version.
        var reused = w.Create();
        Check(reused.Id == e.Id, "id is recycled");
        Check(reused.Version != e.Version, "recycled id has new version");
        Check(w.IsAlive(reused) && !w.IsAlive(e), "stale handle stays dead after recycle");
        Check(!w.Has<Position>(reused), "recycled entity starts with no components");

        // DestroyAll
        w.Create(); w.Create();
        w.DestroyAll();
        Check(!w.IsAlive(reused), "DestroyAll kills all live entities");
    }

    private static void Queries()
    {
        var w = new World();
        var a = w.Create(); w.Add(a, new Position()); w.Add(a, new Velocity());
        var b = w.Create(); w.Add(b, new Position());
        var c = w.Create(); w.Add(c, new Position()); w.Add(c, new Velocity()); w.Add(c, new Tag());

        var movers = w.Query<Position, Velocity>().Build();
        Check(w.Entities(movers).Count == 2, "Query<Pos,Vel> matches both with-both entities");

        var moversNoTag = w.Query<Position, Velocity>().Without<Tag>().Build();
        Check(w.Entities(moversNoTag).Count == 1, "Without<Tag> excludes tagged entity");

        var positions = w.Query<Position>().Build();
        Check(w.Entities(positions).Count == 3, "Query<Position> matches all three");

        // Cache invalidates on structural change.
        w.Add(b, new Velocity());
        Check(w.Entities(movers).Count == 3, "query cache updates after adding a component");

        w.Destroy(c);
        Check(w.Entities(positions).Count == 2, "query cache updates after destroy");

        // Repeated Build of same signature dedupes to same QueryId.
        var movers2 = w.Query<Position, Velocity>().Build();
        Check(movers2.Index == movers.Index, "identical query signatures dedupe");
    }

    private static void ForEachIteration()
    {
        var w = new World();
        for (int i = 0; i < 5; i++)
        {
            var e = w.Create();
            w.Add(e, new Position());
            w.Add(e, new Velocity { X = 2 });
        }

        w.DeltaTime = 0.5f;
        w.Query<Position, Velocity>().ForEach((World world, Entity e, ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.X * world.DeltaTime;
        });

        int correct = 0;
        foreach (var e in w.Entities(w.Query<Position>().Build()))
        {
            if (Math.Abs(w.Get<Position>(e).X - 1.0f) < 0.0001f) correct++;
        }
        Check(correct == 5, "ForEach mutated all matched components via ref");
    }
}
