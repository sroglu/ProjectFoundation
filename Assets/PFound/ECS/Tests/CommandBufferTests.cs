using PFound.ECS;

// CommandBuffer: deferred structural changes recorded during iteration and applied later,
// avoiding mid-ForEach structural-change bugs. Placeholder entities resolve on Playback.
internal static class CommandBufferTests
{
    private struct Position { public float X; }
    private struct Velocity { public float X; }
    private struct Dead { }

    public static void Run()
    {
        DeferredAddAndRemove();
        DeferredDestroy();
        DeferredCreateWithComponents();
        SafeToRecordDuringForEach();
        ReusableAfterPlayback();
    }

    private static void DeferredAddAndRemove()
    {
        var w = new World();
        var e = w.Create();
        w.Add(e, new Position { X = 1 });

        var cb = w.CreateCommandBuffer();
        cb.Add(e, new Velocity { X = 9 });
        cb.Remove<Position>(e);

        // Nothing applied until playback.
        TestKit.Check(!w.Has<Velocity>(e), "deferred Add not applied before Playback");
        TestKit.Check(w.Has<Position>(e), "deferred Remove not applied before Playback");

        cb.Playback();
        TestKit.Check(w.Has<Velocity>(e) && w.Get<Velocity>(e).X == 9, "deferred Add applied on Playback");
        TestKit.Check(!w.Has<Position>(e), "deferred Remove applied on Playback");
    }

    private static void DeferredDestroy()
    {
        var w = new World();
        var e = w.Create();
        var cb = w.CreateCommandBuffer();
        cb.Destroy(e);
        TestKit.Check(w.IsAlive(e), "deferred Destroy not applied before Playback");
        cb.Playback();
        TestKit.Check(!w.IsAlive(e), "deferred Destroy applied on Playback");
    }

    private static void DeferredCreateWithComponents()
    {
        var w = new World();
        var cb = w.CreateCommandBuffer();
        var placeholder = cb.Create();
        cb.Add(placeholder, new Position { X = 5 });
        cb.Add(placeholder, new Velocity { X = 6 });

        TestKit.Check(!w.IsAlive(placeholder), "placeholder is not alive before Playback");

        cb.Playback();

        int created = 0;
        Entity real = Entity.Invalid;
        foreach (var ent in w.Entities(w.Query<Position, Velocity>().Build())) { created++; real = ent; }
        TestKit.Check(created == 1, "Playback creates one real entity from placeholder");
        TestKit.Check(w.IsAlive(real) && w.Get<Position>(real).X == 5 && w.Get<Velocity>(real).X == 6,
            "components added to placeholder land on the resolved real entity");
    }

    private static void SafeToRecordDuringForEach()
    {
        var w = new World();
        for (int i = 0; i < 3; i++) { var e = w.Create(); w.Add(e, new Position()); }

        var cb = w.CreateCommandBuffer();
        w.Query<Position>().ForEach((World world, Entity e, ref Position p) => cb.Add(e, new Dead()));
        cb.Playback();

        TestKit.Check(w.Entities(w.Query<Dead>().Build()).Count == 3,
            "structural changes recorded during ForEach apply cleanly after Playback");
    }

    private static void ReusableAfterPlayback()
    {
        var w = new World();
        var e = w.Create();
        var cb = w.CreateCommandBuffer();
        cb.Add(e, new Position { X = 1 });
        cb.Playback();
        // Second round on the same buffer must not replay the first round's commands.
        cb.Add(e, new Velocity { X = 2 });
        cb.Playback();
        TestKit.Check(w.Get<Position>(e).X == 1 && w.Get<Velocity>(e).X == 2,
            "buffer empties after Playback and is reusable without replaying old commands");
    }
}
