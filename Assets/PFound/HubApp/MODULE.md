# HubApp

## Purpose

Project-specific layer that bridges reusable PFound framework modules (ServiceRegistry, Signaling, Navigation, etc.) with a hub-style app's cross-cutting concerns: profile management, save persistence, badge/sticker/photo services, audio mixing, parent gate, IAP, analytics, and a mini-game module contract with lifecycle orchestration.

Designed for **Playnest** (Toddler Games Hub App) but structured so any kid-safe hub-style game can consume it. Game-specific content (badge definitions, mini-game implementations, shell UI) lives in the consuming project, not in this submodule.

## Layer

**Integration** — depends on multiple framework submodules:

- `PFound.DependencyContainer` (DI)
- `PFound.Signaling` (event bus)
- `PFound.ScreenRouter` (in-scene UI navigation)
- `PFound.LocalizationService` (strings, EN + TR baseline)
- `PFound.Utilities` + `PFound.Utilities.FileSystemTools` (atomic write for save)
- `PFound.EpochClock` (daily reset timestamps)
- `PFound.Utilities.EditorHelpers` (editor-side — `IGameSpecificAssetProvider`)

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.HubApp` | `Runtime/PFound.HubApp.asmdef` | Runtime: Save + Services + MiniGame |
| `PFound.HubApp.Editor` | `Editor/PFound.HubApp.Editor.asmdef` | Editor-side: `IGameSpecificAssetProvider` impls, inspectors |

## Planned Scope (Faz 1 of Playnest implementation)

### `Runtime/Save/`
- `SaveSchema.cs` — root POCO with `profile` / `shared` / `games` / `settings` namespaces
- `ISaveService` + `SaveService` — load/save/flush, atomic write via `FileTools.AtomicWriteAllText`, `.bak` fallback, schema version + migration pipeline
- `IMigration` + `MigrationPipeline` — topological migration chain

### `Runtime/Services/`
- `ProfileService` — multi-profile CRUD
- `BadgeService` / `StickerService` — subscribe to `BadgeEarnedSignal` / `StickerEarnedSignal`, persist to `shared`, emit `*PersistedSignal` for toast UI
- `PhotoAlbumService` — JPEG encode, thumbnail cache, FIFO eviction (200 photo cap)
- `AudioService` — `AudioMixer` binding + voice ducking snapshot (-6 dB music on Voice play)
- `ParentGateService` — math-challenge contract (UI in consuming project's Shell)
- `IAPService` — Unity IAP wrapper + `ProductCatalog` SO
- `AnalyticsService` — pluggable `IAnalyticsProvider` (`GameAnalyticsProvider`, `NullProvider`); COPPA-safe PII filtering at facade level

### `Runtime/MiniGame/`
- `IMiniGameModule` interface (`GameId`, `Initialize(ctx)`, `Pause`, `Resume`, `OnExit`)
- `MiniGameContext` struct (scoped services + `RequestExit` callback + optional `Scene`)
- `MiniGameSceneContext` MonoBehaviour — per-host-scene ref holder on the `MiniGameContentRoot`; exposes scene-level objects (e.g. `Camera`) the module can't serialize itself. The host reads it and surfaces it on `MiniGameContext.Scene` so modules reach scene refs without `Find`/`Camera.main`. Optional (null when the scene exposes none).
- `MiniGameHost` — additive scene load/unload orchestration, `ServiceRegistry.CreateScope()` lifecycle, `Resources.UnloadUnusedAssets()` + `GC.Collect()` on exit; `Load(..., sceneContext)` threads the scene context into the built `MiniGameContext`
- `ScopedSaveService` — auto-prefixes `games.{profileId}.{gameId}` so mini-games can only write their own namespace
- `MiniGameDefinition` SO — display metadata (name, icon, scene ref)

### `Editor/Providers/`
- `HubAppAssetProvider : IGameSpecificAssetProvider` — creates default `AudioMixer` asset, default `ProductCatalog.asset`, etc. at `Assets/GameSpecific/HubApp/`

## Quick Start (consuming project)

After adding as submodule (`git submodule add git@github.com:sroglu/HubApp.git Assets/PFound/HubApp`):

```csharp
// Bootstrap (Assets/Shell/Scripts/Bootstrapper.cs in consuming project)
var registry = new ServiceRegistry();

registry.Register<ISaveService, SaveService>();
registry.Register<IProfileService, ProfileService>();
registry.Register<IBadgeService, BadgeService>();
registry.Register<IStickerService, StickerService>();
registry.Register<IPhotoAlbumService, PhotoAlbumService>();
registry.Register<IAudioService, AudioService>();
registry.Register<IParentGateService, ParentGateService>();
registry.Register<IAPService, IAPService>();
registry.Register<AnalyticsService>()
    .WithFactory(p => new AnalyticsService(new GameAnalyticsProvider("your-key")));

registry.Build();

// Load save
var save = registry.Get<ISaveService>();
await save.LoadAsync();

// Route to ProfileSelect or Home based on active profile
```

A mini-game implements `IMiniGameModule`:

```csharp
public class TossyTossModule : MonoBehaviour, IMiniGameModule
{
    public string GameId => "tossytoss";

    public void Initialize(MiniGameContext ctx)
    {
        _save = ctx.Save;        // ScopedSaveService auto-namespaced to games.{profileId}.tossytoss
        _badges = ctx.Badges;    // shared across hub
        _audio = ctx.Audio;
        // ...
    }

    public void Pause() { /* ... */ }
    public void Resume() { /* ... */ }
    public void OnExit() { /* cleanup */ }
}
```

And `MiniGameHost` handles the rest (additive load, ServiceRegistry scope, GC on exit).

## Status

**Scaffold only (v0.0.0).** Runtime folders are empty placeholders — implementation happens during Faz 1 of the consuming project's bootstrap (typically 1-2 weeks solo-dev work to fill in Save + Services + MiniGame).

## License

Proprietary.
