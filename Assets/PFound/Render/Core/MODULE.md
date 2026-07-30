# Render.Core — Phase 1

Foundation primitives for every later Render phase: a base-class pair for URP 17 RenderGraph features/passes, a keyed transient `RenderTexture` pool with zero-allocation leak detection, a priority-ordered per-frame global shader parameter publisher, and the shared HLSL include library that all Phase 2+ shaders depend on.

> **Status:** v0.1.0 — shipped 2026-05-15. See `Render/CHANGELOG.md` (entry `[0.1.0]`).

---

## Scope

**Core is for:**
- Authoring URP 17 `ScriptableRendererFeature` / `ScriptableRenderPass` subclasses with a minimal override surface — `RenderFeatureBase` and `RenderPassBase<TPassData>` own all the boilerplate (pass list, material auto-dispose, RenderGraph builder setup, non-capturing render delegate caching).
- Leasing transient `RenderTexture` instances from a per-feature pool (`RenderTexturePool`) keyed on `(width, height, GraphicsFormat, depthBits, MSAA, HDR)`. Eviction + leak detection are first-class.
- Publishing per-frame global shader parameters in a deterministic priority order from multiple decoupled providers (`GlobalShaderParameterManager` + `IGlobalShaderParameterProvider`).
- Sharing low-level HLSL helpers across Phase 2+ effect shaders (`Common.hlsl`, `Math.hlsl`, `Sampling.hlsl`).

**Core is NOT for:**
- HDRP / Built-in Pipeline / URP Compatibility Mode — RenderGraph-only by design.
- Persistent (session-lifetime) render targets — the pool is purely transient; long-lived RTs are client-managed via the raw `RenderTexture` API (Phase 1 spec clarification 2).
- Compute / unsafe `ScriptableRenderPass` variants — `RenderPassBase<TPassData>` covers raster passes only. Compute base lands in a later phase when needed.
- Generic image utilities — `TextureResizer` + `RenderDebugTools` live in the sibling `PFound.Render.Utilities` asmdef (independent of Core per `Render/MODULE.md` FR-003).
- Effect-specific shader helpers — each later phase ships its own `*.hlsl` (`Blur.hlsl`, `Outline.hlsl`, `ColorGrading.hlsl`). Core ships only the universal subset.
- GameSpecific assets — Phase 1 produces no runtime assets (Constitution III). The placeholder `Core.Editor/.../RenderGameSpecificAssetProviders` documents the registration convention for future phases.

---

## Public Surface (Runtime — `PFound.Render.Core`)

| Type | Role |
|---|---|
| `RenderFeatureBase` (`Pipeline/`) | Abstract `ScriptableRendererFeature` base. Subclasses override `OnCreate()` only (and optionally `OnDispose()`); base owns the pass list, material allocation/disposal, `AddRenderPasses` enumeration, and `Dispose(bool)` chain. `Create()` and `AddRenderPasses()` are `sealed`. Helpers: `LoadMaterial(Shader)`, `LoadMaterial(string)`, `EnqueuePass(ScriptableRenderPass)`. |
| `RenderPassBase<TPassData>` (`Pipeline/`) | Abstract URP 17 RenderGraph `ScriptableRenderPass` base. `TPassData : class, new()`. Subclasses implement `Populate(IRasterRenderGraphBuilder, ref TPassData, ContextContainer)` + `Execute(RasterCommandBuffer, in TPassData)`. Constructor takes `passTag` (defaults to type name) + `injectionPoint` (defaults to `AfterRenderingTransparents`). `RecordRenderGraph` is `sealed` and caches a non-capturing delegate to keep the per-frame path allocation-free. |
| `ReferenceRenderFeature` (`ReferenceFeature/`) | Canonical no-op subclass of `RenderFeatureBase` shipped as a template. Enqueues a single `ReferenceRenderPass`. Phase 1 documentation, not a useful effect. |
| `ReferenceRenderPass` (`ReferenceFeature/`) | Canonical no-op subclass of `RenderPassBase<ReferenceRenderPass.PassData>`. Demonstrates the `Populate`/`Execute` pattern with one tracked field (`FrameNumber`) and no resource declarations. `AllowPassCulling(true)`. |
| `RenderTexturePool` (`RenderTextures/`) | Keyed transient RT pool. `Lease(in RenderTextureKey) → RenderTextureLease`, `Release(in RenderTextureLease)`, `Tick(int currentFrame)`, `ClearAll()`, `Dispose()`. Per-instance ownership — **no static `Instance`** (deliberate asymmetry vs `GlobalShaderParameterManager`). Idle eviction + leak detection via preallocated `UnsafeRingBuffer<RenderLeakEntry>`. |
| `RenderTextureKey` (`RenderTextures/`) | Readonly struct: `Width`, `Height`, `GraphicsFormat Format`, `DepthBits`, `MSAA`, `HDR`. Validates in constructor (`width/height > 0`; `depthBits ∈ {0,16,24,32}`; `msaa ∈ {1,2,4,8}`). FNV1a hash. Convenience ctor folds `(RenderTextureFormat, RenderTextureReadWrite)` into `GraphicsFormat` via `GraphicsFormatUtility` — so linear vs sRGB produce distinct keys, and HDR is derived from the resolved format. |
| `RenderTextureLease` (`RenderTextures/`) | `IDisposable` handle returned by `Lease`. Exposes `RT`, `Key`. `Dispose()` calls `Owner.Release(this)`. Idempotent. Internal `Token` + `Owner` fields gate the release path. |
| `RenderTexturePoolOptions` (`RenderTextures/`) | POCO config: `IdleFrameThreshold` (default 120), `LeakFrameThreshold` (default 600), `LeakRingBufferCapacity` (default 64), `LogLeaksToConsole` (default `Debug.isDebugBuild`). Constructor validates positive values. `Default` static accessor returns a fresh instance per call. |
| `RenderLeakEntry` (`RenderTextures/`) | Burst-compatible readonly struct stored in the leak ring buffer: `FixedString64Bytes Key`, `int LeasedFrame`, `int ReportedFrame`, `int ThreadId`. |
| `GlobalShaderParameterManager` (`ShaderParameters/`) | Per-frame priority-ordered publisher. `Register(provider, priority = 0)`, `Unregister(provider)`, `PublishAll()`, `GetSnapshot(IList<ProviderInfo>)`, `Dispose()`. Lazy static `Instance` (Constitution II exception — see Singleton Justification below). Same-instance double-registration throws `InvalidOperationException` + emits `Debug.LogError`. |
| `IGlobalShaderParameterProvider` (`ShaderParameters/`) | Interface: `string DebugName { get; }` + `void Publish()`. Implementations call `Shader.SetGlobal*` directly inside `Publish`. |
| `ProviderInfo` (`ShaderParameters/`) | Editor-debug struct returned by `GetSnapshot`: `DebugName`, `Priority`, `LastPublishedFrame`. |
| `Common.hlsl` (`Shaders/`) | Universal constants (`M_RENDER_PI`, `M_RENDER_INV_PI`, `M_RENDER_TWO_PI`, `M_RENDER_HALF_PI`, `M_RENDER_EPSILON`) + `MRender_Saturate` + `MRender_RemapClamped`. Include guard `PFOUND_RENDER_COMMON_INCLUDED`. |
| `Math.hlsl` (`Shaders/`) | `MRender_Pow2/3/4`, `MRender_LinearToGamma`, `MRender_GammaToLinear` (2.2 approximation — use URP's `Color.hlsl` for the real sRGB curve). Include guard `PFOUND_RENDER_MATH_INCLUDED`. |
| `Sampling.hlsl` (`Shaders/`) | `MRender_BoxFilter4Tap`, `MRender_GaussianFilter5Tap` — 5-tap separable Gaussian with weights `{0.227027, 0.1945946×2, 0.1216216×2}`. Used by Phase 3 Blur + Phase 4 Outline shaders. Include guard `PFOUND_RENDER_SAMPLING_INCLUDED`. |

Internal (not in the public surface):

| Type | Role |
|---|---|
| `PooledRenderTexture` (`RenderTextures/`, `internal`) | One-per-RT entry in the pool. Holds `RT`, `Key`, `Token`, `IsLeased`, `LeasedFrame`, `LastReleasedFrame`, `LeakReported`. |
| `UnsafeRingBuffer<T>` (`RenderTextures/`, `internal`) | Fixed-capacity oldest-overwrite ring backed by `UnsafeList<T>` (`Allocator.Persistent`). Zero per-write managed allocations. `unmanaged` constraint. |
| `PriorityRegistration` (`ShaderParameters/`, `internal`) | `IComparable<PriorityRegistration>` registration entry — sorts by `Priority` ascending then `InsertionOrder` (FIFO tiebreaker). |

---

## Architecture

### `RenderFeatureBase` lifecycle

The base class hides every URP-version-sensitive detail behind a stable subclass surface:

1. `Create()` (URP) → clears the internal pass + material lists, then calls `OnCreate()` once per feature creation (and after each assembly reload).
2. Subclasses call `LoadMaterial(...)` / `EnqueuePass(...)` inside `OnCreate`. Materials are constructed with `HideFlags.HideAndDontSave` via the embedded `CoreUtilsCompat.CreateEngineMaterial` helper (avoiding a hard dependency on a specific URP helper signature across versions).
3. `AddRenderPasses(...)` enumerates the tracked pass list and calls `renderer.EnqueuePass` for each.
4. `OnDestroy()` (URP does NOT wire this to `Dispose(true)` by default — only `IDisposable.Dispose()` does) calls `Dispose(true)` to tear down enqueued `IDisposable` passes + auto-allocated materials deterministically. `Dispose` is idempotent (`_disposed` guard).
5. Material teardown uses `Object.Destroy` in Play mode and `Object.DestroyImmediate` in Editor.

`Create()`, `AddRenderPasses()`, and `Dispose(bool)` are all `sealed` — the contract is fixed; subclasses extend only via `OnCreate()` / `OnDispose()`.

### `RenderPassBase<TPassData>` RenderGraph integration

Single-pass authoring model — the base owns the RenderGraph dance:

1. `RecordRenderGraph(renderGraph, frameData)` (URP, sealed) opens a `using` builder via `renderGraph.AddRasterRenderPass<TPassData>(_passTag, out passData)`.
2. Calls subclass `Populate(builder, ref passData, frameData)` — subclass fills the DTO + declares reads/writes on the builder.
3. Caches a non-capturing `BaseRenderFunc<TPassData, RasterGraphContext>` delegate (`_cachedRenderFunc`) so the per-frame path produces zero managed allocations. The cached adapter forwards to subclass `Execute(cmd, in passData)`.

`TPassData` instances are pooled / managed by URP — subclasses **must** treat them as transient and never retain references across frames.

### `RenderTexturePool` semantics

**Per-instance ownership.** No `Instance`. Construct one per `ScriptableRendererFeature` or share via DI. This is the deliberate counterpoint to `GlobalShaderParameterManager` — pools address a per-feature concern, not a process-wide one.

**Lease.** `Lease(in RenderTextureKey)` returns a pooled entry when one is free; otherwise allocates a new `RenderTexture` (`useDynamicScale = false`, `hidden + HideAndDontSave` semantics via `name = "PooledRT[{key}]"`). HDR keys override to `RenderTextureFormat.DefaultHDR`. The returned `RenderTextureLease` carries an internal `Token` for safe release matching.

**Release.** O(N) linear scan of the all-entries list (acceptable for typical small pool sizes; LINQ would allocate). Marks the entry `IsLeased = false`, records `LastReleasedFrame`, pushes onto the per-key free stack.

**Tick.** Two sweeps:
- **Idle eviction** — entries unleased for ≥ `IdleFrameThreshold` (default 120) frames: removed from the free stack via a temp-stack drain pattern, `RT.Release() + DestroyImmediate`, removed from the all-entries list.
- **Leak detection** — entries leased for ≥ `LeakFrameThreshold` (default 600) frames: appended to the `UnsafeRingBuffer<RenderLeakEntry>` (zero managed alloc) + optional `Debug.LogWarning` when `LogLeaksToConsole` is true. `LeakReported` flag prevents duplicate writes for the same entry.

**Dispose.** Drains any still-leased entries as leak reports first, then releases + destroys every `RenderTexture`, clears the dictionaries, and disposes the ring buffer's persistent allocation.

**Leak ring-buffer overflow** — `DroppedLeakCount` is exposed as a monotonic counter (oldest record is overwritten on collision; `TryReadLeak` drains FIFO, `GetLeakSnapshot` returns a non-destructive snapshot via `Span<T>` or `NativeArray<T>`).

### `GlobalShaderParameterManager` ordering

Providers are stored in a sorted `List<PriorityRegistration>` with insertion-time sort — O(N) per register, no per-frame sort cost. `PublishAll()` enumerates in ascending priority (FIFO tiebreaker for equal priorities) and updates each entry's `LastPublishedFrame` from `Time.frameCount`.

**Same-instance double-register is rejected.** The check uses `ReferenceEquals` against every existing entry's `Provider`, emits `Debug.LogError`, and throws `InvalidOperationException`. This is a defensive contract — two instances of the same provider class are still allowed (they get independent registrations).

`GetSnapshot(IList<ProviderInfo>)` is the editor-only debug surface (allocates only when the destination list grows). The runtime hot path (`PublishAll`) is zero-alloc after warm-up — verified by `GlobalShaderParameterManagerZeroAllocTests` (PlayMode).

### HLSL conventions

All Phase 2+ shader includes follow the prefix discipline established by Phase 1:

- **Macros**: `M_RENDER_<SCREAMING_SNAKE>` (e.g., `M_RENDER_PI`, `M_RENDER_EPSILON`).
- **Functions**: `MRender_<PascalCase>` (e.g., `MRender_Saturate`, `MRender_GaussianFilter5Tap`).
- **Include guards**: `PFOUND_RENDER_<MODULE>_INCLUDED`.
- Includes safe to compose alongside URP Core HLSL (`UNITY_*` macros) without symbol collision.

---

## Singleton Justification (per Constitution II)

Core ships **one** intentional static accessor:

- **`GlobalShaderParameterManager.Instance`** — lazy main-thread-only static. Justified because the manager addresses a **process-wide** concern (per-frame global shader parameters), is **performance-critical** (called every frame), and is **main-thread-only**. Consumers wanting strict DI can construct their own `new GlobalShaderParameterManager()` and ignore `Instance`.

By contrast, **`RenderTexturePool` has NO static `Instance`** — pool ownership is per-`ScriptableRendererFeature` (or via `ServiceRegistry`). Multiple pools coexist (one per Blur feature, one per Outline feature, etc.). The asymmetry is intentional: the global parameter manager addresses a process-wide concern; the pool addresses a per-feature concern.

---

## Owner-Managed Conventions

Per CODING-STYLE.md §8, both `RenderTexturePool` and `GlobalShaderParameterManager` follow owner-managed lifecycle:

- Pool: owner of the feature that constructed the pool is responsible for `Dispose()` in the matching `OnDispose`/`OnDestroy` hook. The pool does **not** subscribe to scene-unload events. Outstanding leases at `Dispose` time are reported as leaks first, then forcibly torn down.
- Manager: provider lifetime is owner-managed — register on enable, `Unregister` on disable. The manager does **not** observe MonoBehaviour lifecycle.
- No domain-reload auto-disposal hook is wired in Phase 1 — if a leak pattern emerges, a `[RuntimeInitializeOnLoadMethod]` cleanup may land in a later patch (`Render/MODULE.md` Limitations section).

---

## Tests

`PFound.Render.Core.Tests` (EditMode, namespace `PFound.Render.Core.Tests`):

- `RenderFeatureBaseTests` — `OnCreate` / `OnDispose` invocation count, `Dispose(true)` idempotence, `IDisposable` pass auto-disposal, material auto-cleanup.
- `RenderPassBaseTests` — `Populate` / `Execute` invocation count, pass-data field round-trip, injection-point + tag forwarding.
- `RenderTextureLeaseTests` — `Dispose()` idempotence, default-struct safety, `Token` / `Owner` mismatch handling.
- `RenderTexturePoolLeaseReleaseTests` — basic lease/release cycle, key-keyed reuse, distinct-key isolation.
- `RenderTexturePoolEvictionTests` — idle-eviction threshold behaviour, free-stack drain.
- `RenderTexturePoolLeakDetectionTests` — leak threshold, ring buffer writes, `LeakReported` no-duplicate guarantee.
- `RenderTexturePoolLogLeaksTests` — `LogLeaksToConsole` toggle behaviour.
- `RenderTexturePoolOverflowTests` — `DroppedLeakCount` monotonic on ring-buffer overflow.
- `RenderTexturePoolDisposeDrainTests` — outstanding-lease leak report at `Dispose`.
- `GlobalShaderParameterManagerRegistrationTests` — null reject, double-register `InvalidOperationException` + `Debug.LogError`.
- `GlobalShaderParameterManagerOrderingTests` — ascending-priority + FIFO-tiebreaker.
- `GlobalShaderParameterManagerUnregisterTests` — successful unregister + re-register idempotence.

`PFound.Render.Core.Tests.PlayMode` (PlayMode):

- `RenderTexturePoolZeroAllocTests` — 60-frame zero-alloc on the lease/release happy path (Profiler Recorder soft-threshold).
- `RenderTexturePoolLeakZeroAllocTests` — 60-frame zero-alloc on the leak-write path.
- `GlobalShaderParameterManagerZeroAllocTests` — 60-frame zero-alloc on `PublishAll` after warm-up.
- `ReferenceFeatureLifecycleTests` — `ReferenceRenderFeature` Create → AddRenderPasses → Dispose smoke through a real `ScriptableRenderer`.

---

## Known Gaps / Deferred

- **No compute pass base** — `RenderPassBase<TPassData>` covers raster passes only. Compute / unsafe pass variants will land when a concrete need appears (no current phase requires them).
- **No persistent RT pool** — by design. Session-lifetime RTs are explicitly client-managed (Phase 1 spec clarification 2).
- **No domain-reload auto-disposal hook** for `RenderTexturePool` — pool instances must be disposed by their owner (feature `OnDisable` / `Dispose`). Candidate for a `[RuntimeInitializeOnLoadMethod]` cleanup hook in a future patch if a leak pattern emerges.
- **`RenderTexturePool.Release` is O(N)** in the all-entries list — acceptable for typical small pool sizes in URP usage; consider a `Token → entry` map if profiling shows a hot path here.
- **No URP Compatibility Mode fallback** — RenderGraph-only. `Render/MODULE.md` documents this as a hard scope boundary.
- **HLSL Sampling.hlsl is intentionally minimal** — only the 4-tap box and 5-tap separable Gaussian helpers shared across Phase 3 Blur + Phase 4 Outline live here. Effect-specific filter weights (e.g., Roberts cross) live in per-effect `*.hlsl` includes.
- **Main-thread only** — every public API assumes the Unity main thread. No thread-safety guarantees on `RenderTexturePool` or `GlobalShaderParameterManager`.

---

## Editor Companion — `PFound.Render.Core.Editor`

Sibling editor asmdef at `Render/Core.Editor/` (Editor-only platform include). Phase 1 surface is intentionally minimal:

| Type | Role |
|---|---|
| `RenderGameSpecificAssetProviders` (`internal static`, `Core.Editor/Runtime/`) | Placeholder for future-phase `IGameSpecificAssetProvider` registrations under `Assets/GameSpecific/Render/`. **Phase 1 ships an empty class** — the convention is documented and the wiring point is reserved; no providers are registered (Phase 1 produces no runtime assets per Constitution III). Phase 2 ColorGrading was the first phase to add a real provider here (Identity LUT under `Assets/GameSpecific/Render/LUTs/Identity.asset`), in its own editor asmdef. |

**Asmdef references:** `PFound.Render.Core`, `PFound.Utilities.EditorHelpers`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Universal.Editor`. Editor-only (`includePlatforms: ["Editor"]`).

Future phases land their providers in their own `<Module>.Editor/` sibling folder (matches the Render-submodule layout convention — see `Render/MODULE.md` and `RenderContext/MODULE.md` for the established pattern).

---

## Related

- `Render/MODULE.md` — top-level Render submodule index + phase roadmap.
- `Render/Utilities/MODULE.md` — sibling utility asmdef (`TextureResizer`, `RenderDebugTools`), independent of Core.
- `Render/CHANGELOG.md` — release notes; Phase 1 entry is `[0.1.0]` (2026-05-15).
- `specs/012-render-core-foundation/` — full spec / plan / tasks / contracts for Phase 1.
- `Render/BatchRendering/MODULE.md`, `Render/RenderContext/MODULE.md`, `Render/UIShapes/MODULE.md` — example consumers of `RenderFeatureBase` / `RenderPassBase` / `RenderTexturePool` from later phases.
