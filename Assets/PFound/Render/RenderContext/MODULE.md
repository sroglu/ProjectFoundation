# Render.RenderContext — Phase 10 (RT-only)

Render-to-texture stacked-camera contexts for offscreen content — hero portraits, dialog backdrops, minimap panels, world-space mirrors. Pipeline-agnostic across three rendering backends (uGUI `RawImage`, UI Toolkit `VisualElement`, world-space `MeshRenderer`).

> **Status:** v1.0 (Phase 10). See `Render/CHANGELOG.md` for release notes.

---

## Scope

**RenderContext is for:**
- Acquiring a pooled `RenderTexture` + dedicated `Camera` + a `ContentRoot` `Transform` via a single `Acquire(descriptor, anchor)` call.
- Binding the resulting RT to a target component (`RawImage`, `VisualElement`, `MeshRenderer`) with state restored on `Dispose`.
- Reusing RTs across acquire/dispose cycles via a `(width, height, format, depthBits, msaa, colorSpace)`-keyed pool — zero allocation on the steady-state hot path.
- Headless / pure-C# usage from EditMode tests (no scene required).

**RenderContext is NOT for:**
- URP RendererFeature authoring — use `Render.Core` for that.
- Persistent (session-lifetime) RTs — those are client-managed via the raw `RenderTexture` API.
- Camera stacking via URP's built-in `Camera.cameraStack` — RenderContext owns dedicated render-to-texture cameras, not screen-blit camera stacks.
- GameSpecific assets — descriptors are runtime values, not ScriptableObjects.

---

## Public Surface

| Type | Role |
|---|---|
| `IRenderContextService` | Entry point: `Acquire(descriptor, anchor) → IRenderContextHandle`, `Dispose()`. |
| `RenderContextService` | Concrete implementation. Construct directly or register via `RenderContextRegistration.Register(container)`. |
| `RenderContextRegistration` | Convenience bootstrap — `Register(container)` constructs service + registers on container + points `RenderContextResolver` at it. |
| `RenderContextResolver` | Static facade — `Use(provider/instance/container/func)`, `Clear()`, `IsConfigured`. Host configures once at boot; wrapper resolves through this. |
| `IRenderContextServiceProvider` | Strategy interface for service resolution. Built-ins: `SingletonRenderContextServiceProvider`, `ContainerRenderContextServiceProvider`, `DelegateRenderContextServiceProvider`. |
| `IRenderContextHandle` | Per-acquisition handle: `Texture`, `Camera`, `ContentRoot`, `IsAlive`, `Refresh()`, `IDisposable`. |
| `IRenderContextAnchor` | Target abstraction: `Target`, `PreferredWidth/Height`, `TargetAlive`, `CreateSink()`. |
| `IRenderContextSink` | RT-to-target binding: `Bind(rt)`, `Unbind()`. |
| `RenderContextDescriptor` | Value struct (12 fields) describing RT + camera essentials. |
| `RawImageAnchor` / `VisualElementAnchor` / `MeshRendererAnchor` | Built-in anchors for the three rendering backends. |
| `RawImageSink` / `VisualElementSink` / `MeshRendererSink` | Built-in sinks; capture target's pre-bind state, restore on `Unbind`. |
| `RenderContextSinkBehaviour` | Opt-in MonoBehaviour wrapper (Inspector authoring path; per `CODING-STYLE.md §3`). Requires a host-registered service. |

---

## Quick Start

### Host-app bootstrap (recommended)

```csharp
var container = new PFound.DependencyContainer.DependencyContainer();
RenderContextRegistration.Register(container);   // service constructed + stashed for the locator
container.Build();
```

Then anywhere in your code:

```csharp
var service = container.Get<IRenderContextService>();
var desc = new RenderContextDescriptor {
    Width = 512, Height = 512,
    Format = RenderTextureFormat.ARGB32,
    DepthBits = 16, Msaa = 2,
    ClearFlags = CameraClearFlags.SolidColor,
    BackgroundColor = new Color(0.1f, 0.1f, 0.15f, 0f),
    FieldOfView = 30f,
    CullingMask = 1 << LayerMask.NameToLayer("RenderContext"),
};
using var handle = service.Acquire(desc, new RawImageAnchor(myRawImage));
Instantiate(heroPrefab, handle.ContentRoot);
```

`RenderContextSinkBehaviour` MonoBehaviour wrapper resolves the registered service via `RenderContextResolver.Resolve()`. Inspector-driven consumers should ensure the host has called `Register` (or any other `RenderContextResolver.Use(...)` variant) before any wrapper's `OnEnable`.

### Standalone (no container, e.g., game-specific demo)

```csharp
var service = new RenderContextService();   // owned by you, dispose when done
RenderContextResolver.Use(service);         // wires the wrapper without DependencyContainer
// ... use as above ...
RenderContextResolver.Clear();
service.Dispose();
```

### Custom strategies

`RenderContextResolver.Use(...)` accepts four shapes:

| Call | When to use |
|---|---|
| `Use(IRenderContextServiceProvider provider)` | Custom resolution logic (multi-tenant routing, scoped overrides). |
| `Use(IRenderContextService instance)` | Host owns a long-lived singleton, no container needed. |
| `Use(PFound.DependencyContainer.DependencyContainer container)` | Resolve from a `DependencyContainer` on every call (default container path). |
| `Use(Func<IRenderContextService> factory)` | Tests, lazy bootstrap, lambdas without writing a provider class. |

The MB wrapper is strategy-agnostic — switching paths is a one-line host change.

---

## Dependencies

- `PFound.Render.Core` — RT lifecycle is delegated to `RenderTexturePool` (idle eviction + leak detection). Composition, not duplication.
- `PFound.DependencyContainer` — `RenderContextRegistration.Register(container)` host-side wiring helper.
- `PFound.LoopScheduler` — `BeforeRender` tick for `AnchorResizeWatcher` + Core pool eviction sweep.
- `Unity.RenderPipelines.Universal.Runtime` + `Unity.RenderPipelines.Core.Runtime` — `UniversalAdditionalCameraData` on the dedicated camera (post-process / shadows / camera-stack flags accessible).

Editor:
- `PFound.Render.RenderContext` (runtime peer)

---

## Topology

Per-acquisition hierarchy (built by `RenderContextSceneFactory`):

```text
[RenderContextService]            ← hidden owner GO (HideAndDontSave, DontDestroyOnLoad in PlayMode,
                                     parked at world (10000, 10000, 10000) for camera isolation)
└── RenderContextRoot_(W×H ...)/  ← wrapper, lifecycle root per pool entry
    ├── Camera/                   ← Camera child; targetTexture = leased RT
    └── ContentRoot/              ← Transform sibling; consumer parents prefabs here
```

The wrapper-with-siblings topology was a deliberate choice over child-of-camera (which made content inherit the camera's transform and collide with it). Camera and ContentRoot are independent now.

---

## Pool Composition

The pool delegates RT lifecycle to `PFound.Render.Core.RenderTextures.RenderTexturePool`:
- **Idle eviction** (default 120 frames unused → RT released) — frees GPU memory after menus close.
- **Leak detection** (default 600 frames leased → warning) — catches consumers that forget to Dispose handles.

Sidecar (Camera GO + ContentRoot) is tracked locally by `RenderContextPool` keyed by `RenderContextPoolKey`. When Core's pool evicts an RT, the sidecar persists — next Lease for that key gets a fresh RT, Camera.targetTexture is reassigned, content continues uninterrupted.

### Pool Key (Research Item 2 + GraphicsFormat folding)

Six descriptor fields are pool-keying: `(Width, Height, Format, DepthBits, Msaa, ColorSpace)`. ColorSpace folds into the Core pool's `GraphicsFormat` field inside `RenderTextureKey`'s `(RenderTextureFormat, RenderTextureReadWrite)` ctor overload — `R8G8B8A8_SRGB` vs `R8G8B8A8_UNorm` produce different Core keys, so linear vs sRGB descriptors don't collide. RenderContext's pool simply forwards the descriptor fields; the Core key does the fold.

Per-lease camera state (`CullingMask`, `ClearFlags`, `BackgroundColor`, `FieldOfView`, `Orthographic*`) is **excluded** from the key and re-applied on every lease via `SceneFactory.ResetCamera`.

---

## Diagnostics (Footgun Warnings — FR-010)

One-shot per service instance:

- **`Msaa > 2`** — high sample count costs measurable fillrate; consider 1 or 2.
- **`CullingMask == ~0` ("Everything")** — renders all scene layers into the context; usually unintended.
- **Camera shadows enabled** (URP `UniversalAdditionalCameraData.renderShadows`) — typically wasteful for portrait-scale RTs (extra depth pass). Default off; consumer can toggle on if they really want shadows in the context.

---

## Known Gaps (v1.x candidates)

- URP renderer-asset selection in descriptor.
- Camera near/far clip plane fields in descriptor.
- `sortingLayerID` field in descriptor.
- Live-preview EditorWindow.

---

## Folder Layout

```text
RenderContext/
├── Runtime/
│   ├── Service/        # IRenderContextService, IRenderContextHandle, impls
│   ├── Descriptor/     # RenderContextDescriptor value struct
│   ├── Anchors/        # IRenderContextAnchor + IExplicitSizeAnchor + 3 built-ins
│   ├── Sinks/          # IRenderContextSink + 3 built-ins
│   ├── Pool/           # RenderContextPool + RenderContextPoolKey + PooledEntry
│   ├── Scene/          # RenderContextSceneFactory (BuildHierarchy + ResetCamera + DestroyChildren)
│   ├── Watcher/        # AnchorResizeWatcher (LoopScheduler BeforeRender tick)
│   └── Wrapper/        # RenderContextSinkBehaviour (opt-in MonoBehaviour)
└── PFound.Render.RenderContext.asmdef

RenderContext.Editor/    # Sibling editor folder (Render submodule convention)
├── Editor/
│   └── RenderContextSinkBehaviourEditor.cs
└── PFound.Render.RenderContext.Editor.asmdef
```

> **Layout note:** Spec (`plan.md`) initially showed Editor as a subfolder under RenderContext/. The Render submodule convention (Core, ColorGrading, Particles.Image, …) is a **sibling** `<Module>.Editor/` folder with the asmdef at its root. This module follows the sibling convention.

---

## Tests

EditMode (in shared `PFound.Render.Tests` asmdef):

- `RenderContextDescriptorTests` — value-struct validation rules.
- `RenderContextPoolKeyTests` — 6-field equality, hash stability, per-lease-field exclusion.
- `RenderContextPoolTests` — Lease/Return reference identity, content-child destroy, idempotent dispose.
- `RenderContextServiceLifecycleTests` — owner-GO flags, null/disposed guards, double-bind throw, dispose chain.
- `RawImageAnchorTests` + `RawImageSinkTests` — US1 backend.
- `VisualElementAnchorTests` + `VisualElementSinkTests` — US2 backend.
- `MeshRendererAnchorTests` + `MeshRendererSinkTests` — US3 backend.
- `AnchorResizeWatcherTests` — Tick semantics (stable/diff/zero-size/destroyed-target).
- `RenderContextHeadlessRenderTests` — US4 pure-C# render path (SC-004 timing logged).
- `RenderContextHandleIdempotentDisposeTests` — handle dispose + post-dispose accessor throws.
- `RenderContextZeroAllocSteadyStateTests` — SC-002 zero-alloc check (Recorder-based, soft-threshold).
- `RenderContextDiagnosticsTests` — one-shot warning semantics (MSAA>2, CullingMask=~0, per-service-instance).

PlayMode (in same asmdef):

- `RenderContextUGUISmokeTests` — US1 RT pixel-content assertion.
- `RenderContextUIToolkitSmokeTests` — US2 RT pixel-content assertion via runtime UIDocument.
- `RenderContextWorldSpaceSmokeTests` — US3 material-clone + pixel-content + sharedMaterial restoration.
- `RenderContextWrapperParityTests` — `RenderContextSinkBehaviour` end-to-end through OnEnable/OnDisable.

---

## Troubleshooting

- **"My content shows up in the consumer's main camera too"** — the service parks its owner GO at (10000, 10000, 10000) by default. If your main camera is anywhere within ~12000 units of origin, it won't see the RT content. If you need stronger isolation, set descriptor `CullingMask` to a dedicated layer and exclude that layer from your main camera's culling mask.
- **"My RT shows only the clear color, no content"** — the older topology bug (Camera as parent of ContentRoot) was fixed; if you see this now, check that content is parented under `handle.ContentRoot` (not `handle.Camera.transform`).
- **"Releasing render texture that is set as Camera.targetTexture!"** — should never happen in normal use; if it does, the Pool's `DestroyEntry` path was hit before `Camera.targetTexture` was cleared. File an issue with repro steps.
- **"InvalidOperationException: Anchor target already bound by a live handle"** — same target object is being passed to two simultaneous `Acquire` calls. Dispose the first handle before the second `Acquire`, or use a different target.

## Unity 6 Behaviors Discovered During Implementation

- **`VisualElement.style.backgroundImage` silently drops `RenderTexture` values.** Unity 6's IStyle setter normalizes any `Background` whose only populated field is `renderTexture` to `StyleKeyword.Null`. The supported path is the `Image` element with its `image` property (accepts any `Texture` including `RenderTexture`). `VisualElementSink` attaches an absolute-positioned, stretch-to-fill `Image` child (named `__renderContextImage`) on `Bind`, removes it on `Unbind`. Original `style.backgroundImage` content is preserved untouched.
- **`Transform.childCount` doesn't always reflect post-`DestroyImmediate` state on the same call.** `RenderContextSceneFactory.DestroyChildren` detaches each child via `SetParent(null)` BEFORE destroying — guarantees `childCount` drops immediately, regardless of EditMode/PlayMode distinction. (Caught by `RenderContextPoolTests.Return_DestroysContentChildren`.)
- **PlayMode test harness does not always tick dedicated cameras via Unity's auto-render loop.** Tests that `ReadPixels` from `handle.Texture` call `handle.Camera.Render()` explicitly before sampling. The runtime path (consumers driving via `Time.deltaTime` frames in normal play) is unaffected — only the test-harness frame ticks need the explicit kick.
- **`DontDestroyOnLoad` rejects EditMode callers** — wrapped in `if (Application.isPlaying)` guard inside the service ctor so EditMode tests don't throw.
