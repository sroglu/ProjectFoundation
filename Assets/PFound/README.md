# PFound — Module Catalog

PFound is a monorepo of per-feature Unity modules. Each module lives under `Assets/PFound/<Module>/`
with its own asmdef(s) (`autoReferenced:false`) and a `MODULE.md`. Modules are grouped **by purpose**
below; grouping is conceptual — the actual dependency edges are listed under *Dependencies* and per
module. A module is separately usable wherever its dependencies allow.

## Groups

### LiveOps
Backend-driven live operations. Analytics is emitted to HubApp's `IAnalyticsProvider`.
- **RemoteGameConfig** — remote config values, feature flags, A/B experiments, keyed by a stable install id.
- **PlayerDataSync** — per-user authoritative game state synced to a backend (hybrid opaque blob).
- **Commerce** — IAP + cost/reward economy; purchases verified server-side, idempotent ledger.

### Content & Assets
- **ContentDelivery** — addressable AssetBundle + catalog delivery (local/CDN), runtime load by address.
- **AssetPipeline** — editor-time import-policy audit + sprite-atlas build over the same AssetGroups.
- **RemoteResourceCache** — tiered (memory→disk→remote) binary-resource cache with TTL + retry.
- **Compression** — engine-free compression codecs (LZMA).

### UI & Presentation
- **UISystem** — UI Toolkit component/theme library (Material-3-inspired, GPU-SDF).
- **ScreenRouter** — one active screen + modal-frame stack; guards, pooling, transitions, blur.
- **TweenPresetLibrary** — authored, data-driven tween presets.
- **GuidedOnboardingFlow** — step-driven tutorial/onboarding engine.
- **MVC** — lightweight per-screen MVC pattern. _Slated for retirement (see `TODO.md`; Toolbox has mvp+viewmanager)._

### Data & Persistence
- **GameDataStore** — in-memory runtime data model (`Entry` tagged union + typed singleton DBs). No persistence.
- **UserPrefs** — typed, schema-versioned local preferences store (PlayerPrefs + atomic JSON).

### App Foundation
Low-level primitives most modules build on.
- **DependencyContainer** — constructor-injection DI container (pure C#).
- **Signaling** — payload-free, deferred publish/subscribe signal bus.
- **LoopScheduler** — deterministic multi-phase scheduler injected into Unity's PlayerLoop.
- **StartupOrchestration** — weighted, multi-step async app-boot pipeline.
- **EpochClock** — Unix-epoch game clock + duration/period math.
- **Collections** — allocation-conscious data structures + best-first/A* pathfinding.
- **Utilities** — shared helper library (filesystem, datatype, network tools). Most-depended-on leaf.

### App Shell
- **HubApp** — multi-mini-game front-end shell: save, profiles, badges, stickers, photos, audio,
  parent-gate, analytics, plus a `MiniGameHost`.

### Gameplay / Simulation
- **ECS** — pure-C# sparse-set ECS runtime (World, struct components, queries, systems, events).

### Networking
- **NetworkLayer** — transport-agnostic realtime request/reply/notify messaging (Telepathy TCP).
- **ServerOperationFlow** — client-side server-authoritative operation lifecycle (predict → send → interpret → apply, single-flight, uniform failure) over the NetworkLayer transport seam. Core is engine- and transport-agnostic.

### Input
- **InputRouter** — backend-agnostic input-**intent** router (legacy Input Manager + Input System).

### Rendering
- **Render** — low-level rendering toolbox split into per-feature assemblies.

### Localization
- **LocalizationService** — key→text localization with a pluggable content source + editor pipeline.

### Dev Tools
- **KunaiDebugTool** — zero-GC in-game debug overlay (console, inspector, profiler, bug reporter).

## Cross-module dependencies (hard)

The only hard PFound→PFound edges (everything else is a leaf):

- AssetPipeline → ContentDelivery → Compression
- Commerce → RemoteGameConfig → RemoteResourceCache
- PlayerDataSync → RemoteGameConfig
- ServerOperationFlow → NetworkLayer (the adapter binds the lifecycle's transport seam to `ClientPeer`; the Core has no deps)
- GameDataStore / UserPrefs / UISystem → Utilities
- LocalizationService → Compression, Utilities
- HubApp → ContentDelivery, Signaling, Utilities
- GuidedOnboardingFlow → DependencyContainer, ScreenRouter, Signaling, TweenPresetLibrary, UserPrefs
- Render → Collections, DependencyContainer, LoopScheduler, Utilities

`Utilities` is the most-depended-on module; the App-Foundation primitives (DependencyContainer,
Signaling, LoopScheduler, StartupOrchestration, EpochClock, Collections, ECS, NetworkLayer, Input,
ScreenRouter, Compression, RemoteResourceCache) are dependency-free leaves.

## Optional integrations (define-gated, third-party — off by default)

Each is an isolated, define-gated assembly so the module builds without the package:

- **BestHTTP** (`PFOUND_BESTHTTP`) — ContentDelivery, RemoteResourceCache, LocalizationService.
- **Firebase** (`PFOUND_FIREBASE`) — RemoteGameConfig, PlayerDataSync, Commerce.
- **Unity IAP** (`PFOUND_UNITY_IAP`) — Commerce.
