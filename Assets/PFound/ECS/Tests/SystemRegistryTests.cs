using System;
using System.Collections.Generic;
using System.IO;
using PFound.ECS;

// Public, externally-instantiable systems the generator is allowed to emit `new T()` for.
// They double as the fixtures the committed SystemRegistry.g.cs registers.
namespace PFound.ECS.Tests.Registry
{
    [ECSSystem(ECSPhase.PreUpdate)]
    public sealed class ExampleInputSystem : SystemBase
    {
        public static int Ran;
        protected override void Execute() => Ran++;
    }

    [ECSSystem(ECSPhase.OnUpdate)]
    public sealed class ExampleMovementSystem : SystemBase
    {
        public static int Ran;
        protected override void Execute() => Ran++;
    }
}

// System registry generator: the discovery predicate, the source emitter, and the committed
// generated registry. Reflection (SystemDiscovery.FromLoadedAssemblies) is the parity oracle.
internal static class SystemRegistryTests
{
    public static void Run()
    {
        IsSystemTypePredicate();
        SelectSystemsFiltersAndOrders();
        EmitterShape();
        EmitterDeterministicAndEmpty();
        GeneratedRegistryRegistersSystems();
        GeneratedRegistryMatchesReflectionOracle();
        CommittedFileMatchesEmitter();
    }

    // A non-visible system: the generator must NOT pick it (can't `new` it cross-assembly).
    [ECSSystem] private sealed class HiddenSystem : SystemBase { protected override void Execute() { } }

    private static void IsSystemTypePredicate()
    {
        TestKit.Check(SystemDiscovery.IsSystemType(typeof(PFound.ECS.Tests.Registry.ExampleInputSystem)),
            "IsSystemType: concrete, public, parameterless system qualifies");
        TestKit.Check(!SystemDiscovery.IsSystemType(typeof(SystemBase)),
            "IsSystemType: abstract SystemBase rejected");
        TestKit.Check(!SystemDiscovery.IsSystemType(typeof(string)),
            "IsSystemType: non-system rejected");
        TestKit.Check(!SystemDiscovery.IsSystemType(typeof(HiddenSystem)),
            "IsSystemType: non-visible (private nested) system rejected");
    }

    private static void SelectSystemsFiltersAndOrders()
    {
        var input = new[]
        {
            typeof(PFound.ECS.Tests.Registry.ExampleMovementSystem), // out of order
            typeof(string),                                          // filtered out
            typeof(SystemBase),                                      // filtered out (abstract)
            typeof(PFound.ECS.Tests.Registry.ExampleInputSystem),
        };
        var selected = SystemDiscovery.SelectSystems(input);
        TestKit.Check(selected.Count == 2, "SelectSystems keeps only registrable systems");
        TestKit.Check(selected[0] == typeof(PFound.ECS.Tests.Registry.ExampleInputSystem) &&
                      selected[1] == typeof(PFound.ECS.Tests.Registry.ExampleMovementSystem),
            "SelectSystems orders deterministically by full name");
    }

    private static void EmitterShape()
    {
        var systems = new List<Type>
        {
            typeof(PFound.ECS.Tests.Registry.ExampleInputSystem),
            typeof(PFound.ECS.Tests.Registry.ExampleMovementSystem),
        };
        var src = SystemRegistryEmitter.Emit("PFound.ECS.Generated", "SystemRegistry", systems);

        TestKit.Check(src.Contains("namespace PFound.ECS.Generated"), "Emit: namespace present");
        TestKit.Check(src.Contains("public static class SystemRegistry"), "Emit: class present");
        TestKit.Check(src.Contains("public static void Register(World world)"), "Emit: Register method present");
        TestKit.Check(src.Contains("new PFound.ECS.Tests.Registry.ExampleInputSystem()") &&
                      src.Contains("new PFound.ECS.Tests.Registry.ExampleMovementSystem()"),
            "Emit: a `new T()` registration per system");
        TestKit.Check(src.IndexOf("ExampleInputSystem()", StringComparison.Ordinal) <
                      src.IndexOf("ExampleMovementSystem()", StringComparison.Ordinal),
            "Emit: registrations preserve input order");
    }

    private static void EmitterDeterministicAndEmpty()
    {
        var systems = new List<Type> { typeof(PFound.ECS.Tests.Registry.ExampleInputSystem) };
        var a = SystemRegistryEmitter.Emit("N", "R", systems);
        var b = SystemRegistryEmitter.Emit("N", "R", systems);
        TestKit.Check(a == b, "Emit: deterministic (same input ⇒ identical output)");

        var empty = SystemRegistryEmitter.Emit("N", "R", new List<Type>());
        TestKit.Check(empty.Contains("System.Array.Empty<System.Type>()") && !empty.Contains("new "),
            "Emit: empty system list yields an empty registry");
    }

    private static void GeneratedRegistryRegistersSystems()
    {
        PFound.ECS.Tests.Registry.ExampleInputSystem.Ran = 0;
        PFound.ECS.Tests.Registry.ExampleMovementSystem.Ran = 0;

        var w = new World();
        w.DeltaTime = 0.016f; // scaled systems only run while time advances
        PFound.ECS.Generated.SystemRegistry.Register(w);
        w.Update();

        TestKit.Check(PFound.ECS.Tests.Registry.ExampleInputSystem.Ran == 1 &&
                      PFound.ECS.Tests.Registry.ExampleMovementSystem.Ran == 1,
            "Generated registry registers and runs every system (no reflection)");
    }

    private static void GeneratedRegistryMatchesReflectionOracle()
    {
        var generated = new HashSet<Type>(PFound.ECS.Generated.SystemRegistry.SystemTypes);
        var oracle = new HashSet<Type>(SystemDiscovery.FromLoadedAssemblies());
        TestKit.Check(generated.SetEquals(oracle),
            "Generated SystemTypes match the reflection oracle (catches a stale registry)");
    }

    private static void CommittedFileMatchesEmitter()
    {
        const string path = "Assets/PFound/ECS/Tests/SystemRegistry.g.cs";
        if (!File.Exists(path))
        {
            TestKit.Check(false, "Committed registry file present at " + path);
            return;
        }
        var expected = SystemRegistryEmitter.Emit(
            "PFound.ECS.Generated", "SystemRegistry", SystemDiscovery.FromLoadedAssemblies());
        var actual = File.ReadAllText(path).Replace("\r\n", "\n");
        TestKit.Check(actual == expected,
            "Committed SystemRegistry.g.cs equals fresh emitter output (regenerate if this fails)");
    }
}
