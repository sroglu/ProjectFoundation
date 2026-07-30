# PFound.StartupOrchestration

A weighted, multi-step async app-boot pipeline. Register self-contained `IStartupStep`s; the
orchestrator runs them **all concurrently** and reports one aggregated `StartupAggregate` (overall
`0..1` + a dominant "what are we waiting on" reason) per tick, so a loading screen binds straight to
it. Completes when every step finishes — success or fail-soft.

## Quick reference

```csharp
var boot = new StartupOrchestrator();
boot.Register(new CatalogStep());
boot.Register(new TranslationsStep());

var progress = new Progress<StartupAggregate>(agg =>
{
    loadingScreen.SetProgress(agg.Overall01);
    loadingScreen.SetStatus(agg.DominantReason);   // -> loading.{reason} localization key
});

await boot.RunAsync(progress, appQuitToken);       // returns when all steps finish
```

## Designer-authored boot set

Prefer a designer to pick which steps boot? Author a **`BootStepManifest`** asset
(`Create ▸ PFound ▸ Startup Orchestration ▸ Boot Step Manifest`) — an on/off list of steps stored via
`[SerializeReference]` (one asset for the whole set) — then register it before running:

```csharp
var boot = new StartupOrchestrator();
manifest.RegisterInto(boot);                       // registers every enabled, non-empty step
await boot.RunAsync(progress, appQuitToken);
```

The manifest is the **set** of steps to run, not an ordered plan (steps run in parallel); ordered,
dependency-aware init is the DI container's job, not this boot runner's. `BootManifestRegistrar` is the
engine-free core (registers any `IEnumerable<IStartupStep>`), unit-tested with plain C#.

## Dependencies

Engine only (`Debug` / `Mathf`) + BCL `Task`. Container-agnostic. `autoReferenced:false`.

## Docs

Deep reference: [MODULE.md](MODULE.md) — the phase/step model (concurrent run, weighted monotonic
bar, dominant reason, fail-soft), full API, and pure-library `new StartupOrchestrator()` wiring.
