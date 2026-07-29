# PFound.HubApp

> **Module group — App Shell.** Single-module group. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

Hub-app shell services for a multi-mini-game front-end: save, profiles, badges, stickers, photo
album, audio, parent-gate, analytics — plus a `MiniGameHost` that loads, scopes, and tears down one
mini-game at a time. Every service is a plain C# class with constructor injection; there is no
container, no singleton, and no host MonoBehaviour — the consuming app owns construction and lifetime.

## Scope boundary — the save subsystem vs other data modules

HubApp's `SaveService` persists ONE per-profile / per-minigame **structured document** (`save.json`:
profiles, badges, stickers, photos, settings, and per-game blobs), with `ScopedSaveService` giving
each mini-game an isolated `games.{profile}.{game}.*` bag. It is distinct from:
- **`PFound.UserPrefs`** — flat, pre-declared **typed settings keys** (device-local); a global bag of
  declared keys, not a multi-profile document.
- **`PFound.GameDataStore`** — **in-memory** runtime data model, no persistence.
- **`PFound.PlayerDataSync`** — authoritative player state synced to a **backend/cloud**; HubApp-save
  is device-local only.

> Note: the save subsystem currently reimplements local persistence (its own Newtonsoft atomic-write +
> migration) rather than building on UserPrefs. Planned direction is to unify onto an enhanced
> UserPrefs (see TODO.md); the per-profile/scoped structure stays HubApp-owned regardless.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.HubApp` | `Runtime/PFound.HubApp.asmdef` | Runtime: Save + Services + MiniGame. `autoReferenced: true`. |
| `PFound.HubApp.Editor` | `Editor/PFound.HubApp.Editor.asmdef` | Editor-only: `IGameSpecificAssetProvider` impl. |
| `PFound.HubApp.Tests` | `Tests/PFound.HubApp.Tests.asmdef` | Edit-mode tests; `autoReferenced: false`, gated on `UNITY_INCLUDE_TESTS`. |

## Dependencies

Runtime (`PFound.HubApp`) references:

- `PFound.Signaling` — the shared `SignalTracker` event bus.
- `PFound.Utilities.FileSystemTools` — `FileTools.AtomicWriteAllText` / `SafeReadAllText` for crash-safe save IO.
- `PFound.ContentDelivery` — `AssetLoader`, used by `PrefabMiniGameLoader` to load mini-game prefabs.
- `Unity.Collections` — `FixedString64Bytes` (loader id).
- `UniTask` — async ready-gating (`IMiniGameModuleAsyncReady`).

Editor (`PFound.HubApp.Editor`) references `PFound.HubApp` + `PFound.Utilities.EditorHelpers`
(`IGameSpecificAssetProvider`). No DI container, no `ServiceRegistry`, no navigation/localization
module dependency — services are plain objects the consumer wires by hand.

## Key Types

**Save (`PFound.HubApp.Save`)**
- `ISaveService` / `SaveService` — root JSON persistence. `Schema` is **null until `Load()`**.
- `SaveSchema` — root POCO (`Profile` / `shared` / `games` / `settings` namespaces + `SchemaVersion`).
- `MigrationPipeline` — version-to-version migration chain applied on `Load()`.

**Services (`PFound.HubApp.Services.*`, interface → impl)**
- `IProfileService` → `ProfileService` — multi-profile CRUD; exposes `ActiveProfileId`.
- `IBadgeService` → `BadgeService`, `IStickerService` → `StickerService` — persist earned items to `shared`.
- `IPhotoAlbumService` → `PhotoAlbumService` — photo capture + FIFO-capped album on disk.
- `IAudioService` → `AudioService` — `AudioMixer` per-bus volume + snapshot ducking. `AudioBus { Master, Music, SFX, Voice, UI }`.
- `IParentGateService` → `ParentGateService` — pure math-challenge model, issues `ParentGateChallenge`.
- `AnalyticsService` — facade over `IAnalyticsProvider` → `NullProvider` (default no-op).

**Consumer seams (interface only, no impl ships)**
- `IContentPackEntitlement` (`PFound.HubApp.Entitlement`) — `IsEntitled(packId)`, the IAP / content-pack gate.
- `ITutorialBootstrap` (`PFound.HubApp.Tutorial`) — `Boot(miniGameId)`.
- `IMiniGameModule` / `IMiniGameModuleAsyncReady` — implemented on each mini-game's prefab root.
- `IAnalyticsProvider` — your analytics backend (or ship `NullProvider`).

**Mini-game (`PFound.HubApp.MiniGame`)**
- `MiniGameHost` — single-occupant lifecycle orchestrator (plain class).
- `IMiniGameModuleLoader` → `PrefabMiniGameLoader` — loads the prefab over `PFound.ContentDelivery` `AssetLoader`.
- `MiniGameContext` (readonly struct) — service bundle handed to the module.
- `IScopedSaveService` / `ScopedSaveService` — save writes auto-namespaced to `games.{profileId}.{gameId}`.
- `MiniGameDefinition` — authored `ScriptableObject` (`GameId`, `ModuleAddress`, `Unlocked`).
- `MiniGameSceneContext` — optional MonoBehaviour on the scene content root, surfaces scene-level refs (e.g. `Camera`).
- Signals (`PFound.HubApp.MiniGame.Signals`, all `: SignalBase`) — see below.

## Public API

**`SaveService`** (`PFound.HubApp.Save`)
- `SaveService(string saveRoot, MigrationPipeline migrations = null)`
- `static string DefaultSaveRoot` (= `Application.persistentDataPath`), `const string SaveFileName = "save.json"`, `string SavePath`
- `void Load()` — **must be called before any other service touches `Schema`**; brings up a fresh
  `SaveSchema` on first launch, applies migrations, propagates malformed-JSON exceptions.
- `void Save()` / `void Flush()` — atomic write; `void Reset()` — fresh in-memory schema.
- `SaveSchema Schema { get; }` — **null until `Load()`** (deliberate fail-fast).

**Services** — construct with `new`, dependencies via ctor:
- `ProfileService(ISaveService save, SignalTracker signals)`
- `BadgeService(ISaveService save, SignalTracker signals)`
- `StickerService(ISaveService save, SignalTracker signals)`
- `PhotoAlbumService(ISaveService save, SignalTracker signals, string photosRoot, int maxPhotos = 200)` — `static string DefaultPhotosRoot`
- `AudioService(ISaveService save, AudioMixer mixer, AudioMixerSnapshot defaultSnapshot, AudioMixerSnapshot duckingSnapshot)`
- `ParentGateService(int? seed = null)`
- `AnalyticsService(ISaveService save, IAnalyticsProvider provider)`

**`MiniGameHost`** (`PFound.HubApp.MiniGame`)
- `MiniGameHost(IMiniGameModuleLoader loader, ISaveService save, IProfileService profile, IBadgeService badges, IStickerService stickers, IPhotoAlbumService photoAlbum, SignalTracker signals)`
- `void Load(MiniGameDefinition definition, string moduleAddressOverride = null, Transform parent = null, Action onReady = null, MiniGameSceneContext sceneContext = null)`
- `void Pause()`, `void Resume()`, `void RequestExit()`, `void UnloadActive()`
- `IMiniGameModule ActiveModule`, `MiniGameDefinition ActiveDefinition`, `bool IsLoaded`, `bool IsLoading`
- Single-occupant: throws on a second `Load` while active/loading; requires a non-empty
  `IProfileService.ActiveProfileId`; `GameId` on the definition must match the module's `GameId`.

**`MiniGameContext`** (readonly struct, handed to `IMiniGameModule.Initialize`)
- `IScopedSaveService Save` (auto-namespaced `games.{profileId}.{gameId}`), `IProfileService Profile`,
  `IBadgeService Badges`, `IStickerService Stickers`, `IPhotoAlbumService PhotoAlbum`,
  `Action RequestExit`, `MiniGameSceneContext Scene` (optional, may be null).

**`IMiniGameModule`** (implemented by the consumer on the prefab root)
- `string GameId { get; }`, `void Initialize(MiniGameContext context)`, `void Pause()`, `void Resume()`, `void OnExit()`
- Optional `IMiniGameModuleAsyncReady.WaitUntilReadyAsync(CancellationToken ct)` — gates `onReady`
  until async content is loaded.

**Signals** (`PFound.HubApp.MiniGame.Signals`, all `: SignalBase`, payloadless): `MiniGameStartedSignal`,
`MiniGameCompletedSignal`, `MiniGamePausedSignal`, `MiniGameResumedSignal`, `ProfileSelectedSignal`,
`BadgePersistedSignal`, `StickerPersistedSignal`, `PhotoCapturedSignal`. Listeners re-query the service
for state; the signal carries no payload.

## Setup / wiring

**No installer, no scene host, no singleton, no `DontDestroyOnLoad` — the module ships none of these.**
The consuming project writes its own bootstrapper (in its own assembly) that constructs the services
and **holds the references for the app lifetime** (e.g. on its own persistent boot object — HubApp
does not persist anything for you). Recommended shape:

```csharp
var signals = new SignalTracker();                       // PFound.Signaling — shared bus

var save = new SaveService(SaveService.DefaultSaveRoot); // persistentDataPath/save.json
save.Load();                                             // MUST call before any other service

var profile    = new ProfileService(save, signals);
var badges     = new BadgeService(save, signals);
var stickers   = new StickerService(save, signals);
var photoAlbum = new PhotoAlbumService(save, signals, PhotoAlbumService.DefaultPhotosRoot);
var audio      = new AudioService(save, mixer, defaultSnapshot, duckingSnapshot);
var parentGate = new ParentGateService();
var analytics  = new AnalyticsService(save, new NullProvider());   // swap in your IAnalyticsProvider

var loader = new PrefabMiniGameLoader();
var host   = new MiniGameHost(loader, save, profile, badges, stickers, photoAlbum, signals);
// keep host + services alive for the whole app; flush save yourself on pause/settings-close.
```

**Placement / lifetime is the consumer's decision.** Hold these on your own bootstrapper object; if
the hub must survive scene loads, mark *that* object `DontDestroyOnLoad` — nothing here does it. The
services are scene-agnostic plain objects. Services never auto-flush — the app calls `save.Flush()` /
`save.Save()` at pause / settings-close. The `AudioService` needs an `AudioMixer` you author (bus
volumes via exposed params `MusicVolume` / `SFXVolume` / `VoiceVolume`, plus default/ducking snapshots).

**Consumer-authored assets:**
- One `MiniGameDefinition` ScriptableObject per mini-game (Create Asset menu). Its `GameId` **must
  equal** the mini-game's `IMiniGameModule.GameId`.
- Each mini-game's **root prefab** registered in `PFound.ContentDelivery` at the definition's
  `ModuleAddress`, containing exactly one `IMiniGameModule` component.
- The `AudioMixer` + its default/ducking `AudioMixerSnapshot`s.
- Impls of `IContentPackEntitlement` (and optionally `IAnalyticsProvider`, `ITutorialBootstrap`) —
  none ship in this module.

**Mini-game lifecycle:** `host.Load(definition)` validates (unlocked, single-occupant, active
profile), async-loads the prefab via the loader, resolves the `IMiniGameModule`, checks its `GameId`,
builds a `ScopedSaveService`, calls `module.Initialize(context)`, and queues `MiniGameStartedSignal`
(awaiting `IMiniGameModuleAsyncReady.WaitUntilReadyAsync` before `onReady` if implemented). The
module calls `context.RequestExit()`; the host queues `MiniGameCompletedSignal`, calls
`module.OnExit()`, unloads, and runs `Resources.UnloadUnusedAssets()` + `GC.Collect()`.

> The editor `HubAppAssetProvider` (an `IGameSpecificAssetProvider`) currently registers zero assets
> — author all GameSpecific assets manually.

## File Structure

```
HubApp/
├── Runtime/
│   ├── PFound.HubApp.asmdef
│   ├── Save/            ISaveService, SaveService, SaveSchema, MigrationPipeline
│   ├── Services/
│   │   ├── Profile/     IProfileService, ProfileService
│   │   ├── Badges/      IBadgeService, BadgeService
│   │   ├── Stickers/    IStickerService, StickerService
│   │   ├── PhotoAlbum/  IPhotoAlbumService, PhotoAlbumService
│   │   ├── Audio/       IAudioService, AudioService, AudioBus
│   │   ├── ParentGate/  IParentGateService, ParentGateService, ParentGateChallenge
│   │   └── Analytics/   AnalyticsService, IAnalyticsProvider, NullProvider
│   ├── MiniGame/        MiniGameHost, IMiniGameModule(+AsyncReady), IMiniGameModuleLoader,
│   │   │                PrefabMiniGameLoader, MiniGameContext, IScopedSaveService,
│   │   │                ScopedSaveService, MiniGameDefinition, MiniGameSceneContext
│   │   └── Signals/     MiniGame{Started,Completed,Paused,Resumed}, Profile/Badge/Sticker/Photo
│   ├── Entitlement/     IContentPackEntitlement
│   └── Tutorial/        ITutorialBootstrap
├── Editor/
│   ├── PFound.HubApp.Editor.asmdef
│   └── Providers/       HubAppAssetProvider (IGameSpecificAssetProvider — registers nothing yet)
└── Tests/               PFound.HubApp.Tests.asmdef + service/host/save edit-mode tests
```

## Downstream Dependents

None within PFound — this is a top-of-stack app-shell module. Consumers are the hub game projects
(e.g. Playnest) that write their own bootstrapper and mini-game modules against these seams.

## Limitations / Known Gaps

- `HubAppAssetProvider` registers zero assets; the `AudioMixer` and any GameSpecific assets are
  authored by hand (the mixer is not a `ScriptableObject`, so it cannot be auto-created via the
  provider seam).
- No IAP/content-pack impl ships — `IContentPackEntitlement` is a bare seam the consumer implements.
- `AnalyticsService` defaults to `NullProvider` (no-op); a real `IAnalyticsProvider` is consumer-supplied.
- Save is single-file JSON at `{saveRoot}/save.json`; no encryption or cloud sync.
</content>
</invoke>
