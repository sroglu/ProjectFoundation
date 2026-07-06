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
        RunPhaseRunsOnlyThatPhase();
        ReflectionDiscovery();
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

    // ---- tests ----

    private static void LifecycleCalledOnce()
    {
        var w = new World();
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
        w.RegisterSystem(new Pre());
        w.RegisterSystem(new On());
        w.RunPhase(ECSPhase.PreUpdate);
        TestKit.Check(Log.Count == 1 && Log[0] == "pre", "RunPhase executes only systems of that phase");
    }

    private static void ReflectionDiscovery()
    {
        Log.Clear();
        var w = new World();
        // The generated/reflection registry hands the scheduler a list of system types to
        // instantiate. Pass an explicit type list so the test is deterministic.
        w.DiscoverSystems(new[] { typeof(Discovered) });
        w.Update();
        TestKit.Check(Log.Contains("discovered"), "DiscoverSystems instantiates and registers systems by type");
    }
}
