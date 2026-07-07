# PFound.GuidedOnboardingFlow

A guided, step-driven onboarding/tutorial engine for Unity. A `TutorialManager` runs one tutorial
at a time — an ordered list of steps (dialog, focus-an-element, open-a-screen, wait-for-signal) —
driven either manually or by a trigger. The state machine is engine-free (`Core`); the Unity layer
supplies the overlay UI seams, ScriptableObject authoring, and a DI installer.

## Model

- **Engine-free core** (`PFound.GuidedOnboardingFlow.Core`, `noEngineReferences`) — `TutorialManager`,
  `TutorialRunner`, `TutorialInstance`, `TutorialBlueprint`, `TutorialId`, the step contract, and
  triggers/completion stores. Unit-testable without Unity.
- **One active run.** `TutorialManager` owns a single `TutorialRunner`. On idle `Tick`, it polls
  `Automatic` blueprints in registration order; the first eligible one whose `Trigger.ShouldFire`
  returns true (and that isn't already completed / is whitelisted) starts. `Completed` outcomes are
  persisted to the completion store so they don't replay.
- **Steps** implement `ITutorialStep` (`CheckReadiness` → `Begin` → `Advance(dt)` → `Complete`/`Cancel`),
  usually via `TutorialStepBase` (adds timeout + fault bookkeeping). A step signals readiness
  `Ready`/`Deferred`/`Unreachable` so a `FocusStep` can wait for its anchor to appear.

## Public API

**Manager (`ITutorialManager` / `TutorialManager`):** `Active`, `IsRunning`, `AutoAdvance`;
events `TutorialStarted(TutorialId)`, `StepStarted(ITutorialStep)`, `TutorialEnded(TutorialId, TutorialOutcome)`;
`bool TryStart(TutorialId)` (no-op if already running), `ForceStart(TutorialId)`, `Skip()`,
`SetRunSpecific(IEnumerable<TutorialId>)` (whitelist), `Tick(float dt)`.

**Runner (`TutorialRunner`):** `Phase` (`Pending`/`Active`/`Ended`), `Outcome`, `Id`, `StepIndex`,
`StepCount`, `CurrentStep`; `Begin()`, `Tick(float)`, `Skip()`, `NotifyShutdown()`.

**Steps:** core `LogStep`, `DelayStep`; Unity `DialogStep`, `FocusStep`,
`OpenScreenStep` (`OpenScreenStep.For<TScreen>(router, timeout)` over `PFound.ScreenRouter`),
`WaitForSignalStep<TSignal>` (over `PFound.Signaling.SignalTracker`).

**Triggers:** `ITutorialTrigger.ShouldFire(in TriggerContext)`, `TutorialTriggerBase`, `StartupTrigger`.

**Completion stores (`ITutorialCompletionStore`):** `HydrateAsync()`, `IsCompleted(id)`,
`MarkCompleted(id)`, `Clear(id)`. Impls: `InMemoryCompletionStore` (default, volatile),
`SessionCompletionStore`, and `UserPrefsCompletionStore` (persists via `PFound.UserPrefs` `IPrefsStore`).

**Authoring (ScriptableObjects):** `TutorialCatalog` (list of `TutorialDefinition`), abstract
`TutorialStepAuthoring` (concretes `LogStepAuthoring`, `FocusStepAuthoring`, `DialogStepAuthoring`),
abstract `TutorialTriggerAuthoring` (`StartupTriggerAuthoring`). `OpenScreen`/`WaitForSignal` steps
are code-only (no authoring asset).

**UI seams:** interfaces `IInputBlocker`, `ITutorialHand`, `IHighlightMask`, `IDialogPanel`,
`ITutorialCanvas`, `ITutorialAnchorRegistry`; MonoBehaviour impls `InputBlocker`, `TutorialHand`,
`HighlightMask`, `DialogPanel`, `TutorialCanvas`, `SceneAnchor`, and `TutorialAnchorRegistry`.

## Setup / wiring

There is **no static `Instance`, no singleton, and no `DontDestroyOnLoad` — everything is
scene-scoped.** The manager is a plain object owned by your DI container / scene; the consumer owns
its lifetime. Wiring has four parts: a scene installer, the tick host, an overlay canvas, and an
authored catalog.

1. **Place a `TutorialInstaller` MonoBehaviour** in the scene. It is the composition root: its
   `Install(...)` method builds the anchor registry, `TutorialRuntimeServices`, blueprints, and the
   `TutorialManager`, then registers `ITutorialManager`, `ITutorialAnchorRegistry`, and
   `TutorialRuntimeServices` into your `PFound.DependencyContainer` container.
2. **Place a `TutorialRunnerHost` MonoBehaviour** and drag it into the installer's `_host` slot.
   It is the tick driver — its `Update()` calls `Manager.Tick(Time.unscaledDeltaTime)` and
   `OnApplicationQuit` shuts the active run down cleanly. The manager does **not** tick itself.
3. **Build an overlay `Canvas`** carrying `TutorialCanvas` + `InputBlocker` + `TutorialHand` +
   `HighlightMask` + `DialogPanel`, and drag each into the installer's inspector slots. The module
   does **not** spawn these — you provide them as scene objects.
4. **Author a `TutorialCatalog`** (create-asset menu `PFound/Guided Onboarding/Catalog`) with one
   `TutorialDefinition` per tutorial (a `TutorialId` handle, mode, optional trigger asset, and an
   ordered list of `TutorialStepAuthoring` assets), and assign it to the installer's `_catalog`.
5. **Tag focus targets** by dropping `SceneAnchor` (with a string tag) on UI elements. Anchors
   present at boot go in the installer's `_anchors[]` (bound immediately); others self-register on
   enable through the shared `TutorialAnchorRegistry`. `FocusStep` resolves its target by that tag.

From a composition root (`Start()`):

```csharp
var container = new DiContainer();
// Pass router/signals/store to opt into OpenScreenStep / WaitForSignalStep / persistence:
ITutorialManager manager = _installer.Install(
    container,
    router: screenRouter,                 // optional — PFound.ScreenRouter, for OpenScreenStep
    signals: signalTracker,               // optional — PFound.Signaling, for WaitForSignalStep
    store: new UserPrefsCompletionStore(prefsStore));  // optional — defaults to InMemoryCompletionStore

manager.TutorialStarted += id => { /* ... */ };
manager.TryStart(new TutorialId(1));      // or leave it to an Automatic trigger
```

`Install` defaults the completion store to `InMemoryCompletionStore` and the `router`/`signals`
params to null — omit them if a project doesn't use screen or signal steps. See
`Sample/SampleOnboardingBootstrap.cs` for the minimal container + install + event wiring (with
manual `T`/`S`/`A` keys to start/skip/toggle auto-advance).

## Layout

- `Core/Runtime/` — engine-free state machine. Assembly `PFound.GuidedOnboardingFlow.Core`
  (`noEngineReferences`, `autoReferenced:false`).
- `Runtime/` — installer/host, concrete `Steps/`, `Authoring/` assets, `UI/` seams. Assembly
  `PFound.GuidedOnboardingFlow` (refs `Core`, `DependencyContainer`, `Signaling`, `ScreenRouter`,
  `TweenPresetLibrary.Core`, `UserPrefs`).
- `Editor/` — `TutorialId` drawer. `Sample/` — `SampleOnboardingBootstrap`.

## Testing

`Core.Tests` is a standalone csc/mono runner (engine-free); `Tests.EditAndPlayModes` covers the
Unity layer. The MonoBehaviour/overlay wiring is verified in the editor on integration.
