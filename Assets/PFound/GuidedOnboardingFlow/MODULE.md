# GuidedOnboardingFlow

## Purpose

A guided, step-driven onboarding/tutorial engine for Unity. A `TutorialManager` runs one tutorial
at a time — an ordered list of steps (dialog, focus-an-element, open-a-screen, wait-for-signal) —
driven either manually or by a trigger. The state machine is engine-free (`Core`); the Unity layer
supplies the overlay UI seams, ScriptableObject authoring, and a DI installer.

## Assemblies

| Assembly | Folder | Notes |
| --- | --- | --- |
| `PFound.GuidedOnboardingFlow.Core` | `Core/Runtime/` | Engine-free state machine. `noEngineReferences: true`, `autoReferenced: false`. Unit-testable without Unity. |
| `PFound.GuidedOnboardingFlow` | `Runtime/` | Installer/host, concrete steps, authoring assets, UI seams. `autoReferenced: false`. |
| `PFound.GuidedOnboardingFlow.Editor` | `Editor/` | Editor-only (`TutorialId` drawer). |
| `PFound.GuidedOnboardingFlow.Sample` | `Sample/` | Minimal bootstrap sample. |
| `PFound.GuidedOnboardingFlow.Core.Tests` | `Core/Tests/` | Standalone csc/mono runner (engine-free). |
| `PFound.GuidedOnboardingFlow.Tests.EditAndPlayModes` | `Tests/` | NUnit edit/play-mode coverage of the Unity layer. |

## Dependencies

- **Core** (`PFound.GuidedOnboardingFlow.Core`) — no dependencies (`noEngineReferences`).
- **Runtime** (`PFound.GuidedOnboardingFlow`) references `Core`, `PFound.DependencyContainer`,
  `PFound.Signaling`, `PFound.ScreenRouter`, `PFound.TweenPresetLibrary.Core`, `PFound.UserPrefs`.
- `PFound.ScreenRouter` powers `OpenScreenStep`; `PFound.Signaling` powers `WaitForSignalStep<T>`;
  `PFound.UserPrefs` backs `UserPrefsCompletionStore`. All three are opt-in per project.

## Key Types

**Core state machine (`PFound.GuidedOnboardingFlow.Core`)**

- `ITutorialManager` / `TutorialManager` — the DI-facing service that owns the single active run.
- `TutorialRunner` — drives one `TutorialInstance`'s steps in order through a phase lifecycle.
- `TutorialInstance` — an instantiated tutorial: `Id`, `DisplayName`, ordered `Steps`.
- `TutorialBlueprint` — a definition: `Id`, `Mode`, `Trigger`, `Precondition`; `Instantiate()`.
- `TutorialId` — compact `struct` handle backed by an `int handle` (`None` == 0).
- `ITutorialStep` / `TutorialStepBase` — the step contract and its timeout/fault base.
- Enums — `RunnerPhase`, `TutorialOutcome`, `StepReadiness`, `StepStatus`, `StepCancelReason`,
  `TriggerMode` (`Manual` / `Automatic`).

**Steps** — core `LogStep` (over `ITutorialLog`), `DelayStep`; Unity `DialogStep`, `FocusStep`,
`OpenScreenStep`, `WaitForSignalStep<TSignal>`.

**Triggers** — `ITutorialTrigger.ShouldFire(in TriggerContext)`, `TutorialTriggerBase`, `StartupTrigger`.

**Completion stores** — `ITutorialCompletionStore`; impls `InMemoryCompletionStore` (default,
volatile), `SessionCompletionStore` (both in `Core`), and `UserPrefsCompletionStore` (Runtime,
persists via `PFound.UserPrefs`).

**Authoring ScriptableObjects (`PFound.GuidedOnboardingFlow.Authoring`)** — `TutorialCatalog`,
`TutorialDefinition`, abstract `TutorialStepAuthoring` (+ `LogStepAuthoring`, `FocusStepAuthoring`,
`DialogStepAuthoring`), abstract `TutorialTriggerAuthoring` (+ `StartupTriggerAuthoring`).

**UI seams** — interfaces `IInputBlocker`, `ITutorialHand`, `IHighlightMask`, `IDialogPanel`,
`ITutorialCanvas`, `ITutorialAnchorRegistry`; MonoBehaviour impls `InputBlocker`, `TutorialHand`,
`HighlightMask`, `DialogPanel`, `TutorialCanvas`, `SceneAnchor`; and pure `TutorialAnchorRegistry`.

**Composition** — `TutorialInstaller`, `TutorialRunnerHost`, `TutorialRuntimeServices`.

## Model

- **One active run.** `TutorialManager` owns a single `TutorialRunner`. On idle `Tick`, it polls
  `Automatic` blueprints in registration order; the first eligible one whose `Trigger.ShouldFire`
  returns true (and that isn't already completed / is whitelisted) starts. When several become
  eligible on the same tick the first-registered one wins (deterministic tie-break). `Completed`
  outcomes are persisted to the completion store so they don't replay.
- **Runner lifecycle.** Constructed `Pending`; `Begin()` moves it to `Active`; it settles in `Ended`
  with a `TutorialOutcome`. Each tick it confirms preconditions still hold, gates the current step on
  its readiness check, then advances it, promoting to the next step on a normal finish. Zero-duration
  steps chain within the same tick (bounded by step count as a safety net).
- **Steps** implement `ITutorialStep` (`CheckReadiness` → `Begin` → `Advance(dt)` →
  `Complete`/`Cancel`), usually via `TutorialStepBase` (adds timeout + fault bookkeeping). A step
  signals readiness `Ready` / `Deferred` / `Unreachable` so a `FocusStep` can wait for its anchor to
  appear; `Deferred` holds the run, `Unreachable` ends it as `Invalidated`.

## Public API

**Manager (`ITutorialManager` / `TutorialManager`)**

```csharp
TutorialRunner Active { get; }          // live runner, or null when idle
bool IsRunning { get; }
bool AutoAdvance { get; set; }          // interactive steps self-complete when true

event Action<TutorialId> TutorialStarted;
event Action<TutorialId, TutorialOutcome> TutorialEnded;
event Action<ITutorialStep> StepStarted;

bool TryStart(TutorialId id);           // no-op + false if already running
void ForceStart(TutorialId id);         // aborts the current run first
void Skip();                            // player-cancel the active run
void SetRunSpecific(IEnumerable<TutorialId> ids);   // whitelist; empty/null clears it
void Tick(float deltaSeconds);          // advance active run; poll automatic triggers when idle
```

**Runner (`TutorialRunner`)**

```csharp
RunnerPhase Phase { get; }              // Pending / Active / Ended
TutorialOutcome? Outcome { get; }
TutorialId Id { get; }
int StepIndex { get; }
int StepCount { get; }
ITutorialStep CurrentStep { get; }      // null when pending/ended

event Action<ITutorialStep> StepStarted;
event Action<TutorialRunner> Ended;

void Begin();
void Tick(float deltaSeconds);
void Skip();                            // → Skipped
void NotifyShutdown();                  // → Shutdown
```

**Steps**

```csharp
// Core (engine-free):
new LogStep(ITutorialLog log, string message);
new DelayStep(float seconds);

// Unity:
new DialogStep(...);
new FocusStep(...);
OpenScreenStep.For<TScreen>(ScreenRouter router, float? timeout = null);
new WaitForSignalStep<TSignal>(...);    // over PFound.Signaling.SignalTracker
```

**Triggers** — `bool ShouldFire(in TriggerContext context)`; base `TutorialTriggerBase`; built-in
`StartupTrigger`.

**Completion stores (`ITutorialCompletionStore`)**

```csharp
Task HydrateAsync();
bool IsCompleted(TutorialId id);
void MarkCompleted(TutorialId id);
void Clear(TutorialId id);
// UserPrefsCompletionStore(IPrefsStore store) uses PrefKey<string> CompletedKey ("gof.completed").
```

**Authoring** — `TutorialCatalog` (create-asset `PFound/Guided Onboarding/Catalog`) holds a list of
`TutorialDefinition` (a `TutorialId` handle, `TriggerMode`, optional trigger asset, ordered
`TutorialStepAuthoring` list); `_catalog.BuildBlueprints(services)` resolves them. `TutorialStepAuthoring`
concretes expose `ITutorialStep CreateStep(TutorialRuntimeServices)`; `TutorialTriggerAuthoring`
concretes expose `ITutorialTrigger CreateTrigger()`. `OpenScreen`/`WaitForSignal` steps are code-only
(no authoring asset).

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

## File Structure

```
GuidedOnboardingFlow/
├── Core/
│   ├── Runtime/            PFound.GuidedOnboardingFlow.Core (engine-free state machine)
│   │   ├── ITutorialManager.cs / TutorialManager.cs
│   │   ├── TutorialRunner.cs / TutorialInstance.cs / TutorialBlueprint.cs / TutorialId.cs
│   │   ├── ITutorialStep.cs / TutorialStepBase.cs / BuiltInSteps.cs   (LogStep, DelayStep)
│   │   ├── Triggers.cs      (ITutorialTrigger, TutorialTriggerBase, StartupTrigger)
│   │   ├── CompletionStores.cs / ITutorialCompletionStore.cs
│   │   └── Enums.cs
│   └── Tests/              PFound.GuidedOnboardingFlow.Core.Tests (csc/mono runner)
├── Runtime/                PFound.GuidedOnboardingFlow (Unity layer)
│   ├── TutorialInstaller.cs / TutorialRunnerHost.cs / TutorialRuntimeServices.cs
│   ├── UserPrefsCompletionStore.cs / UnityTutorialLog.cs
│   ├── Steps/              DialogStep, FocusStep, OpenScreenStep, WaitForSignalStep
│   ├── Authoring/          TutorialCatalog, TutorialStepAuthoring, TutorialTriggerAuthoring
│   └── UI/                 UiSeams (interfaces) + impls, TutorialAnchorRegistry, TutorialSpotlight.shader
├── Editor/                 PFound.GuidedOnboardingFlow.Editor (TutorialId drawer)
├── Sample/                 PFound.GuidedOnboardingFlow.Sample (SampleOnboardingBootstrap)
└── Tests/                  PFound.GuidedOnboardingFlow.Tests.EditAndPlayModes
```

## Downstream Dependents

None within PFound — this is a consumer-facing feature module. It is wired per game at the
composition root; the four cross-module dependencies (ScreenRouter, Signaling, UserPrefs,
TweenPresetLibrary.Core) are its collaborators, not its dependents.

## Testing

`Core.Tests` is a standalone csc/mono runner (engine-free) covering the runner, manager,
persistence, and step semantics. `Tests.EditAndPlayModes` covers the Unity layer (step and UI
layers). The MonoBehaviour/overlay wiring is verified in the editor on integration.

## Limitations / Known Gaps

- **One active run at a time** by design — there is no concurrent-tutorial support.
- **The consumer supplies the overlay objects.** The module never spawns the `Canvas` / overlay
  MonoBehaviours or the tick host; unwired inspector slots surface as fail-fast errors, not silent
  no-ops (nothing here is expected to be null).
- **The manager does not self-tick** — without a bound `TutorialRunnerHost` (or an equivalent caller
  of `Tick`) nothing advances.
- `OpenScreenStep` / `WaitForSignalStep<T>` are code-only (no authoring asset) and inert unless the
  matching `router` / `signals` collaborator is passed to `Install`.
</content>
