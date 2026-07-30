# PFound.LoopScheduler

A deterministic multi-phase frame scheduler injected into Unity's PlayerLoop — ordered `LoopPhase`
phases with owner-scoped, re-entrancy-safe callbacks and per-phase Profiler markers. Each phase fires
at its **real engine moment** (a runner node per engine frame slot), not one collapsed up-front pass.
Static and self-installing: nothing to place in a scene.

## Quick reference

```csharp
// no setup — the scheduler installs itself before the first scene loads.
LoopScheduler.RegisterUpdateLoop(Tick, this);                 // persistent; auto-pruned when 'this' dies
LoopScheduler.RegisterBeforeRenderLoop(SyncCamera, this);     // persistent, pre-render hook
LoopScheduler.RegisterAt(LoopPhase.Camera, PlaceCamera, this);// persistent, any of the 15 phases
LoopScheduler.InvokeOnceAt(LoopPhase.EndOfFrame, FlushOnce);  // one-shot
LoopScheduler.DeregisterAt(LoopPhase.Camera, PlaceCamera);    // or DeregisterUpdateLoop(Tick), etc.
bool late = LoopScheduler.IsAfterLateUpdate;                  // which side of late-update am I on?

LoopSchedulerTools.LogPlayerLoop();                           // dump the live PlayerLoop tree
```

## Dependencies

Engine only (`PlayerLoop` / `ProfilerMarker` / `Application.onBeforeRender`). `autoReferenced:false`.

## Docs

Deep reference: [MODULE.md](MODULE.md) — phase→engine-slot map, re-entrancy/ownership model, full
API, and the "just call `Register*`" wiring note.
