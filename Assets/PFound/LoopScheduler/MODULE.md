# LoopScheduler

## Purpose
A deterministic multi-phase frame scheduler injected into Unity's PlayerLoop. Replaces scattered
`Update()` methods with ordered `LoopPhase` phases plus owner-scoped, re-entrancy-safe callbacks and
a per-phase Profiler marker. Every phase fires at its **real engine moment** — a small runner node is
injected into each engine frame slot (early-update, pre-update, update, pre-late-update,
post-late-update) and each ticks the phases mapped to it in order; `BeforeRender` runs on the
engine's pre-render hook.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.LoopScheduler` | `Runtime/PFound.LoopScheduler.asmdef` | `autoReferenced: false`; references the engine for `PlayerLoop` / `ProfilerMarker` / `Application.onBeforeRender` |

## Dependencies
- Engine only: `UnityEngine.LowLevel.PlayerLoop`, `UnityEngine.PlayerLoop.*` slot types,
  `Unity.Profiling.ProfilerMarker`, `Application.onBeforeRender`, `RuntimeInitializeOnLoadMethod`.
- No other PFound module, no third-party package, no scripting define.

## Key Types

### `PFound.LoopScheduler`
- **`LoopScheduler`** (static) — the self-installing injector and the register/deregister surface.
- **`LoopPhase`** (enum) — the ordered frame phases; declaration order follows the real frame
  timeline, and each phase maps to a distinct PlayerLoop moment.
- **`PhaseCallbacks`** (internal) — engine-free, re-entrancy-safe ordered callback list for one
  phase (deferred add/remove, one-shot entries, owner pruning). Pure C#, testable without Unity.
- **`LoopSchedulerTools`** (static) — PlayerLoop utilities: idempotent injection helpers, plus
  inspection/debug tooling to enumerate the injected runner nodes and pretty-print the PlayerLoop.

## Public API

`LoopScheduler` (static):
```csharp
// persistent registration at ANY phase (auto-pruned when owner dies)
void RegisterAt(LoopPhase phase, Action callback, UnityEngine.Object owner);
void DeregisterAt(LoopPhase phase, Action callback);

// shortcuts (thin wrappers over RegisterAt) — kept for source compatibility
void RegisterUpdateLoop(Action callback, UnityEngine.Object owner);        // == RegisterAt(Update, ...)
void DeregisterUpdateLoop(Action callback);
void RegisterBeforeRenderLoop(Action callback, UnityEngine.Object owner);  // == RegisterAt(BeforeRender, ...)
void DeregisterBeforeRenderLoop(Action callback);

void InvokeOnceAt(LoopPhase phase, Action callback);  // one-shot at next occurrence of phase

bool IsAfterLateUpdate { get; }  // true once past late-update, false again at frame start
```

`LoopSchedulerTools`:
```csharp
IEnumerable<Type> CustomLoops { get; }             // the injected runner node types
void ForEachCustomLoop(Action<Type> action);       // iterate them
bool IsCustomLoop(this PlayerLoopSystem system);   // true if this node is one of ours
void LogPlayerLoop();                               // Debug.Log the whole PlayerLoop tree
string DumpPlayerLoop();                            // pretty-printed tree (our nodes highlighted)
string DumpPlayerLoop(PlayerLoopSystem root);       // dump a supplied tree
```

## Model

- **Phases** (`LoopPhase`, declaration order = execution timeline) and the engine slot each is
  injected into:

  | Phase | Engine slot |
  |---|---|
  | `EarlyUpdate`, `Scene`, `Network` | after `EarlyUpdate` |
  | `Input`, `Selection` | after `PreUpdate` |
  | `Update`, `PostUpdate` | after `Update` |
  | `LateUpdate`, `Camera`, `Bindings`, `PreRender` | after `PreLateUpdate` |
  | `BeforeRender` | `Application.onBeforeRender` pre-render hook |
  | `Render`, `PostRender`, `EndOfFrame` | after `PostLateUpdate` |

  Callbacks within a phase run in registration order.
- **One runner node per engine slot.** For each slot the scheduler injects a runner node as a root
  sibling right after the engine's own slot node; the runner ticks its mapped phases in order. This
  is what makes each phase fire at its true engine moment (camera work at pre-late-update, render
  work at post-late-update, etc.) instead of one collapsed up-front pass. `BeforeRender` alone is
  driven by the engine's `Application.onBeforeRender`.
- **`IsAfterLateUpdate`.** Set `false` when the frame's first (early) runner begins and `true` when
  the post-late-update runner begins — so a callback registered in more than one phase can tell
  which side of late-update it is running on.
- **Re-entrancy safe.** Registrations/removals requested while a phase is invoking are deferred to
  after the pass; a callback added mid-invoke waits for the next frame.
- **Owner-scoped auto-cleanup.** A persistent callback tagged with a `UnityEngine.Object` owner is
  pruned automatically once that owner is destroyed (fake-null), before the next invoke of its phase.
  A `null` owner means caller-managed lifetime and is never pruned — you `Deregister…` it yourself.
- **Per-phase profiling.** Each phase invoke is wrapped in a `ProfilerMarker` named
  `LoopScheduler.<Phase>`.
- **Idempotent injection.** Each runner node is injected only if a node of its type is not already
  present, so a domain reload / repeated init does not duplicate nodes.

## Setup / wiring

**No placement, no instantiation — just call `Register*`.** The scheduler installs itself: a
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` entry point injects the per-slot runner nodes into
the PlayerLoop and subscribes the pre-render hook before the first scene loads. You never `new` it,
place it in a scene, or add it to the PlayerLoop by hand.

```csharp
// from anywhere, once your object exists:
LoopScheduler.RegisterUpdateLoop(Tick, this);                 // 'this' MonoBehaviour owns the callback
LoopScheduler.RegisterAt(LoopPhase.Camera, PlaceCamera, this);// any phase can be subscribed persistently
LoopScheduler.InvokeOnceAt(LoopPhase.EndOfFrame, FlushOnce);
// no manual deregister needed when 'this' is destroyed — it is auto-pruned.

// debugging: dump the live PlayerLoop with our nodes highlighted
LoopSchedulerTools.LogPlayerLoop();
```

Pass a real `owner` for anything with a bounded lifetime so it is dropped when the owner dies; pass
`null` only when you will `Deregister…` it yourself. Main-thread only.

## File Structure
```
LoopScheduler/
  README.md
  MODULE.md
  Runtime/
    PFound.LoopScheduler.asmdef
    LoopScheduler.cs        # static self-installing injector + register/deregister surface + IsAfterLateUpdate
    LoopPhase.cs            # frame-phase enum (timeline order; each phase → an engine slot)
    PhaseCallbacks.cs       # internal, engine-free re-entrancy-safe callback list
    LoopSchedulerTools.cs   # PlayerLoop injection helpers + enumeration/dump debug tooling
```

## Downstream Dependents
- **`PFound.Render.BatchRendering`** and **`PFound.Render.RenderContext`** reference the assembly to
  drive their per-frame work off the scheduler's phases rather than their own `Update()`.

## Limitations / Known Gaps
- **Main-thread only.** The callback lists are not thread-safe; register/invoke from the main thread.
- **No ordering key within a phase.** Callbacks run in registration order; there is no priority
  parameter. Register in the order you need, or split across phases.
- **Same-slot phases share an engine moment.** Phases mapped to the same engine slot run back-to-back
  within that slot's runner (still ordered), not at separate PlayerLoop nodes.
- **Global PlayerLoop mutation.** Injection adds runner nodes to the PlayerLoop for the whole player;
  there is no per-scene or opt-out install.
- **Intentionally omitted (recorded):** no cached-time / fixed-timestep facilities — consumers read
  `Time.*` directly or drive fixed work from a phase.
