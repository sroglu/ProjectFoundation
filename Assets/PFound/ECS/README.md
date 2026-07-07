# PFound.ECS

A pure-C# sparse-set ECS runtime: a `World` of entities and struct components, ref-returning
queries, a deferred `CommandBuffer`, deferred events, transient interactions, and an
attribute-driven, phase-ordered system scheduler. The core has **no** Unity, Burst, Collections or
Mathematics dependency — you `new World()` and drive it from any update loop.

## Model

- **World** owns entities, component pools (sparse-set per type, O(1) add/get/remove), the query
  cache, events and interactions. Main-thread only; no internal locking.
- **Entity** is an `Id`/`Version` pair; a destroyed id is recycled with a bumped version so stale
  handles fail `IsValid`/`IsAlive`.
- **Components** are plain `struct`s, auto-registered on first use (max 256 component types).
- **Systems** are `SystemBase` subclasses ordered by frame **phase** then intra-phase dependency.

## Public API

**World lifecycle** — `new World()`; `Dispose()`. Per-frame clocks are public fields set by the host
before ticking: `DeltaTime`, `UnscaledDeltaTime`; also `CurrentScene` (via `SceneChanged(name)`).

**Entities** — `Entity Create()`, `Destroy(Entity)`, `DestroyAll()`, `bool IsValid(Entity)`,
`bool IsAlive(Entity)`.

**Components** — `Add<T>(Entity, in T)`, `AddIfMissing<T>(...)`, `SetOrAdd<T>(...)`,
`ref T Get<T>(Entity)`, `bool TryGet<T>(Entity, out T)`, `bool Has<T>(Entity)`, `Remove<T>(Entity)`,
`bool RemoveIfExists<T>(Entity)`.

**Queries** — `Query<T1..T10>()` returns a `QueryBuilder`. Refine with `.Without<TX>()`, then either:
- `ForEach(RefAction<...>)` — zero-alloc iteration over the dense arrays:
  ```csharp
  world.Query<Position, Velocity>().ForEach(
      (World w, Entity e, ref Position p, ref Velocity v) => p.X += v.X * w.DeltaTime);
  ```
- `QueryId Build()` (cache it in `CreateQueries`) → `QueryResult Entities(QueryId)` — a managed,
  zero-alloc view (`.Count`, indexer, enumerator) valid until the next structural change.

**CommandBuffer** — `CreateCommandBuffer()` records structural changes safely during iteration:
`Create()`, `Destroy(e)`, `Add<T>(e, in T)`, `Remove<T>(e)`, `Count`; apply with `Playback()`.

**Events (`World.Events`)** — `IDisposable Subscribe<T>(Action<T>)`, `Publish<T>(in T)` (queues),
`Dispatch()` (delivers in publish order, re-entrancy safe), `PendingCount`.

**Interactions (`World.Interactions`)** — `Start(int type, Entity interactor, Entity interactee)`,
`InteractionView Get(int type)`, `OfInteractor(int type, Entity)`,
`ForEachInteractee(int type, Entity, Action<Entity>)`, `Clear()` / `Clear(int type)` (typically reset
each frame).

**Systems** — subclass `SystemBase`, override `Execute()` (required) and optionally `OnInitialize()`
/ `CreateQueries()`. Class-level attributes: `[ECSSystem(ECSPhase phase = OnUpdate, UpdateTime time
= Scaled)]`, `[DependsOn(typeof(OtherSystem), ...)]` (orders within a phase, topo-sorted),
`[ActiveInScene("Level1", ...)]` (scene-scoped). Phases run in declaration order: `PreUpdate`,
`OnUpdate`, `PostUpdate`, `PreRender`, `OnRender`, `PostRender`, `PostFrame`. `SystemBase.DeltaTime`
resolves to the scaled or unscaled clock per the attribute.

**System registration & tick** — `RegisterSystem(SystemBase)` for one instance, or
`DiscoverSystems(IEnumerable<Type>)` to reflect-and-register many. `Update()` runs every active
system in phase then dependency order; `RunPhase(ECSPhase)` runs a single phase.

## Setup / wiring

The runtime is a pure library — but it has no update loop of its own, so **something must own a
`World` and pump it**. The canonical host is a thin MonoBehaviour (see `Samples/SampleBootstrap.cs`):

```csharp
public sealed class EcsHost : MonoBehaviour
{
    World _world;

    void Start()
    {
        _world = new World();
        // Register systems — pick ONE of:
        Generated.SystemRegistry.Register(_world);                     // generated (reflection-free)
        // _world.DiscoverSystems(SystemDiscovery.FromLoadedAssemblies()); // reflection oracle

        var e = _world.Create();
        _world.Add(e, new Position { X = 0f });
        _world.Add(e, new Velocity { X = 1f });
    }

    void Update()
    {
        _world.DeltaTime = Time.deltaTime;               // feed the clocks BEFORE Update
        _world.UnscaledDeltaTime = Time.unscaledDeltaTime;
        _world.Update();                                 // run all systems in phase order
    }

    void OnDestroy() => _world.Dispose();
}
```

Decisions a consumer makes:

- **Who owns the World and when it ticks.** Any MonoBehaviour (or any other update pump) works; the
  core is engine-agnostic. Whether the host is per-scene or `DontDestroyOnLoad` is your call — call
  `world.SceneChanged(sceneName)` on scene loads if you use `[ActiveInScene]` systems.
- **System discovery: generated vs reflection.** Ship the generated `SystemRegistry` (Tools ▸ PFound
  ECS ▸ Regenerate System Registry emits `SystemRegistry.g.cs` — a `Register(World)` that news up each
  system, no runtime reflection) for IL2CPP/perf; `DiscoverSystems` reflection is the parity oracle.
- **No ScriptableObject, no DI registration required.** Systems self-describe via attributes; there
  is nothing to author in the inspector.

**Unity NativeArray adapters (optional).** The pure core deliberately returns managed `QueryResult`
/ `InteractionView`. If you want `NativeArray`, reference the `PFound.ECS.Unity` assembly (namespace
`PFound.ECS.Unity`, depends on Unity.Collections) for the extension methods
`QueryResult.ToNativeArray(Allocator)`, `World.EntitiesNative(QueryId, Allocator)`,
`InteractionView.ToNativeArray(Allocator)`, `InteractionManager.GetNative(int type, Allocator)`. The
caller owns the returned array (`Dispose` it, or use `Allocator.Temp`).

All PFound.ECS assemblies are `autoReferenced:false`, so a consumer asmdef must reference
`PFound.ECS` (and `PFound.ECS.Unity` if it wants the native adapters) explicitly.

## Testing

`Tests/` is a standalone runner (no Unity/dotnet needed):

```
csc -nologo -warn:0 -out:/tmp/pf_ecs.exe ECS/Runtime/*.cs ECS/Tests/*.cs && mono /tmp/pf_ecs.exe
```

## Layout

- `Runtime/` — `World`, `Entity`, `ComponentPool`/`ComponentMask`/`ComponentType`, `Query`,
  `CommandBuffer`, `EventManager`, `InteractionManager`, `SystemBase` + attributes + discovery.
  Assembly `PFound.ECS` (no engine, no third-party deps).
- `Editor/` — `SystemRegistryGenerator` (system-registry codegen). Assembly `PFound.ECS.Editor`.
- `Unity/` — `NativeEcsAdapters` (NativeArray extensions). Assembly `PFound.ECS.Unity`.
- `Samples/` — Transform / Camera / Render / Particle modules + `SampleBootstrap`. Assembly
  `PFound.ECS.Samples`.
- `Tests/` — csc/mono runner.
