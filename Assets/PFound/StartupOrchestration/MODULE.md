# StartupOrchestration

## Purpose
A weighted, multi-step async app-boot pipeline. You register self-contained `IStartupStep`s; the
orchestrator runs them **all concurrently** and reports a single aggregated `StartupAggregate`
(overall `0..1` + a dominant "what are we waiting on" reason) per tick, so a loading screen can bind
straight to it. Returns when every step finishes — success or fail-soft.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.StartupOrchestration` | `Runtime/PFound.StartupOrchestration.asmdef` | `autoReferenced: false`; references the engine for `Debug.LogError` / `Mathf.Clamp01` and the boot-manifest `ScriptableObject` |

## Dependencies
- Engine only: `UnityEngine.Debug` (boundary error logging) and `UnityEngine.Mathf` (progress
  clamping).
- BCL async: `System.Threading.Tasks.Task`, `IProgress<T>`, `CancellationToken`.
- No other PFound module, no third-party package, no scripting define. Deliberately independent of
  any DI container.

## Key Types

### `PFound.StartupOrchestration`
- **`StartupOrchestrator`** (sealed) — runs registered steps concurrently, aggregates progress,
  completes when all finish. Container-agnostic; holds no static state.
- **`IStartupStep`** — one unit of parallel boot prep (`Reason`, `Weight`, `ExecuteAsync`).
- **`StartupAggregate`** (readonly struct) — combined per-tick view: `Overall01`, `DominantReason`,
  `PerStep`.
- **`StartupStatus`** (readonly struct) — one step's instantaneous report (`Reason`, `Progress01`).
- **`WaitReason`** (enum) — categorized, localizable "what are we waiting on" label.
- **`BootStepManifest`** (`ScriptableObject`) — a designer-authored boot **set**: an asset a designer
  fills with the steps to run at start, each with an on/off toggle. Steps are stored with
  `[SerializeReference]`, so one asset holds the whole set (pick a concrete `IStartupStep` type and set
  its fields inline).
- **`BootStepEntry`** (`[Serializable]`) — one manifest row: `Enabled` + the chosen `IStartupStep`.
- **`BootManifestRegistrar`** (static, **engine-free**) — reads any step sequence (a manifest, a
  hard-coded list, a test) and registers each into a `StartupOrchestrator`; skips empty entries.

## Public API

`StartupOrchestrator`:
```csharp
void Register(IStartupStep step);   // idempotent on instance identity; throws on null
Task RunAsync(IProgress<StartupAggregate> progress, CancellationToken ct);  // runs all in parallel; completes when all finish
```

`IStartupStep`:
```csharp
WaitReason Reason { get; }
float Weight => 1f;                  // relative bar share; 0 = participate without moving the bar
Task ExecuteAsync(IProgress<float> progress, CancellationToken ct);   // report 0..1 for THIS step only
```

`StartupAggregate`:
```csharp
float Overall01;                    // smoothed, monotonic non-decreasing
WaitReason DominantReason;          // step furthest from done (ties -> most recent report)
IReadOnlyList<(string source, WaitReason reason, float progress)> PerStep;
```

`StartupStatus`: `WaitReason Reason`, `float Progress01` (clamped 0..1).

`WaitReason`: `Catalog`, `Translations`, `Save`, `RemoteConfig`, `BundlesVerify`, `Systems`,
`ShaderWarmup`, `Scene` (value `0` reserved for invalid/unset).

`BootStepManifest` (`ScriptableObject`):
```csharp
IReadOnlyList<BootStepEntry> Steps { get; }        // all authored rows, including turned-off ones
IEnumerable<IStartupStep> EnabledSteps();          // turned-on, non-empty steps only
int RegisterInto(StartupOrchestrator orchestrator);// registers EnabledSteps; returns count added
```

`BootStepEntry` (`[Serializable]`): `bool Enabled`, `IStartupStep Step` (`Step` may be `null` for an
empty inspector row).

`BootManifestRegistrar` (static, engine-free):
```csharp
int RegisterAll(StartupOrchestrator orchestrator, IEnumerable<IStartupStep> steps); // skips nulls; returns count added
```

## Architecture — phase/step model

- **Steps are independent and dependency-free.** An `IStartupStep` does one unit of parallel boot
  prep and reports its own `0..1`. It MUST NOT do DI lookups or assume another step ran (order is
  undefined; there is no concurrency cap in v1). Work that needs a built service belongs in that
  service's own init, not here.
- **Concurrent run, single aggregate.** `RunAsync` launches every step as a `Task` and awaits
  `Task.WhenAll`. A `RunState` (per-run, so one orchestrator instance can run more than once) collects
  each step's latest progress behind a lock and emits a fresh `StartupAggregate` on every report.
- **Weighted, monotonic bar.** `Overall01` is the weighted mean of per-step progress
  (`IStartupStep.Weight`, default `1`; `0` lets a step run without moving the bar) clamped
  non-decreasing — so a long download can dominate the bar and the bar never goes backwards. Negative
  weights are floored to `0`; an all-zero weight set falls back to equal weight so the bar still
  moves.
- **Dominant reason.** `DominantReason` is the step furthest from completion (lowest progress; ties
  broken by most-recent report), so the status line names whatever is actually holding boot up.
- **Fail-soft, never hang.** A step MUST NOT throw on a user-facing failure (offline, fetch fail): it
  reports `progress.Report(1f)` and completes; its owner applies the fallback. A step that *does*
  throw is caught at the boundary, logged via `Debug.LogError`, and counted as completed anyway —
  a safety net, not the intended path. `OperationCanceledException` propagates (app quit stops the
  boot).
- **Reasons drive localized status text.** `WaitReason` maps to a localization key
  `loading.{enum-name}` (PascalCase verbatim); a missing key shows the raw enum name so the gap is
  visible in dev. Adding a `WaitReason` value requires a new localization row.

## Setup / wiring

**Pure library — `new StartupOrchestrator()`, no scene object, no host MonoBehaviour, no singleton,
no DI registration.** The orchestrator is a plain C# type you own and drive from your own boot code;
it deliberately holds no static state and knows nothing about a container.

Recommended shape: from your boot entry point, `new StartupOrchestrator()`, `Register(...)` each
step, then `await RunAsync(progress, ct)` where `progress` is an `IProgress<StartupAggregate>` that
updates your loading screen and `ct` is tied to your app-quit token. When the task completes, tear
down the loading screen and continue boot.

```csharp
var boot = new StartupOrchestrator();
boot.Register(new CatalogStep());
boot.Register(new TranslationsStep());
boot.Register(new RemoteConfigStep());

var progress = new Progress<StartupAggregate>(agg =>
{
    loadingScreen.SetProgress(agg.Overall01);
    loadingScreen.SetStatus(agg.DominantReason);   // -> loading.{reason} localization key
});

await boot.RunAsync(progress, appQuitToken);
// all steps done (success or fail-soft) — proceed.
```

Because it is container-agnostic, the *steps* capture whatever they need at construction time (you
`new` them, so pass their inputs in) — the orchestrator never resolves anything. Nothing here
persists across scenes; the instance lives exactly as long as your boot flow. `ExecuteAsync` returns
a plain `Task` (no third-party async dep); UniTask-native owners adapt at the boundary (`.AsTask()`).

## Designer-authored boot manifest

For teams that want a designer — not code — to choose which steps boot, author a **`BootStepManifest`**
asset (`Create ▸ PFound ▸ Startup Orchestration ▸ Boot Step Manifest`). It holds an ordered list of
`BootStepEntry` rows; each row has an on/off toggle and a `[SerializeReference]` `IStartupStep` picker,
so one asset holds the whole boot set and a step can be kept-but-skipped without deleting it.

At boot, hand the manifest to a fresh orchestrator, then run:

```csharp
var boot = new StartupOrchestrator();
manifest.RegisterInto(boot);          // registers every turned-on, non-empty step
await boot.RunAsync(progress, appQuitToken);
```

`BootManifestRegistrar.RegisterAll(orchestrator, steps)` is the engine-free core underneath — it takes
any `IEnumerable<IStartupStep>` (a manifest, a hard-coded list, a test double), registers each, and
skips empty rows, so the same logic is unit-testable with plain C# and reusable outside the SO path.

**The manifest is the SET of steps to run, not an ordered plan.** The orchestrator runs every
registered step in parallel, so the list order in the asset does not decide run order — it only groups
the boot set into one authorable place. A step that a designer picks must be a `[Serializable]` type
with a no-argument constructor (so the inspector can create it); a step that needs constructor inputs
is wired in code instead.

### On execution-order phases — covered, not added

An authored boot set can, in principle, come paired with an *ordered*, dependency-aware init (an
execution-order phase enum + a topological sort by declared dependencies). In this framework that
ordering concern lives in the **DI container**, which resolves and initializes services in dependency
order. This boot runner is deliberately the parallel, dependency-free half: independent steps + a
weighted progress bar + a dominant "what are we waiting on" reason. So no phase enum is introduced
here — a phase/ordering need is served by the DI container's ordered init, and cross-step ordering
inside a single boot run is explicitly out of scope (see "Limitations / Known Gaps"). The manifest
closes the *authoring* side: letting a designer pick the boot set in an asset.

### Verify (engine-free)

```
csc -nologo -warn:0 -define:PF_STARTUP_TESTS -out:/tmp/pf_startup.exe \
    Assets/PFound/StartupOrchestration/Runtime/*.cs \
    Assets/PFound/StartupOrchestration/Tests/*.cs && mono /tmp/pf_startup.exe   # -> passed=6 failed=0
```

The `Tests/` runner ships a tiny stand-in for the few `UnityEngine` symbols the runtime touches (all
under the `PF_STARTUP_TESTS` define, which Unity never sets), so the manifest/registrar core runs with
no Unity or dotnet.

## File Structure
```
StartupOrchestration/
  README.md
  MODULE.md
  Runtime/
    PFound.StartupOrchestration.asmdef
    StartupOrchestrator.cs   # orchestrator + per-run RunState + StepProgress
    IStartupStep.cs          # the step contract
    StartupAggregate.cs      # combined per-tick view
    StartupStatus.cs         # single-step instantaneous report
    WaitReason.cs            # categorized wait-reason enum
    BootStepManifest.cs      # designer-authored boot SET (ScriptableObject)
    BootStepEntry.cs         # one manifest row: Enabled + IStartupStep ([SerializeReference])
    BootManifestRegistrar.cs # engine-free: registers a step sequence into an orchestrator
  Tests/
    BootManifestTests.cs     # standalone mono/csc runner (PF_STARTUP_TESTS-gated)
```

## Downstream Dependents
None within PFound — it is a standalone boot library consumed by an app's own boot flow.

## Limitations / Known Gaps
- **No concurrency cap (v1).** Every registered step starts at once; a boot with many heavy steps
  competes for bandwidth/CPU. Batch or throttle inside your steps if needed.
- **No deadline (v1).** The orchestrator never times a step out; a step that needs a limit enforces
  its own and reports success on expiry.
- **Steps must be truly independent.** Order is undefined and there is no dependency graph — cross-step
  ordering or shared prerequisites are unsupported by design.
- **Thrown exceptions are swallowed as "completed."** The boundary catch keeps boot from hanging but
  is a safety net, not the failure path — steps are expected to fail-soft to `1f` themselves.
- **New `WaitReason` values need a localization row.** Otherwise the status line shows the raw enum
  name.
