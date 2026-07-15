# InputRouter

## Purpose

A backend-agnostic input **intent** router. Caller-supplied source adapters turn raw device input
into typed intent structs; the router derives a four-phase lifecycle per intent and dispatches each
phase to its subscribers in stable registration order, once per tick. The core is engine-free — the
only place a real backend (legacy Input Manager, Input System) is touched is inside a caller's
`IIntentSource<T>` adapter.

## Assemblies

- `PFound.InputRouter.Core` — engine-free router + interfaces. `noEngineReferences: true`,
  `autoReferenced: false`. rootNamespace `PFound.InputRouter`.
- `PFound.InputRouter` (runtime) — Unity glue (`IntentRouterDriver`, `PointerOverUiGate`). References
  `PFound.InputRouter.Core`. `autoReferenced: true`.
- `PFound.InputRouter.Samples` — dual-backend example sources + intents. References the Core, runtime,
  and `Unity.InputSystem`; maps `com.unity.inputsystem` to the `PFOUND_INPUTSYSTEM` define.
  `autoReferenced: false`.
- `PFound.InputRouter.Core.Tests` (csc/mono) + `PFound.InputRouter.Tests` (EditMode).

## Dependencies

- Runtime → `PFound.InputRouter.Core`. Samples → Core + runtime + `Unity.InputSystem`.
- The sample sources compile-gate on Unity's own backend defines: `ENABLE_LEGACY_INPUT_MANAGER` and
  `ENABLE_INPUT_SYSTEM` (the latter also mapped to `PFOUND_INPUTSYSTEM` by the Samples asmdef).
- The Core itself has no engine or third-party dependency.

## Key Types

**Core (`PFound.InputRouter`)**

- `IntentRouter` — the router.
- `IIntent` — marker for an intent `struct` (names a game action, carries its payload).
- `IIntentSource<TIntent>` — single-input source: `IntentReading<TIntent> Read()`.
- `IMultiIntentSource<TIntent>` — multi-input source: `void Read(List<KeyedIntentReading<TIntent>>
  active)`.
- `IntentReading<TIntent>` — `Active(payload)` / `Inactive`; `KeyedIntentReading<TIntent>` adds an
  `InputId`.
- `IntentContext<TIntent>` — dispatch payload (`Payload` + `Phase`); `MultiIntentContext<TIntent>`
  adds `Id`.
- `IntentPhase` — `Idle`, `Started`, `Held`, `Ended`. `IntentGroup` — `Gameplay`, `System`.
- `InputId` — per-input key for multi-input. `IntentChannel` / `MultiIntentChannel`,
  `InsertionOrderedMap` (internal dispatch plumbing that preserves registration order).

**Runtime (`PFound.InputRouter`)** — `IntentRouterDriver` (MonoBehaviour host; also offers
combined `Enable()`/`Disable()`/`SetInputActive(bool)` that gate intent dispatch *and* the active
EventSystem UI input module together, re-synced on active-scene change), `PointerOverUiGate`
(`FromEventSystem()` factory for the UI gate; touch-aware — mouse pointer OR primary touch id 0).

**Samples (`PFound.InputRouter.Samples`)** — `MoveIntent` (fields `Horizontal`, `Vertical`),
`TouchPointIntent`; `LegacyMoveSource` / `InputSystemMoveSource` (single-input),
`LegacyMultiTouchSource` / `InputSystemMultiTouchSource` (multi-input).

## Public API (`IntentRouter`)

- `new IntentRouter(Func<bool> pointerOverUi = null)` — the predicate is the only UI hook; when null,
  UI never suppresses. The core takes no dependency on any EventSystem.
- **Switches:** `bool Enabled` (global; when false `Tick` does nothing), `bool GameplayEnabled` (the
  `Gameplay` group only).
- **Single-input:** `void Register<TIntent>(IIntentSource<TIntent> source, IntentGroup group)`,
  `IDisposable Subscribe<TIntent>(IntentPhase phase, Action<IntentContext<TIntent>> handler)` (dispose
  to unbind), `void Replace<TIntent>(IIntentSource<TIntent> source)` (hot-swap, keeps subscribers +
  dispatch slot), `void Unregister<TIntent>()`, `bool IsRegistered<TIntent>()`.
- **Multi-input:** `void RegisterMulti<TIntent>(IMultiIntentSource<TIntent> source, IntentGroup
  group)`, `IDisposable SubscribeMulti<TIntent>(IntentPhase phase, Action<MultiIntentContext<TIntent>>
  handler)` — the handler fires once per active `InputId` that hits the phase this tick.
- `void Tick()` — poll every source once and dispatch. Call once per update step.

**Phase derivation** — from the active/inactive edge between the previous and current tick:
`was=false,now=false → Idle`; `false→true → Started` (rising edge); `true→true → Held`;
`true→false → Ended` (falling edge). Subscribers bind to exactly one phase.

**Groups** — `Gameplay` is silenced when gameplay is gated off or the pointer is over UI; `System`
(menu/pause/debug) is only silenced by the global `Enabled` switch. A gated channel is fed an
inactive sample (not skipped), so a held input releases cleanly and re-presses as a fresh `Started`
edge once the gate reopens.

An intent is either single or multi, not both. Registering a second source for the same intent, or
subscribing before registering, fail-fast (throw) rather than silently no-op. `IntentReading` must
never throw for a missing device — return `Inactive`.

## Setup / wiring

The Core is engine-free and can be ticked from anywhere; the runtime assembly ships the Unity glue.
Two ways to host it:

**1. MonoBehaviour host (`IntentRouterDriver`).** Drop the component on a GameObject. On `Awake` it
constructs `Router` (wiring the EventSystem UI gate when its serialized `_respectUiGate` bool is on)
and pumps `Router.Tick()` every `Update`. Register sources and subscribe against `Router` after the
component exists (Awake onward). Whether it lives per-scene or is made `DontDestroyOnLoad` is the
consumer's decision — the driver does not persist itself.

*EventSystem input-module control.* Beyond the per-tick UI gate, the driver exposes a combined
switch that suppresses **both** intent dispatch and UI clicks at once — useful during cutscenes,
loading, or modal transitions where the whole input surface should freeze:

- `driver.Disable()` / `driver.Enable()` (or `driver.SetInputActive(bool)`) sets
  `Router.Enabled` **and** toggles the active `EventSystem`'s `BaseInputModule.enabled`.
- The chosen state is remembered and re-applied on `SceneManager.activeSceneChanged`, because a
  newly loaded scene brings its own EventSystem whose input module defaults back to enabled.
- To gate only gameplay intents while leaving UI input live, set `Router.GameplayEnabled` instead
  (that is the per-group switch; `SetInputActive` is the whole-surface switch). The pure
  `IntentRouter` core stays engine-free — only this host reaches into the EventSystem.

```csharp
var driver = gameObject.AddComponent<IntentRouterDriver>();  // or place it in the scene
IntentRouter router = driver.Router;                          // valid from Awake on

router.Register<MoveIntent>(new InputSystemMoveSource(moveAction), IntentGroup.Gameplay);
IDisposable sub = router.Subscribe<MoveIntent>(
    IntentPhase.Held, ctx => _body.Move(ctx.Payload.Horizontal, ctx.Payload.Vertical));
// sub.Dispose() to unbind.
```

**2. Manual host (no MonoBehaviour).** `new IntentRouter(...)` in your own bootstrap and call `Tick()`
from an existing update pump. For the UI gate, pass `PointerOverUiGate.FromEventSystem()` (a
`Func<bool>` true while the current `EventSystem` reports the pointer over UI; false when no
EventSystem is present). The predicate is **touch-aware**: it ORs the mouse pointer id (-1, the
default overload) with the primary touch pointer (id 0), so a UI hit is caught on both desktop and
touch devices — the plain `IsPointerOverGameObject()` never resolves on touch and would let gameplay
intents fire through the UI.

```csharp
var router = new IntentRouter(PointerOverUiGate.FromEventSystem());
// ... register/subscribe ...
void Update() => router.Tick();
```

Consumer asmdefs reference `PFound.InputRouter` (Unity glue) and/or `PFound.InputRouter.Core`
(engine-free core, `autoReferenced:false`).

## Samples — dual Input backends

The router never sees a backend — you register an `IIntentSource<T>` (or `IMultiIntentSource<T>`)
that reads whichever backend you compiled against. The `Samples/` assembly ships a **dual pair per
intent**, backend-gated by Unity's own defines:

- `LegacyMoveSource` / `LegacyMultiTouchSource` under `ENABLE_LEGACY_INPUT_MANAGER` (old Input
  Manager).
- `InputSystemMoveSource` / `InputSystemMultiTouchSource` under `ENABLE_INPUT_SYSTEM` (new Input
  System).

Each pair carries a `MoveIntent` / `TouchPointIntent` payload. Copy or subclass these for your own
intents; the router stays backend-agnostic — both backends can even be enabled at once (Unity's
"Both" active-input-handling setting) and the correct source is picked per compile define. For the
Input System multi-touch source, call `UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport
.Enable()` once at startup before it reports touches.

## File Structure

```
InputRouter/
  Core/                                   # PFound.InputRouter.Core (engine-free)
    IntentRouter.cs, Intent.cs, IIntentSource.cs, MultiIntent.cs
    IntentPhase.cs, IntentGroup.cs, InputId.cs
    IntentChannel.cs, MultiIntentChannel.cs, InsertionOrderedMap.cs
  Runtime/                                # PFound.InputRouter (Unity glue)
    IntentRouterDriver.cs, PointerOverUiGate.cs
  Samples/                                # PFound.InputRouter.Samples (dual-backend)
    MoveIntent.cs, LegacyMoveSource.cs, InputSystemMoveSource.cs, MultiTouchSource.cs
  Core.Tests/     Program.cs, Assert.cs, TestSupport.cs   (csc/mono runner)
  Tests/          IntentRouterEditModeTests.cs            (Unity EditMode)
```

## Downstream Dependents

None within PFound. Consumed by game input code that provides its own `IIntentSource<T>` adapters
(the Samples show the two Unity backends).

## Limitations / Known Gaps

- Main-thread, one dispatch per `Tick()`; no internal threading.
- A source's `Read()` must be non-throwing and cheap — it is polled every tick for every registered
  intent.
- One source per intent; no fan-in of multiple sources onto the same single-input intent (use
  multi-input for many simultaneous inputs).
- Phase derivation is edge-based on the previous tick only — sub-tick presses that start and end
  within one `Tick()` collapse to a single sample.

## Testing

- `Core.Tests/` — standalone `csc`/`mono` runner over the engine-free core (phase derivation,
  registration order, gating, multi-input keying).
- `Tests/IntentRouterEditModeTests.cs` — Unity EditMode NUnit suite (also covers the driver's
  combined `Enable()`/`Disable()` gating the router's dispatch switch; the EventSystem UI-module and
  scene-resync halves need a live EventSystem and are covered by in-editor Unity-verify).
