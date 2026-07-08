# ECS

## Purpose

A pure-C# sparse-set ECS runtime: a `World` of entities and `struct` components, ref-returning
queries, a deferred `CommandBuffer`, deferred events, transient interactions, and an attribute-driven,
phase-ordered system scheduler. The core has **no** Unity, Burst, Collections or Mathematics
dependency — you `new World()` and drive it from any update loop.

## Assemblies

- `PFound.ECS` (runtime) — the whole pure-C# core. `noEngineReferences: true`, `autoReferenced: false`.
- `PFound.ECS.Editor` — `SystemRegistryGenerator` (system-registry codegen). rootNamespace
  `PFound.ECS.EditorTools`.
- `PFound.ECS.Unity` — `NativeEcsAdapters` (NativeArray extensions, `using Unity.Collections`).
  `autoReferenced: false`.
- `PFound.ECS.Samples` — Transform / Camera / Render / Particle modules + `SampleBootstrap`.
- `PFound.ECS.Tests` — standalone `csc`/`mono` runner.

All PFound.ECS assemblies are `autoReferenced: false`, so a consumer asmdef must reference
`PFound.ECS` (and `PFound.ECS.Unity` for the native adapters) explicitly.

## Dependencies

- Core: none (no engine, no third-party).
- `PFound.ECS.Unity`: `Unity.Collections` (for `NativeArray`), via the `using` directive.
- Editor/Samples reference `PFound.ECS`. No scripting defines.

## Key Types

`PFound.ECS` namespace:

- `World` — owns entities, component pools, the query cache, `Events`, `Interactions`. Partial across
  `World.cs`, `World.HighArity.cs`, `World.Systems.cs`.
- `Entity` — an `Id`/`Version` pair.
- `ComponentPool`, `ComponentMask`, `ComponentType` — per-type sparse-set storage + archetype mask +
  type registry (max 256 component types).
- `QueryBuilder<T1..T10>`, `QueryId`, `QueryResult`, `RefAction<...>` — the query surface.
- `CommandBuffer` — deferred structural changes.
- `EventManager` (`World.Events`) + `SpanAction<T>` (batch handler), `InteractionManager`
  (`World.Interactions`), `Interaction`, `InteractionView`, `InteractionType` /
  `InteractionTypeRegistry` / `InteractionRole` (named, typed interaction keys).
- `SystemBase` + attributes `ECSSystemAttribute` / `DependsOnAttribute` / `ActiveInSceneAttribute`,
  enums `ECSPhase` / `UpdateTime`. `SystemDiscovery`, `SystemRegistryEmitter` (reflection + codegen).

`PFound.ECS.Unity`: `NativeEcsAdapters` (extension methods).
`PFound.ECS.EditorTools`: `SystemRegistryGenerator`.

## Public API

**World lifecycle** — `new World()`; `Dispose()`. Per-frame clocks are public fields set by the host
before ticking: `float DeltaTime`, `float UnscaledDeltaTime`; scene name via
`void SceneChanged(string)` (readable as `string CurrentScene`).

**Entities** — `Entity Create()`, `void Destroy(Entity)`, `void DestroyAll()`, `bool IsValid(Entity)`,
`bool IsAlive(Entity)`.

**Components** — `void Add<T>(Entity, in T)`, `bool AddIfMissing<T>(Entity, in T)`,
`void SetOrAdd<T>(Entity, in T)`, `ref T Get<T>(Entity)`, `bool TryGet<T>(Entity, out T)`,
`bool Has<T>(Entity)`, `void Remove<T>(Entity)`, `bool RemoveIfExists<T>(Entity)`. Components are
plain `struct`s, auto-registered on first use.

- `Span<T> GetAllComponents<T>()` — bulk single-array access to the packed dense store of every
  `T`. Zero-copy over live storage (writes through the span mutate in place); dense order, not
  entity order; invalidated by the next structural change to that pool.

**Queries** — `QueryBuilder<T1..T10> Query<...>()`. Refine with `.Without<TX>()`, then either:

- `void ForEach(RefAction<...>)` — zero-alloc iteration over the dense arrays:
  ```csharp
  world.Query<Position, Velocity>().ForEach(
      (World w, Entity e, ref Position p, ref Velocity v) => p.X += v.X * w.DeltaTime);
  ```
- `QueryId Build()` (cache it in `CreateQueries`) → `QueryResult Entities(QueryId)` — a managed,
  zero-alloc view (`.Count`, indexer, enumerator) valid until the next structural change.
- `void FlushQueries()` bumps the structure version manually if you need to invalidate cached views.

**CommandBuffer** — `CommandBuffer CreateCommandBuffer()` records structural changes safely during
iteration: `Entity Create()`, `void Destroy(Entity)`, `void Add<T>(Entity, in T)`,
`void Remove<T>(Entity)`, `int Count`; apply with `void Playback()`.

**Events (`World.Events`)** — `IDisposable Subscribe<T>(Action<T>)` (per-event),
`IDisposable SubscribeBatch<T>(SpanAction<T>)` (all queued events of a type delivered in one
`Span<T>` call), `void Publish<T>(in T)` (queues), `void Dispatch()` (delivers per-event handlers
then batch handlers, in publish order, re-entrancy safe), `int PendingCount`. `T : struct`.

**Interactions (`World.Interactions`)** — directed entity↔entity relationships keyed by an
interaction-type id (a raw `int`, or a named `InteractionType` that implicitly converts to `int`).

- Record / reset: `void Start(int, Entity interactor, Entity interactee)` (de-dupes an identical
  active interaction; reactivates a completed one), `void Clear()` / `void Clear(int)`.
- Forward reads: `InteractionView Get(int)`, `InteractionView OfInteractor(int, Entity)`,
  `void ForEachInteractee(int, Entity, Action<Entity>)`.
- Reverse (interactee→interactor) reads: `InteractionView OfInteractee(int, Entity)`,
  `void ForEachInteractor(int, Entity, Action<Entity>)`.
- Completion state: `void CompleteInteraction(in Interaction)`, `bool IsComplete(in Interaction)`,
  `void CompleteAllOfInteractor(int, Entity)`, `void CompleteAllOfType(int)`.
- Existence / role: `bool Exists(int, Entity interactor, Entity interactee)`,
  `bool HasInteractionOfType(int, Entity)` (either role),
  `bool HasRole(int, Entity, InteractionRole)`.
- Dirty tracking: `bool IsDirty(int)`, `IReadOnlyCollection<int> DirtyTypes`, `void ClearDirtyMask()`,
  and a monotonic `int Version` (drives interaction-query cache invalidation).
- Typed keys: `InteractionType` (`Create(name)` / `Named(value, name)` / `Invalid` / `Reverse`) with
  an id↔name `InteractionTypeRegistry` (registers a `name + "_reverse"` for the reverse id).
- `Interaction` carries `Id`, `Type`, `Interactor`, `Interactee`.

**Systems** — subclass `SystemBase`, override `protected abstract void Execute()`, optionally
`protected virtual void OnInitialize()` / `protected virtual void CreateQueries()` /
`protected virtual void OnDispose()`. `protected float DeltaTime` resolves to the scaled or unscaled
clock per the attribute. Each system also gets:

- `protected CommandBuffer CommandBuffer` — a per-system deferred buffer that the world **plays
  back automatically** right after `Execute()`. Record structural changes during iteration; no
  manual `Playback()` needed.
- `protected void Subscribe<T>(Action<T>)` / `protected void SubscribeBatch<T>(SpanAction<T>)` —
  lifecycle-managed event subscriptions that are **auto-unsubscribed** when the system is torn down.
- `public void Dispose()` — teardown (drops managed subscriptions, then `OnDispose()`); called
  automatically by `World.Dispose()` / `World.ReloadScene(...)`.

Class-level attributes:

- `[ECSSystem(ECSPhase phase = OnUpdate, UpdateTime time = Scaled)]`.
- `[DependsOn(typeof(OtherSystem), ...)]` — orders within a phase (topo-sorted; a dependency cycle
  is reported as an `InvalidOperationException`, not silently broken).
- `[ActiveInScene("Level1", ...)]` — scene-scoped (matched against `CurrentScene`).

Phases run in declaration order: `PreUpdate`, `OnUpdate`, `PostUpdate`, `PreRender`, `OnRender`,
`PostRender`, `PostFrame`.

**Pause gate.** `Scaled` systems run only while time advances (`DeltaTime > 0`); `Unscaled` systems
always run. Setting `DeltaTime = 0` (e.g. a paused game) therefore skips scaled systems while
unscaled ones keep ticking. Feed a positive `DeltaTime` before `Update()` to run scaled systems.

**System registration & tick** — `void RegisterSystem(SystemBase)` for one instance, or
`void DiscoverSystems(IEnumerable<Type>)` to reflect-and-register many (`SystemDiscovery
.FromLoadedAssemblies()` supplies the type list). `T GetSystem<T>()` / `bool TryGetSystem<T>(out T)`
look up a registered system by type. `void Update()` runs every active system in phase then
dependency order; `void RunPhase(ECSPhase)` runs a single phase.

**Scene lifecycle.** `void SceneChanged(string)` is the light, SPEC default: systems persist and are
merely re-filtered by `[ActiveInScene]`. `void ReloadScene(string)` is the destructive legacy
lifecycle: it disposes every system, clears all entities / components / events / interactions,
switches scene, then re-initializes the systems.

**Interaction-scoped queries.** A query can additionally require an interaction role:
`world.Query<Combatant>().WithInteraction(Attack).AsInteractor()` (or `.AsInteractee()`, or
`.WithInteraction(type, InteractionRole.Interactee)`) matches only entities that play that role in
an interaction of the given type. Works on both `.ForEach(...)` and the cached `.Build()` path
(cache refreshes when the interaction store changes).

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
        Generated.SystemRegistry.Register(_world);                      // generated (reflection-free)
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
- **No ScriptableObject, no DI registration required.** Systems self-describe via attributes; there is
  nothing to author in the inspector.

**Unity NativeArray adapters (optional).** The pure core deliberately returns managed `QueryResult` /
`InteractionView`. For `NativeArray`, reference `PFound.ECS.Unity` (namespace `PFound.ECS.Unity`) for
the extension methods `QueryResult.ToNativeArray(Allocator)`, `World.EntitiesNative(QueryId,
Allocator)`, `InteractionView.ToNativeArray(Allocator)`, `InteractionManager.GetNative(int
interactionType, Allocator)`. The caller owns the returned array (`Dispose` it, or use
`Allocator.Temp`).

## File Structure

```
ECS/
  Runtime/                                # PFound.ECS (no engine, no third-party)
    World.cs, World.HighArity.cs, World.Systems.cs
    Entity.cs, ComponentPool.cs, ComponentMask.cs, ComponentType.cs
    Query.cs, QueryBuilders.HighArity.cs
    CommandBuffer.cs, EventManager.cs, InteractionManager.cs, InteractionType.cs
    SystemBase.cs, SystemAttributes.cs, SystemDiscovery.cs, SystemRegistryEmitter.cs
  Editor/     SystemRegistryGenerator.cs                     # PFound.ECS.Editor
  Unity/      NativeEcsAdapters.cs                           # PFound.ECS.Unity
  Samples/    TransformModule, CameraModule, RenderModule, ParticleModule,
              SampleBootstrap, SystemRegistry.g.cs           # PFound.ECS.Samples
  Tests/      Program.cs + Core/Query/CommandBuffer/Event/Interaction/Data/System characterization tests
```

## Downstream Dependents

None within PFound. The four `Samples/` modules are the reference consumers; game code owns its own
`World` host.

## Extension points

- **Components** — any `struct` becomes a component on first `Add<T>` (no registration ceremony,
  256-type ceiling).
- **Systems** — subclass `SystemBase` + attributes; discovered via codegen or reflection.
- **Events / Interactions** — arbitrary `struct` event types; `int`-keyed interaction types are the
  consumer's own enum.
- **Native interop** — the `PFound.ECS.Unity` adapters bridge managed views to `NativeArray` for
  Burst/jobs without coupling the core to `Unity.Collections`.

## Limitations / Known Gaps

- Main-thread only; no internal locking. The optional `NativeArray` adapters enable jobs interop but
  the World itself is single-threaded.
- `Scaled` systems are paused when `DeltaTime == 0` (the pause gate) — feed a positive `DeltaTime`
  before `Update()` or they will not run; `Unscaled` systems are exempt.
- Interactions are transient and owner-managed: nothing auto-clears them — call `Interactions.Clear()`
  (and typically `ClearDirtyMask()`) each frame if you use them as per-frame relationships.
- Max 256 component types per process (mask width).
- Queries support up to 10 component type parameters (`T1..T10`) plus `.Without<TX>()` exclusions.
- `QueryResult` / `InteractionView` are borrowed views — invalidated by the next structural change;
  do not store them across an `Add`/`Remove`/`Destroy` or `Playback`.
- Systems must have a constructor the chosen discovery path can invoke (the generated registry needs a
  public parameterless ctor; `DiscoverSystems` can reflect a non-public one).

## Design

The non-obvious model internals (sparse-set layout, entity version recycling, query-cache
invalidation, deferred command playback, phase + topo-sort scheduling) are documented in
[DESIGN.md](DESIGN.md).

## Testing

`Tests/` is a standalone runner (no Unity/dotnet needed):

```
csc -nologo -warn:0 -out:/tmp/pf_ecs.exe ECS/Runtime/*.cs ECS/Tests/*.cs && mono /tmp/pf_ecs.exe
```
