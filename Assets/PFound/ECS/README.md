# PFound.ECS

A pure-C# sparse-set ECS runtime: a `World` of entities and struct components, ref-returning
queries, a deferred `CommandBuffer`, deferred events, transient interactions, and an attribute-driven,
phase-ordered system scheduler. The core has **no** Unity/Burst/Collections/Mathematics dependency —
you `new World()` and drive it from any update loop.

## Quick reference

Own a `World` in a thin host and pump it each frame:

```csharp
_world = new World();
Generated.SystemRegistry.Register(_world);        // generated (reflection-free); or _world.DiscoverSystems(...)
// per frame:
_world.DeltaTime = Time.deltaTime;                // feed the clocks BEFORE Update (0 pauses Scaled systems)
_world.Update();                                  // runs every system in phase then dependency order
// on teardown: _world.Dispose();
```

Systems get a per-system `CommandBuffer` (auto-played back after `Execute`), lifecycle-managed
`Subscribe<T>` / `SubscribeBatch<T>` (auto-unsubscribed on teardown), an `OnDispose()` hook, and
`GetSystem<T>()` / `TryGetSystem<T>()` lookup. Bulk component access is `world.GetAllComponents<T>()
-> Span<T>`. Interactions offer bidirectional lookup, completion state, dirty tracking, existence /
role queries, named `InteractionType`s, and interaction-scoped queries
(`Query<…>().WithInteraction(type).AsInteractor()`).

## Dependencies

None (core). Optional `PFound.ECS.Unity` for `NativeArray` adapters (Unity.Collections). All ECS
assemblies are `autoReferenced:false` — a consumer asmdef must reference `PFound.ECS` (and
`PFound.ECS.Unity` if it wants the native adapters) explicitly.

## Docs

Deep reference: [MODULE.md](MODULE.md) — world/entity/component/query/command-buffer/events/
interactions model, the attribute-driven phase-ordered scheduler, generated-vs-reflection system
registry, the optional NativeArray adapters, extension points, and the standalone `csc`/`mono` test
runner. The sparse-set model rationale is in MODULE.md §Design.
