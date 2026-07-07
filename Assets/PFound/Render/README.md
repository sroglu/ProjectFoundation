# PFound.Render

Rendering building blocks for Unity URP: a pooled render-texture / global-shader-parameter core, a
Burst-culled GPU instancing service, an off-screen render-to-texture "context" for portraits/
previews, and a set of pure texture utilities. Four independent sub-modules, each its own assembly —
take only what you need.

## Sub-modules

| Sub-module | Assembly | What it is | Needs scene/wiring? |
|---|---|---|---|
| **Core** | `PFound.Render.Core` | Render-texture pool, global shader parameter manager, URP render-feature/pass base classes. | Feature classes go on a URP Renderer; the rest are pure libraries. |
| **BatchRendering** | `PFound.Render.BatchRendering` | Burst frustum/distance-culled GPU instancing service (classic / procedural / indirect backends). | `new` the service; it self-drives. Optional URP feature for the RenderGraph path. |
| **RenderContext** | `PFound.Render.RenderContext` | Off-screen camera → RenderTexture bound to a `RawImage` / `MeshRenderer` / UI Toolkit element. | MonoBehaviour component + a one-time resolver config at boot. |
| **Utilities** | `PFound.Render.Utilities` | Texture creation, GPU resize/blit, readback, render debug helpers. | Pure static helpers — no setup. |

## Public API

**Core**
- `RenderTexturePool` — `new RenderTexturePool(options)`; `Lease(in RenderTextureKey)` →
  `RenderTextureLease`, `Release(in lease)`, `Tick(currentFrame)` (idle eviction), `ClearAll()`,
  leak snapshot APIs, `Dispose()`.
- `GlobalShaderParameterManager` — process singleton `.Instance`; `Register(IGlobalShaderParameterProvider, priority)`,
  `Unregister(...)`, `PublishAll()` (pushes every provider's globals, priority-ordered), `Clear()`,
  `ResetInstance()`.
- `RenderFeatureBase : ScriptableRendererFeature` (sealed `Create`/`AddRenderPasses`; override
  `OnCreate`) + `RenderPassBase`; `ReferenceRenderFeature` is a minimal working sample subclass.

**BatchRendering**
- `IBatchRenderingService` / `BatchRenderingService` — `RegisterBatch(BatchRenderingBatch)` →
  `IBatchHandle` (owner disposes it), `Dispose()`.
- `BatchRenderingBatch` (struct: `mesh`, `material`, `subMeshIndex`, `mpb`, `source`, `culling`,
  `backend`, `layer`, shadow/motion flags, `participatesInRenderGraph`).
- `IBatchInstanceSource` implementations: `TransformArrayInstanceSource`, `NativeArrayInstanceSource`,
  `ComputeBufferInstanceSource`. `CullingPolicy`, `BackendKind` describe culling + backend.
- `BatchRenderingFeature : RenderFeatureBase` — `AttachService(IBatchRenderingService)` /
  `DetachService()`, `injectionPoint`.

**RenderContext**
- `RenderContextSinkBehaviour` (MonoBehaviour) — authored descriptor + auto-resolved anchor;
  exposes `Texture`, `Camera`, `ContentRoot`, `IsAlive`.
- `IRenderContextService` / `RenderContextService` — `Acquire(RenderContextDescriptor, IRenderContextAnchor)`
  → `IRenderContextHandle`.
- `RenderContextResolver` (static) — `Use(...)` / `Resolve()` / `Clear()` / `IsConfigured`.

**Utilities** (all pure static / IDisposable): `TextureFactory`, `TextureResizer`, `RenderingTools`,
`RenderDebugTools`, `AutoSizedRenderTexture`.

## Setup / wiring

### Core
Pure libraries plus URP feature bases — nothing auto-instantiates.
- `RenderTexturePool` and `GlobalShaderParameterManager` are `new`/singleton libraries: own the pool
  instance yourself and call `Tick(frame)` once per frame from your host loop; call
  `GlobalShaderParameterManager.Instance.PublishAll()` after your providers register.
- `RenderFeatureBase` subclasses (including `ReferenceRenderFeature`) are `ScriptableRendererFeature`s:
  add them to the **Renderer Features** list on your URP *Universal Renderer* asset in the inspector.
  There is no scene object.
- `Core/Editor` `RenderGameSpecificAssetProviders` is an editor `InitializeOnLoad` extension point
  (a documented stub) for a game to register default render assets — no action needed to consume Core.

### BatchRendering
`new BatchRenderingService()` and you are running — the constructor spawns a hidden
`DontDestroyOnLoad` owner GameObject and subscribes to `PFound.LoopScheduler`'s before-render loop, so
it culls + dispatches every registered batch per active camera per frame with no MonoBehaviour of
yours. **Lifecycle is owner-managed**: whoever calls `RegisterBatch` must `Dispose()` the returned
handle at the matching unload/disable — the service never listens to `SceneManager` and never
auto-clears.

```csharp
var service = new BatchRenderingService();
var handle  = service.RegisterBatch(new BatchRenderingBatch
{
    mesh = mesh, material = mat, subMeshIndex = 0,
    source = new TransformArrayInstanceSource(transforms),
    culling = CullingPolicy.Default, backend = BackendKind.Classic,
});
// ... later, at the matching close hook:
handle.Dispose();
service.Dispose();
```

For the SRP RenderGraph dispatch path add a `BatchRenderingFeature` to your URP Renderer's feature
list and call `feature.AttachService(service)` (and `DetachService()` on teardown); mark those batches
`participatesInRenderGraph = true`. Depends on Unity Burst / Collections / Jobs / Mathematics and
`PFound.LoopScheduler`.

### RenderContext
Two steps.
1. **Configure the resolver once at boot** — the sink component resolves its service through the
   static `RenderContextResolver`, so pick a strategy before any sink enables:
   `RenderContextResolver.Use(new RenderContextService())` (singleton),
   `.Use(container)` (a `PFound.DependencyContainer`), or `.Use(() => ...)` (a delegate). If it is not
   configured, `Resolve()` throws — that is the wiring bug surfacing, by design.
2. **Add a `RenderContextSinkBehaviour`** to the GameObject that displays the result. It auto-resolves
   its anchor from a sibling `RawImage` (uGUI) or `MeshRenderer`; for UI Toolkit assign its
   `_uiDocument` + `_elementName`. On `OnEnable` it acquires a pooled camera+RenderTexture from the
   service and binds it to the anchor; on `OnDisable` it disposes the handle (the shared service is
   left alone). All sinks resolving to the same service share the RenderTexture pool.

### Utilities
Pure library — call the static helpers directly (`TextureFactory.*`, `TextureResizer.*`,
`RenderingTools.*`, `RenderDebugTools.*`), or `new AutoSizedRenderTexture(...)` and `Dispose()` it.
No scene object, no lifecycle.

## Layout

Each sub-module is `<Name>/Runtime/` (+ `Editor/`, `Tests/EditMode`, `Tests/PlayMode`) with its own
asmdef. `Shaders/` holds shared shader assets. Core has no third-party deps; BatchRendering pulls in
Burst/Collections/Jobs; RenderContext optionally references `PFound.DependencyContainer`.
