# GuidedOnboardingFlow

> **Module group — UI & Presentation.** Sibling modules in this group: `UISystem`, `ScreenRouter`, `TweenPresetLibrary`, `MVC`. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

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
- `TutorialInstance` — an instantiated tutorial: `Id`, `DisplayName`, ordered `Steps`, plus a
  `DefaultTimeoutOutcome` applied to steps that don't set their own.
- `TutorialBlueprint` — a definition: `Id`, `Mode`, `Triggers` (list, AND-combined), `Precondition`,
  `Replay` (replay policy), `DefaultTimeoutOutcome`; `Instantiate()` and `ResetTriggers()`.
- `TutorialId` — compact `struct` handle backed by an `int handle` (`None` == 0).
- `ITutorialStep` / `TutorialStepBase` — the step contract and its timeout/fault base; steps expose
  a nullable `TimeoutOutcome` and `CancellationReason`.
- Enums — `RunnerPhase`, `TutorialOutcome`, `StepStatus`, `StepCancelReason`,
  `TriggerMode` (`Manual` / `Automatic`), `StepReadiness`
  (`Ready`/`Deferred`/`Unreachable`/`SkipAndAdvance`/`GoBackOne`),
  `StepTimeoutOutcome` (`Advance`/`AbortRun`),
  `ReplayPolicy` (`OnceAccount`/`OnceSession`/`Repeatable`),
  `LogSeverity` (`Info`/`Warning`/`Error`), `LogVerbosity` (`Errors`/`Lifecycle`/`Verbose`).

**Steps** — core `LogStep` (over `ITutorialLog`, with a `LogSeverity`), `DelayStep`; Unity
`DialogStep` (typewriter + dismiss mode), `FocusStep` (anchor/`GameObject.Find` target, sprite mask,
hand hint, per-anchor offset/padding, mid-step target-loss abort, auto-click synthesis),
`OpenScreenStep` (readiness + landing-timeout abort), `WaitForSignalStep<TSignal>`.

**Triggers** — `ITutorialTrigger.ShouldFire(in TriggerContext)` + `Reset()`; a tutorial may carry
several (AND semantics). `TutorialTriggerBase`, `StartupTrigger`, `DelayedStartupTrigger`. The
richer `TriggerContext` carries `IsFirstPoll`, `TimeSinceLaunch`, `TutorialId`, `RunCount`.

**Completion stores** — `ITutorialCompletionStore` (`HydrateAsync(CancellationToken)`,
`IsCompleted`, `MarkCompleted(id, ReplayPolicy)`, `Clear`, `GetAllCompleted`); impls
`InMemoryCompletionStore` (default, volatile), `SessionCompletionStore`, `CompositeCompletionStore`
(routes OnceAccount→persistent / OnceSession→in-memory / Repeatable→dropped) — all in `Core` — and
`UserPrefsCompletionStore` (Runtime, the persistent tier via `PFound.UserPrefs`).

**Authoring ScriptableObjects (`PFound.GuidedOnboardingFlow.Authoring`)** — `TutorialCatalog`
(with a serialized run-whitelist + `LogVerbosity`), `TutorialDefinition` (replay policy, AND-trigger
list, per-tutorial default timeout outcome), abstract `TutorialStepAuthoring` (per-step timeout +
`Advance`/`AbortRun`/use-default outcome; concretes `LogStepAuthoring` (+severity),
`FocusStepAuthoring` (+hint/mask), `DialogStepAuthoring` (+typewriter/dismiss)), abstract
`TutorialTriggerAuthoring` (+ `StartupTriggerAuthoring`, `DelayedStartupTriggerAuthoring`).

**UI seams** — interfaces `IInputBlocker`, `ITutorialHand`, `IHighlightMask`, `IDialogPanel`
(+ `DialogDismissMode`), `ITutorialCanvas`, `ITutorialAnchorRegistry` (+ `TutorialAnchorHandle`);
MonoBehaviour impls `InputBlocker` (re-executes press/release/click on the target + children),
`TutorialHand` (hint + per-anchor offset), `HighlightMask` (sprite mask + padding), `DialogPanel`
(typewriter + tap/auto/both dismiss), `TutorialCanvas`, `SceneAnchor` (offset/padding/mask);
and pure `TutorialAnchorRegistry` (multi-anchor-per-tag + `ActiveCount`).

**Composition** — `TutorialInstaller`, `TutorialRunnerHost`, `TutorialRuntimeServices`.

## Model

- **One active run.** `TutorialManager` owns a single `TutorialRunner`. On idle `Tick`, it polls
  `Automatic` blueprints in registration order; a blueprint's triggers combine with **AND** (every
  one must fire on the same poll). The first eligible one starts — eligible = not already completed
  (unless `Replay == Repeatable`) and whitelisted if a run-whitelist is set. When several become
  eligible on the same tick the first-registered one wins (deterministic tie-break). On end, the
  run's outcome is recorded per its **replay policy** (`OnceAccount` persists, `OnceSession` holds
  for the session, `Repeatable` is never recorded) and its **triggers are re-armed** (`Reset()`) so a
  session/repeatable tutorial can fire again. `Completed` **and player `Skip`** both mark the tutorial
  seen. `TryStart` respects completion (a finished non-repeatable tutorial won't re-run — use
  `ForceStart`); the manager also emits `StepChanged(id, index)` as the active run moves between steps
  and can log lifecycle lines through an `ITutorialLog` gated by `LogVerbosity`. At construction it
  validates the blueprint set, warning on duplicate or empty ids.
- **Runner lifecycle.** Constructed `Pending`; `Begin()` moves it to `Active`; it settles in `Ended`
  with a `TutorialOutcome`. Each tick it confirms preconditions still hold, gates the current step on
  its readiness check, then advances it, promoting to the next step on a normal finish. Zero-duration
  steps chain within the same tick (bounded by step count as a safety net).
- **Step readiness** (`ITutorialStep.CheckReadiness`): `Ready` proceeds; `Deferred` holds the run
  (wait for a target to appear); `Unreachable` ends it as `Invalidated`; `SkipAndAdvance` drops the
  step and runs the next; `GoBackOne` rewinds one step and re-runs it.
- **Step timeout.** A step's optional `Timeout` cancels it when spent; the run then either advances
  or aborts as `Invalidated` per the step's `TimeoutOutcome` (falling back to the tutorial's
  `DefaultTimeoutOutcome`).
- **Auto-advance / auto-click.** `SetAutoAdvance(enabled, perStepDelaySeconds)` fast-forwards
  interactive steps: after the per-step delay each self-completes, and `FocusStep` additionally
  synthesizes a **real pointer click** on its target so the game's own handler fires.
- **Steps** implement `ITutorialStep` (`CheckReadiness` → `Begin` → `Advance(dt)` →
  `Complete`/`Cancel`), usually via `TutorialStepBase` (adds timeout + fault bookkeeping).

## Public API

**Manager (`ITutorialManager` / `TutorialManager`)**

```csharp
TutorialRunner Active { get; }          // live runner, or null when idle
bool IsRunning { get; }
bool AutoAdvance { get; set; }          // interactive steps self-complete when true (delay 0)
float AutoAdvanceDelaySeconds { get; }  // per-step delay under auto-advance

event Action<TutorialId> TutorialStarted;
event Action<TutorialId, TutorialOutcome> TutorialEnded;
event Action<ITutorialStep> StepStarted;
event Action<TutorialId, int> StepChanged;          // (id, stepIndex) as the run advances

bool TryStart(TutorialId id);           // false if already running OR already completed (non-repeatable)
void ForceStart(TutorialId id);         // aborts the current run first; ignores completion
void Skip();                            // player-cancel the active run (marks it seen)
void SetRunSpecific(IEnumerable<TutorialId> ids);   // whitelist; empty/null clears it
void SetAutoAdvance(bool enabled, float perStepDelaySeconds);
void Tick(float deltaSeconds);          // advance active run; poll automatic triggers when idle
// ctor: new TutorialManager(blueprints, store, ITutorialLog log = null, LogVerbosity = Errors)
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
new LogStep(ITutorialLog log, string message, LogSeverity severity = Info);
new DelayStep(float seconds);

// Unity:
new DialogStep(services, message,
    float typingSpeedCps = 0, DialogDismissMode dismissMode = Tap,
    float? autoDismissSeconds = null, float? timeout = null, StepTimeoutOutcome? timeoutOutcome = null);
new FocusStep(services, anchorTag,
    string hint = null, string maskSpriteName = null,
    float? timeout = null, StepTimeoutOutcome? timeoutOutcome = null);   // anchor → GameObject.Find fallback
OpenScreenStep.For<TScreen>(ScreenRouter router, float landingTimeoutSeconds = 5, float? timeout = null);
new WaitForSignalStep<TSignal>(SignalTracker signals, services = null, float? timeout = null);
```

**Triggers** — `bool ShouldFire(in TriggerContext context)` + `void Reset()`; base
`TutorialTriggerBase`; built-in `StartupTrigger`, `DelayedStartupTrigger`. A blueprint's `Triggers`
list is AND-combined; `ResetTriggers()` re-arms them (the manager calls it on run end).

**Completion stores (`ITutorialCompletionStore`)**

```csharp
Task HydrateAsync(CancellationToken ct = default);
bool IsCompleted(TutorialId id);
void MarkCompleted(TutorialId id, ReplayPolicy mode);   // Repeatable = no-op
void Clear(TutorialId id);
IReadOnlyCollection<TutorialId> GetAllCompleted();
// CompositeCompletionStore(persistent) → OnceAccount→persistent, OnceSession→in-memory, Repeatable→dropped.
// UserPrefsCompletionStore(IPrefsStore store) is the persistent tier; PrefKey<string> CompletedKey ("gof.completed").
```

**Authoring** — `TutorialCatalog` (create-asset `PFound/Guided Onboarding/Catalog`) holds a list of
`TutorialDefinition` (a `TutorialId` handle, `TriggerMode`, an AND-combined trigger-asset list, a
`ReplayPolicy`, a per-tutorial default timeout outcome, and an ordered `TutorialStepAuthoring` list),
plus a serialized **run-whitelist** and a `LogVerbosity` — the installer applies both.
`_catalog.BuildBlueprints(services)` resolves the definitions. Each `TutorialStepAuthoring` carries a
per-step timeout + `Advance`/`AbortRun`/use-default outcome and exposes
`ITutorialStep CreateStep(TutorialRuntimeServices)`; `TutorialTriggerAuthoring` concretes expose
`ITutorialTrigger CreateTrigger()`. `OpenScreen`/`WaitForSignal` steps are code-only (no authoring asset).

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
params to null — omit them if a project doesn't use screen or signal steps. For persistence with a
session tier, pass `store: new CompositeCompletionStore(new UserPrefsCompletionStore(prefsStore))`
(await its `HydrateAsync` first). The installer also applies the catalog's serialized run-whitelist
and `LogVerbosity`. See `Sample/SampleOnboardingBootstrap.cs` for the minimal container + install +
event wiring (with manual `T`/`S`/`A` keys to start/skip/toggle auto-advance).

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

`Core.Tests` is a standalone csc/mono runner (engine-free) — **38 tests** covering the runner
(readiness verdicts incl. skip/rewind, timeout outcomes + per-tutorial default), the manager
(multi-trigger AND, trigger re-arm, replay policies, completion-respecting start, `StepChanged`,
dup/empty-id validation), persistence (in-memory/session/composite tiers, `GetAllCompleted`), and
step semantics. `Tests.EditAndPlayModes` covers the Unity layer (step + UI layers). The
MonoBehaviour/overlay wiring, the InputBlocker click pass-through, the sprite-shaped spotlight, the
dialog typewriter, and the auto-click pointer synthesis are verified in the editor on integration.

## Limitations / Known Gaps

- **One active run at a time** by design — there is no concurrent-tutorial support.
- **The consumer supplies the overlay objects.** The module never spawns the `Canvas` / overlay
  MonoBehaviours or the tick host; unwired inspector slots surface as fail-fast errors, not silent
  no-ops (nothing here is expected to be null).
- **The manager does not self-tick** — without a bound `TutorialRunnerHost` (or an equivalent caller
  of `Tick`) nothing advances.
- **Mid-step target loss ends the run as `Faulted`.** A `FocusStep` whose target is destroyed while
  active tears down its own overlay and aborts; the tutorial is not marked complete, so it stays
  eligible to retry (PFound maps this to `Faulted` rather than a distinct "conditions" outcome).
- `OpenScreenStep` / `WaitForSignalStep<T>` are code-only (no authoring asset) and inert unless the
  matching `router` / `signals` collaborator is passed to `Install`. `OpenScreenStep` readiness is
  `Unreachable` when no router is wired.
</content>
