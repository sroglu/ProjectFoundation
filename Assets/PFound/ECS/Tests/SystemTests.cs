using System.Collections.Generic;
using PFound.ECS;

// Systems: SystemBase lifecycle + the registry/scheduler that orders systems by phase, then
// by [DependsOn] within a phase, filters by [ActiveInScene], and ticks them on World.Update().
internal static class SystemTests
{
    private static readonly List<string> Log = new List<string>();

    public static void Run()
    {
        LifecycleCalledOnce();
        PhaseOrdering();
        DependsOnOrdering();
        ActiveInSceneFiltering();
        ScaledVsUnscaledDelta();
        ScaledSystemsPauseAtZeroDelta();
        RunPhaseRunsOnlyThatPhase();
        ReflectionDiscovery();
        GetSystemAndTryGetSystem();
        DependencyCycleThrows();
        PerSystemCommandBufferAutoPlayback();
        LifecycleManagedSubscriptionAutoUnsubscribes();
        OnDisposeCalledOnWorldDispose();
        ReloadSceneClearsWorldAndReinits();
    }

    // ---- system fixtures ----

    private sealed class Basic : SystemBase
    {
        public int Exec, Init, Queries;
        protected override void OnInitialize() => Init++;
        protected override void CreateQueries() => Queries++;
        protected override void Execute() => Exec++;
    }

    private abstract class Logger : SystemBase
    {
        protected void Note(string s) => Log.Add(s);
    }

    [ECSSystem(ECSPhase.PreUpdate)]   private sealed class Pre : Logger  { protected override void Execute() => Note("pre"); }
    [ECSSystem(ECSPhase.OnUpdate)]    private sealed class On : Logger   { protected override void Execute() => Note("on"); }
    [ECSSystem(ECSPhase.PostUpdate)]  private sealed class Post : Logger { protected override void Execute() => Note("post"); }

    [ECSSystem(ECSPhase.OnUpdate)] private sealed class Movement : Logger { protected override void Execute() => Note("movement"); }
    [ECSSystem(ECSPhase.OnUpdate)]
    [DependsOn(typeof(Movement))]
    private sealed class Collision : Logger { protected override void Execute() => Note("collision"); }

    [ECSSystem(ECSPhase.OnUpdate)]
    [ActiveInScene("Battle")]
    private sealed class BattleOnly : Logger { protected override void Execute() => Note("battle"); }

    private sealed class Always : Logger { protected override void Execute() => Note("always"); }

    [ECSSystem(ECSPhase.OnUpdate, UpdateTime.Unscaled)]
    private sealed class UnscaledReader : SystemBase
    {
        public float Seen;
        protected override void Execute() => Seen = DeltaTime;
    }
    [ECSSystem(ECSPhase.OnUpdate)]
    private sealed class ScaledReader : SystemBase
    {
        public float Seen;
        protected override void Execute() => Seen = DeltaTime;
    }

    [ECSSystem(ECSPhase.OnUpdate)]
    private sealed class Discovered : Logger { protected override void Execute() => Note("discovered"); }

    [ECSSystem(ECSPhase.OnUpdate)] private sealed class CountScaled : SystemBase { public int N; protected override void Execute() => N++; }
    [ECSSystem(ECSPhase.OnUpdate, UpdateTime.Unscaled)] private sealed class CountUnscaled : SystemBase { public int N; protected override void Execute() => N++; }

    // Mutual [DependsOn] within a phase ⇒ a dependency cycle.
    [ECSSystem(ECSPhase.OnUpdate)] [DependsOn(typeof(CycleB))] private sealed class CycleA : SystemBase { protected override void Execute() { } }
    [ECSSystem(ECSPhase.OnUpdate)] [DependsOn(typeof(CycleA))] private sealed class CycleB : SystemBase { protected override void Execute() { } }

    private struct Spawned { public int V; }
    [ECSSystem(ECSPhase.OnUpdate)]
    private sealed class Spawner : SystemBase
    {
        public int Runs;
        protected override void Execute()
        {
            if (Runs++ != 0) return;
            var e = CommandBuffer.Create();
            CommandBuffer.Add(e, new Spawned { V = 42 });
        }
    }

    private struct Ping { public int N; }
    private sealed class Listener : SystemBase
    {
        public int Sum;
        protected override void OnInitialize() => Subscribe<Ping>(p => Sum += p.N);
        protected override void Execute() { }
    }

    private sealed class DisposeTracker : SystemBase
    {
        public int Disposed;
        protected override void OnDispose() => Disposed++;
        protected override void Execute() { }
    }

    private sealed class ReloadTracker : SystemBase
    {
        public int Inits, Disposes;
        protected override void OnInitialize() => Inits++;
        protected override void OnDispose() => Disposes++;
        protected override void Execute() { }
    }

    // ---- tests ----

    private static void LifecycleCalledOnce()
    {
        var w = new World();
        w.DeltaTime = 0.016f; // scaled systems only run while time advances (pause gate)
        var s = new Basic();
        w.RegisterSystem(s);
        TestKit.Check(s.Init == 1, "OnInitialize called once on register");
        TestKit.Check(s.Queries == 1, "CreateQueries called once on register");
        TestKit.Check(s.Exec == 0, "Execute not called before Update");
        w.Update();
        w.Update();
        TestKit.Check(s.Exec == 2, "Execute called once per Update");
        TestKit.Check(s.Init == 1 && s.Queries == 1, "lifecycle hooks not re-run on Update");
    }

    private static void PhaseOrdering()
    {
        Log.Clear();
        var w = new World();
        w.DeltaTime = 0.016f;
        // Register out of phase order; scheduler must still run Pre -> On -> Post.
        w.RegisterSystem(new Post());
        w.RegisterSystem(new Pre());
        w.RegisterSystem(new On());
        w.Update();
        TestKit.Check(Log.Count == 3 && Log[0] == "pre" && Log[1] == "on" && Log[2] == "post",
            "systems run in phase order regardless of registration order");
    }

    private static void DependsOnOrdering()
    {
        Log.Clear();
        var w = new World();
        w.DeltaTime = 0.016f;
        // Register dependent first; [DependsOn] must still run Movement before Collision.
        w.RegisterSystem(new Collision());
        w.RegisterSystem(new Movement());
        w.Update();
        TestKit.Check(Log.IndexOf("movement") < Log.IndexOf("collision"),
            "[DependsOn] runs the dependency before the dependent within a phase");
    }

    private static void ActiveInSceneFiltering()
    {
        Log.Clear();
        var w = new World();
        w.DeltaTime = 0.016f;
        w.RegisterSystem(new BattleOnly());
        w.RegisterSystem(new Always());

        w.Update();
        TestKit.Check(Log.Contains("always") && !Log.Contains("battle"),
            "[ActiveInScene] system inactive until its scene is active; unscoped system always runs");

        Log.Clear();
        w.SceneChanged("Battle");
        w.Update();
        TestKit.Check(Log.Contains("battle"), "[ActiveInScene] system runs once its scene becomes active");

        Log.Clear();
        w.SceneChanged("Menu");
        w.Update();
        TestKit.Check(!Log.Contains("battle"), "[ActiveInScene] system stops when scene changes away");
    }

    private static void ScaledVsUnscaledDelta()
    {
        var w = new World();
        var u = new UnscaledReader();
        var s = new ScaledReader();
        w.RegisterSystem(u);
        w.RegisterSystem(s);
        w.DeltaTime = 0.1f;
        w.UnscaledDeltaTime = 0.9f;
        w.Update();
        TestKit.Check(System.Math.Abs(s.Seen - 0.1f) < 1e-6f, "Scaled system reads DeltaTime");
        TestKit.Check(System.Math.Abs(u.Seen - 0.9f) < 1e-6f, "Unscaled system reads UnscaledDeltaTime");
    }

    private static void RunPhaseRunsOnlyThatPhase()
    {
        Log.Clear();
        var w = new World();
        w.DeltaTime = 0.016f;
        w.RegisterSystem(new Pre());
        w.RegisterSystem(new On());
        w.RunPhase(ECSPhase.PreUpdate);
        TestKit.Check(Log.Count == 1 && Log[0] == "pre", "RunPhase executes only systems of that phase");
    }

    private static void ReflectionDiscovery()
    {
        Log.Clear();
        var w = new World();
        w.DeltaTime = 0.016f;
        // The generated/reflection registry hands the scheduler a list of system types to
        // instantiate. Pass an explicit type list so the test is deterministic.
        w.DiscoverSystems(new[] { typeof(Discovered) });
        w.Update();
        TestKit.Check(Log.Contains("discovered"), "DiscoverSystems instantiates and registers systems by type");
    }

    private static void ScaledSystemsPauseAtZeroDelta()
    {
        var w = new World();
        var cs = new CountScaled();
        var cu = new CountUnscaled();
        w.RegisterSystem(cs);
        w.RegisterSystem(cu);

        w.DeltaTime = 0f; w.UnscaledDeltaTime = 0.016f;
        w.Update();
        TestKit.Check(cs.N == 0, "Scaled system is paused when DeltaTime is 0");
        TestKit.Check(cu.N == 1, "Unscaled system runs even when DeltaTime is 0");

        w.DeltaTime = 0.016f;
        w.Update();
        TestKit.Check(cs.N == 1, "Scaled system resumes when time advances");
    }

    private static void GetSystemAndTryGetSystem()
    {
        var w = new World();
        var s = new CountScaled();
        w.RegisterSystem(s);

        TestKit.Check(ReferenceEquals(w.GetSystem<CountScaled>(), s), "GetSystem returns the registered instance");
        TestKit.Check(w.TryGetSystem<CountScaled>(out var got) && ReferenceEquals(got, s), "TryGetSystem returns the registered instance");
        TestKit.Check(!w.TryGetSystem<CountUnscaled>(out _), "TryGetSystem is false for an unregistered type");
        TestKit.Throws<System.InvalidOperationException>(() => w.GetSystem<CountUnscaled>(), "GetSystem throws for an unregistered type");
    }

    private static void DependencyCycleThrows()
    {
        var w = new World();
        w.DeltaTime = 0.016f;
        w.RegisterSystem(new CycleA());
        w.RegisterSystem(new CycleB());
        TestKit.Throws<System.InvalidOperationException>(() => w.Update(),
            "dependency cycle is reported as an error, not silently broken");
    }

    private static void PerSystemCommandBufferAutoPlayback()
    {
        var w = new World();
        w.DeltaTime = 0.016f;
        w.RegisterSystem(new Spawner());
        w.Update(); // Spawner records a deferred create+add; the world auto-plays it back

        int count = 0, value = 0;
        w.Query<Spawned>().ForEach((World _, Entity __, ref Spawned s) => { count++; value = s.V; });
        TestKit.Check(count == 1 && value == 42, "per-system command buffer is auto-played back after Execute");
    }

    private static void LifecycleManagedSubscriptionAutoUnsubscribes()
    {
        var w = new World();
        var l = new Listener();
        w.RegisterSystem(l);

        w.Events.Publish(new Ping { N = 5 });
        w.Events.Dispatch();
        TestKit.Check(l.Sum == 5, "SystemBase.Subscribe receives events");

        l.Dispose(); // teardown drops the managed subscription
        w.Events.Publish(new Ping { N = 7 });
        w.Events.Dispatch();
        TestKit.Check(l.Sum == 5, "disposed system's subscription no longer fires (auto-unsubscribed)");
    }

    private static void OnDisposeCalledOnWorldDispose()
    {
        var w = new World();
        var d = new DisposeTracker();
        w.RegisterSystem(d);
        w.Dispose();
        TestKit.Check(d.Disposed == 1, "OnDispose runs for each system on World.Dispose");
    }

    private static void ReloadSceneClearsWorldAndReinits()
    {
        var w = new World();
        w.DeltaTime = 0.016f;
        var e = w.Create();
        w.Add(e, new Spawned { V = 1 });

        var rt = new ReloadTracker();
        w.RegisterSystem(rt);
        TestKit.Check(rt.Inits == 1, "system initialized on register");

        w.ReloadScene("Level2");
        TestKit.Check(!w.IsAlive(e), "ReloadScene clears world entities");
        TestKit.Check(rt.Disposes == 1 && rt.Inits == 2, "ReloadScene disposes then re-initializes systems");
        TestKit.Check(w.CurrentScene == "Level2", "ReloadScene switches the active scene");
    }
}
