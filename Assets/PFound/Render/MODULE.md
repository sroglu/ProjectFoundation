# Render

Rendering building blocks for Unity URP: a pooled render-texture / global-shader-parameter core, a
Burst-culled GPU instancing service, an off-screen render-to-texture "context" for portraits and
previews, a set of pure texture utilities, volume-driven full-screen effects (blur, outline, an
overdraw debug view), a typed post-process stack, a boot shader pre-warm controller, and an SDF UI
shape shader + tooling. Ten independent sub-modules, each its own assembly — take only what you
need.

This is the **parent index** for the Render subsystem. Each sub-module has its own deep doc; this
file covers subsystem scope, the sub-module map, the assembly dependency model, the GameSpecific hook,
and cross-cutting setup/wiring. Depth for any single sub-module lives in that sub-module's `MODULE.md`.

---

## Purpose

Render is the low-level rendering toolbox that game code and higher PFound modules compose against.
It deliberately splits into per-feature assemblies so a consumer pulls only the surface it needs — a
project that only wants a texture downscaler does not drag in URP, Burst, or a DI container, and a
project using the blur effect need not pull in the batch renderer.

**In scope:** URP 17 RenderGraph feature/pass base classes, a keyed transient `RenderTexture` pool,
per-frame global shader parameter publishing, Burst-compiled GPU-instanced batch rendering, off-screen
render-to-texture contexts bound to uGUI / UI Toolkit / world-space targets, and pipeline-agnostic
texture helpers.

**Out of scope:** HDRP / Built-in Pipeline / URP Compatibility Mode (RenderGraph-only), skinned-mesh
instancing, `BatchRendererGroup`, persistent (session-lifetime) render targets (client-managed), and
GameSpecific runtime assets (the sub-modules ship none — see the GameSpecific hook below).

---

## Assemblies

Each sub-module is its own assembly (all `autoReferenced: true`); take only what you need:

| Assembly | Kind | Location |
|---|---|---|
| `PFound.Render.Core` | runtime | `Core/PFound.Render.Core.asmdef` |
| `PFound.Render.Core.Editor` | editor | `Core/Editor/PFound.Render.Core.Editor.asmdef` |
| `PFound.Render.BatchRendering` | runtime | `BatchRendering/PFound.Render.BatchRendering.asmdef` |
| `PFound.Render.RenderContext` | runtime | `RenderContext/PFound.Render.RenderContext.asmdef` |
| `PFound.Render.RenderContext.Editor` | editor | `RenderContext/Editor/PFound.Render.RenderContext.Editor.asmdef` |
| `PFound.Render.Utilities` | runtime | `Utilities/PFound.Render.Utilities.asmdef` |
| `PFound.Render.Effects.Blur` | runtime | `Effects.Blur/PFound.Render.Effects.Blur.asmdef` |
| `PFound.Render.Effects.Outline` | runtime | `Effects.Outline/PFound.Render.Effects.Outline.asmdef` |
| `PFound.Render.Effects.Overdraw` | runtime | `Effects.Overdraw/PFound.Render.Effects.Overdraw.asmdef` |
| `PFound.Render.PostProcess` | runtime | `PostProcess/PFound.Render.PostProcess.asmdef` |
| `PFound.Render.ShaderWarmup` | runtime | `ShaderWarmup/PFound.Render.ShaderWarmup.asmdef` |
| `PFound.Render.UIShapes` | runtime | `UIShapes/PFound.Render.UIShapes.asmdef` |
| `PFound.Render.UIShapes.Editor` | editor | `UIShapes/Editor/PFound.Render.UIShapes.Editor.asmdef` |

Each foundation sub-module also ships test assemblies: `PFound.Render.Core.Tests` / `.Core.Tests.PlayMode`,
`PFound.Render.BatchRendering.Tests` / `.Tests.PlayMode`, `PFound.Render.RenderContext.Tests` /
`.Tests.PlayMode`, `PFound.Render.Utilities.Tests` / `.Tests.PlayMode`.

---

## Dependencies

Verified from the asmdefs. There are no external (non-Unity, non-PFound) vendor packages anywhere in
Render; no scripting-define gates are used.

| Assembly | PFound deps | Unity package deps |
|---|---|---|
| `PFound.Render.Core` | none | URP Universal.Runtime, URP Core.Runtime, Collections, Burst, Mathematics (`allowUnsafeCode`) |
| `PFound.Render.Core.Editor` | `PFound.Render.Core`, `PFound.Utilities.EditorHelpers` | URP Universal.Runtime, URP Universal.Editor (Editor-only) |
| `PFound.Render.BatchRendering` | `PFound.Render.Core`, `PFound.LoopScheduler` | URP Universal.Runtime, URP Core.Runtime, Burst, Collections, Mathematics, Jobs |
| `PFound.Render.RenderContext` | `PFound.Render.Core`, `PFound.DependencyContainer`, `PFound.LoopScheduler` | URP Universal.Runtime, URP Core.Runtime |
| `PFound.Render.RenderContext.Editor` | `PFound.Render.RenderContext` | — (Editor-only) |
| `PFound.Render.Utilities` | none | Mathematics only |
| `PFound.Render.Effects.Blur` | `PFound.Render.Core`, `PFound.Collections` | URP Universal.Runtime, URP Core.Runtime |
| `PFound.Render.Effects.Outline` | `PFound.Render.Core`, `PFound.Collections` | URP Universal.Runtime, URP Core.Runtime |
| `PFound.Render.Effects.Overdraw` | `PFound.Render.Core` | URP Universal.Runtime, URP Core.Runtime |
| `PFound.Render.PostProcess` | `PFound.Render.Core`, `PFound.Render.Effects.Blur`, `PFound.Render.Effects.Outline`, `PFound.DependencyContainer`, `PFound.LoopScheduler` | URP Universal.Runtime, URP Core.Runtime |
| `PFound.Render.ShaderWarmup` | `PFound.Render.Core`, `PFound.DependencyContainer`, `PFound.LoopScheduler` | — |
| `PFound.Render.UIShapes` | `PFound.Render.Core` | — |
| `PFound.Render.UIShapes.Editor` | `PFound.Render.UIShapes`, `PFound.Render.Core` | — (Editor-only) |

> `PFound.DependencyContainer` is referenced by RenderContext for the host bootstrap helper only —
> the service itself does not require a container (see the asmdef dependency model below).

---

## Sub-module map / index

| Sub-module | Assembly | Purpose | Doc | Needs scene/wiring? |
|---|---|---|---|---|
| **Core** | `PFound.Render.Core` | Render-texture pool, global shader parameter manager, URP RenderGraph feature/pass base classes, shared HLSL includes. | [Core/MODULE.md](Core/MODULE.md) | Feature classes go on a URP Renderer asset; the rest are pure libraries. |
| **BatchRendering** | `PFound.Render.BatchRendering` | Burst frustum/distance-culled GPU instancing service (classic / indirect / procedural backends). | [BatchRendering/MODULE.md](BatchRendering/MODULE.md) | `new` the service; it self-drives via `PFound.LoopScheduler`. Optional URP feature for the RenderGraph path. |
| **RenderContext** | `PFound.Render.RenderContext` | Off-screen camera → `RenderTexture` bound to a `RawImage` / `MeshRenderer` / UI Toolkit element. | [RenderContext/MODULE.md](RenderContext/MODULE.md) | MonoBehaviour sink component + a one-time resolver config at boot. |
| **Utilities** | `PFound.Render.Utilities` | Texture creation, GPU resize/blit, readback, strip-gated render debug helpers. | [Utilities/MODULE.md](Utilities/MODULE.md) | Pure static helpers / disposable handles — no setup. |
| **Effects.Blur** | `PFound.Render.Effects.Blur` | Volume-driven full-screen Gaussian blur + priority-queue request service. | [Effects.Blur/MODULE.md](Effects.Blur/MODULE.md) | RendererFeature on the URP Renderer asset; `new` the request service (optional). |
| **Effects.Outline** | `PFound.Render.Effects.Outline` | Volume-driven full-screen depth-edge outline + priority-queue request service. | [Effects.Outline/MODULE.md](Effects.Outline/MODULE.md) | RendererFeature on the URP Renderer asset; `new` the request service (optional). |
| **Effects.Overdraw** | `PFound.Render.Effects.Overdraw` | Developer-only overdraw heatmap debug view (strips from release builds). | [Effects.Overdraw/MODULE.md](Effects.Overdraw/MODULE.md) | RendererFeature on the URP Renderer asset; inspector toggle. |
| **PostProcess** | `PFound.Render.PostProcess` | Typed post-process request/adapter stack (built-in Blur + Outline adapters). | [PostProcess/MODULE.md](PostProcess/MODULE.md) | `Register(container)` once; self-ticks via `PFound.LoopScheduler`. |
| **ShaderWarmup** | `PFound.Render.ShaderWarmup` | Time-sliced boot shader variant pre-warm controller. | [ShaderWarmup/MODULE.md](ShaderWarmup/MODULE.md) | `Register(container)` + `BeginSession(...)`; self-ticks via `PFound.LoopScheduler`. |
| **UIShapes** | `PFound.Render.UIShapes` | SDF UI shape shader + size-sync component + editor bake tooling. | [UIShapes/MODULE.md](UIShapes/MODULE.md) | Assign the `Render/UI/Shape` material to a UI Graphic; add `UIShapeSizeSync`. |

---

## Public API

Brief per-sub-module surface pointers. Full signatures live in each sub-module's `MODULE.md`.

**Core** — `RenderTexturePool` (`new`; `Lease(in RenderTextureKey) → RenderTextureLease`,
`Release(in lease)`, `Tick(currentFrame)`, `ClearAll()`, leak snapshot APIs, `Dispose()`);
`GlobalShaderParameterManager` (lazy `.Instance`; `Register(IGlobalShaderParameterProvider, priority)`,
`Unregister(...)`, `PublishAll()`, `GetSnapshot(...)`, `Dispose()`); `RenderFeatureBase` /
`RenderPassBase<TPassData>` (URP RenderGraph base classes; override `OnCreate` / `Populate` / `Execute`)
with `ReferenceRenderFeature` / `ReferenceRenderPass` as the template subclasses.

**BatchRendering** — `IBatchRenderingService` / `BatchRenderingService`
(`RegisterBatch(BatchRenderingBatch) → IBatchHandle`, `Dispose()`); the `BatchRenderingBatch` value
struct + `CullingPolicy` / `BackendKind`; instance sources `NativeArrayInstanceSource` /
`ComputeBufferInstanceSource` / `TransformArrayInstanceSource`; `BatchRenderingFeature`
(`AttachService` / `DetachService`) for the RenderGraph path.

**RenderContext** — `RenderContextSinkBehaviour` (MonoBehaviour; `Texture`, `Camera`, `ContentRoot`,
`IsAlive`); `IRenderContextService` / `RenderContextService`
(`Acquire(RenderContextDescriptor, IRenderContextAnchor) → IRenderContextHandle`);
`RenderContextResolver` (static — `Use(...)` / `Resolve()` / `Clear()` / `IsConfigured`);
`RenderContextRegistration.Register(container)` bootstrap helper; the three anchor/sink pairs.

**Utilities** — all pure static / disposable: `TextureFactory`, `TextureResizer`
(+ `TextureResizeHandle`), `TextureResizer` GPU resize, `RenderingTools`, `RenderDebugTools`
(strip-gated), `AutoSizedRenderTexture` (`IDisposable`).

---

## Setup / wiring

### Core

Pure libraries plus URP feature base classes — nothing auto-instantiates.

- `RenderTexturePool` and `GlobalShaderParameterManager` are `new` / singleton libraries: own the pool
  instance yourself and call `Tick(frame)` once per frame from your host loop; call
  `GlobalShaderParameterManager.Instance.PublishAll()` after your providers register.
- `RenderFeatureBase` subclasses (including `ReferenceRenderFeature`) are `ScriptableRendererFeature`s:
  add them to the **Renderer Features** list on your URP *Universal Renderer* asset in the inspector.
  There is no scene object.
- `Core.Editor`'s `RenderGameSpecificAssetProviders` is an editor `InitializeOnLoad` extension point
  (a documented stub) for a game to register default render assets — no action needed to consume Core.
  See the GameSpecific hook section below.

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
`participatesInRenderGraph = true`.

### RenderContext

Two steps.

1. **Configure the resolver once at boot** — the sink component resolves its service through the
   static `RenderContextResolver`, so pick a strategy before any sink enables:
   `RenderContextResolver.Use(new RenderContextService())` (singleton),
   `.Use(container)` (a `PFound.DependencyContainer`), or `.Use(() => ...)` (a delegate). If it is not
   configured, `Resolve()` throws — that is the wiring bug surfacing, by design.
2. **Add a `RenderContextSinkBehaviour`** to the GameObject that displays the result. It auto-resolves
   its anchor from a sibling `RawImage` (uGUI) or `MeshRenderer`; for UI Toolkit assign its
   `UIDocument` + element name. On `OnEnable` it acquires a pooled camera + `RenderTexture` from the
   service and binds it to the anchor; on `OnDisable` it disposes the handle (the shared service is
   left alone). All sinks resolving to the same service share the RenderTexture pool.

Host-bootstrap variant (DI container):

```csharp
var container = new PFound.DependencyContainer.DependencyContainer();
RenderContextRegistration.Register(container);   // constructs the service + points the resolver at it
container.Build();
```

### Utilities

Pure library — call the static helpers directly (`TextureFactory.*`, `TextureResizer.*`,
`RenderingTools.*`, `RenderDebugTools.*`), or `new AutoSizedRenderTexture(...)` and `Dispose()` it.
No scene object, no lifecycle. `TextureResizer.Resize(...)` returns a `TextureResizeHandle` you must
`using` / `Dispose()` (pass-through disposal is a no-op; the downscale path owns and destroys its
result).

---

## asmdef dependency model

The four assemblies are intentionally decoupled so a consumer takes only what it needs:

- **Core is independent** — it references only Unity URP + Collections/Burst/Mathematics packages; no
  other PFound module. Everything else in Render that touches URP builds on top of it.
- **Utilities is independent of Core** — it references only `Unity.Mathematics`, no URP, no Core. A
  project can take `PFound.Render.Utilities` alone. This independence is a hard contract.
- **BatchRendering depends on Core** (`RenderFeatureBase` / `RenderPassBase`) **and**
  `PFound.LoopScheduler` (before-render tick), plus the Burst/Collections/Jobs/Mathematics cull stack.
  It deliberately has **no** `PFound.DependencyContainer` dependency — container registration is
  consumer-side wiring, outside this module's surface.
- **RenderContext depends on Core** (RT lifecycle delegates to `RenderTexturePool` — composition, not
  duplication), `PFound.LoopScheduler` (resize watcher + eviction sweep), and **optionally**
  `PFound.DependencyContainer` (the `RenderContextRegistration.Register(container)` bootstrap helper).
  The service works standalone via `RenderContextResolver.Use(new RenderContextService())` — the
  container is one of four resolution strategies, never required.

Editor companions (`Core.Editor`, `RenderContext.Editor`) are Editor-only (`includePlatforms:
["Editor"]`) and reference their runtime peer.

---

## GameSpecific hook

`RenderGameSpecificAssetProviders` (`internal static` in `PFound.Render.Core.Editor`, at
`Core/Editor/Runtime/RenderGameSpecificAssetProviders.cs`) is the documented per-game
default-render-asset registration seam. It is an editor `InitializeOnLoad` extension point: a game
project registers `IGameSpecificAssetProvider` instances here, and the parent project's
`GameSpecificAssetGuard` auto-creates their generated assets under `Assets/GameSpecific/Render/`.

The class currently ships as an intentionally empty placeholder — the runtime sub-modules produce no
GameSpecific assets, so no providers are registered. The seam exists so future default render assets
(e.g. a default color-grading LUT or post-process profile) land predictably in a game-specific,
non-shared location without an asmdef dependency from the framework onto the game.

---

## File Structure

```text
Render/
├── Core/                     # PFound.Render.Core — RT pool, shader params, URP RenderGraph bases
│   ├── Runtime/              #   Pipeline/, RenderTextures/, ShaderParameters/, ReferenceFeature/
│   ├── Shaders/              #   shared HLSL includes: Common.hlsl, Math.hlsl, Sampling.hlsl
│   ├── Editor/               # PFound.Render.Core.Editor — GameSpecific registration seam (stub)
│   ├── Tests/                #   EditMode/ + PlayMode/
│   └── MODULE.md, CHANGELOG.md
├── BatchRendering/           # PFound.Render.BatchRendering — Burst-culled GPU instancing service
│   ├── Runtime/              #   Service/, RenderGraph/, culling jobs, instance sources
│   ├── Tests/                #   EditMode/ + PlayMode/
│   └── MODULE.md
├── RenderContext/            # PFound.Render.RenderContext — off-screen render-to-texture contexts
│   ├── Runtime/              #   Service/, Descriptor/, Anchors/, Sinks/, Pool/, Scene/, Watcher/, Wrapper/
│   ├── Editor/               # PFound.Render.RenderContext.Editor — sink inspector
│   ├── Tests/                #   EditMode/ + PlayMode/
│   └── MODULE.md, CHANGELOG.md
├── Utilities/                # PFound.Render.Utilities — texture + debug-draw helpers (no URP/Core dep)
│   ├── Runtime/              #   TextureFactory, TextureResizer(+Handle), RenderingTools, RenderDebugTools, AutoSizedRenderTexture
│   ├── Tests/                #   EditMode/ + PlayMode/
│   └── MODULE.md
├── Effects.Blur/             # PFound.Render.Effects.Blur — volume-driven full-screen blur + request service
│   ├── Runtime/              #   BlurRenderFeature/Pass, BlurStrengthVolumeComponent, BlurSpec, BlurRequestService(+Ticket)
│   ├── Shaders/              #   Blur.shader + Blur.hlsl
│   └── MODULE.md
├── Effects.Outline/          # PFound.Render.Effects.Outline — volume-driven depth-edge outline + request service
│   ├── Runtime/              #   OutlineRenderFeature/Pass, OutlineVolumeComponent, OutlineSpec, OutlineRequestService(+Ticket)
│   ├── Shaders/              #   Outline.shader + Outline.hlsl
│   └── MODULE.md
├── Effects.Overdraw/         # PFound.Render.Effects.Overdraw — overdraw heatmap debug view (strips in release)
│   ├── Runtime/              #   OverdrawRenderFeature/Pass, OverdrawThresholdEntry
│   ├── Shaders/              #   Overdraw.shader + Overdraw.hlsl
│   └── MODULE.md
├── PostProcess/              # PFound.Render.PostProcess — typed request/adapter post-process stack
│   ├── Runtime/              #   Core/ (service, options, registration, ticket), Adapters/ (Blur, Outline), Requests/
│   └── MODULE.md
├── ShaderWarmup/             # PFound.Render.ShaderWarmup — time-sliced boot shader pre-warm controller
│   ├── Runtime/              #   ShaderWarmupController, WarmupSession/Batch, RenderShaderWarmupRegistration
│   └── MODULE.md
├── UIShapes/                 # PFound.Render.UIShapes — SDF UI shape shader + tooling
│   ├── Runtime/              #   UIShapeSizeSync, material-property/keyword helpers, Shaders/ (UIShape.shader + SDF/Noise/Effects HLSL), UIShape.mat
│   ├── Editor/               # PFound.Render.UIShapes.Editor — inspector + bake window/service/validator
│   └── MODULE.md
├── Shaders/                  # shared authored shaders (SoftToony URP shader set)
├── MODULE.md                 # this file
└── README.md                 # thin landing page
```

---

## Downstream Dependents

None within PFound — no other PFound module references `PFound.Render.*` (verified by asmdef grep).
Internally, most sub-modules consume `PFound.Render.Core`, and `PFound.Render.PostProcess` consumes
`PFound.Render.Effects.Blur` + `PFound.Render.Effects.Outline` (its built-in adapters drive their
volume components). Consumers are game projects that select the sub-module assemblies they need.

---

## Limitations / Known Gaps

Per-sub-module gaps are detailed in each sub-module's `MODULE.md`. Subsystem-wide notes:

- **RenderGraph-only** — no HDRP / Built-in / URP Compatibility Mode fallback. This is a hard scope
  boundary.
- **Main-thread only** — every public API assumes the Unity main thread; no thread-safety guarantees on
  `RenderTexturePool`, `GlobalShaderParameterManager`, or the batch service.
- **Owner-managed lifecycle everywhere** — pools, batch handles, render-context handles, and texture
  resize handles are all disposed by their owner. None subscribe to `SceneManager` unload events; a
  forgotten `Dispose` leaks until domain reload (Core's pool + the batch service emit leak/degrade
  warnings as a debugging aid, not an auto-fix).
- **`TextureResizer` blit-path allocation is unavoidable** — only the pass-through path is asserted
  zero-allocation; the downscale path must allocate one `Texture2D` for the readback.
- **BatchRendering occlusion culling is a stub** (flag + one-shot warning) and indirect is
  single-chunk-per-batch in the current phase.
- **No GameSpecific runtime assets ship** — the registration seam is reserved but empty.

---

## Verification

- **Unity console clean** — open the project and confirm no compile errors / warnings for any Render
  assembly after a domain reload.
- **Tests** — run the per-sub-module test assemblies via the Unity Test Runner:
  `PFound.Render.Core.Tests` / `.Core.Tests.PlayMode`, `PFound.Render.BatchRendering.Tests` /
  `.Tests.PlayMode`, `PFound.Render.RenderContext.Tests` / `.Tests.PlayMode`,
  `PFound.Render.Utilities.Tests` / `.Tests.PlayMode`.
- **Pure-C# testable bits** — most Core pool logic, RenderContext pool/descriptor logic, and Utilities
  math run headless in EditMode (no scene / no camera required); PlayMode suites cover the GPU
  round-trips, zero-alloc soft-thresholds (Profiler Recorder), and RenderGraph smoke paths that need a
  live `ScriptableRenderer` / camera render.
