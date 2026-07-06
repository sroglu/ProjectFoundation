# Render.BatchRendering — Phase 11

GPU-instanced batch rendering with Burst-compiled frustum + distance culling. Three backends (`Classic` / `Indirect` / `Procedural`) cover the spectrum from 100-instance gameplay batches to 500k-instance GPU-authored fields.

> **Status:** v0.11.0 — shipped. See `specs/023-batch-rendering/` for the spec / plan / tasks / contracts. Release notes in `Render/CHANGELOG.md`.

---

## Scope

**BatchRendering is for:**
- Drawing many copies of the same `Mesh + Material` pair every frame with one author call: `service.RegisterBatch(...)` → `IBatchHandle`.
- CPU-authored instance transforms (`NativeArray<float4x4>`) via the classic backend.
- GPU-authored instance buffers (`ComputeBuffer<MeshInstanceData>`) via the indirect backend.
- Consumer-owned vertex/index pipelines via the procedural backend (`Graphics.DrawProceduralIndirect` pass-through).
- Burst-compiled frustum culling (sphere-vs-6-planes) by default; optional Burst distance culling.
- Opt-in URP RenderGraph integration via `BatchRenderingFeature` for batches that need to participate in the URP render graph (depth-aware effects, downstream passes).

**BatchRendering is NOT for:**
- Skinned mesh instancing — out of scope; use `SkinnedMeshRenderer` directly.
- `BatchRendererGroup` (Unity's GPU-driven scene API) — separate, heavier sibling that's explicitly out of scope.
- Occlusion culling — stub-only in Phase 11 (sets a flag → emits a one-shot warning). Implementation deferred to a later patch.
- Per-instance scalar shader parameters via a service-owned API — consumers continue to use `MaterialPropertyBlock` (classic / procedural backends) or `ComputeBuffer`-side packing (indirect backend).
- GameSpecific assets — descriptors are runtime values, no ScriptableObject ships in the submodule (Constitution III preserved).

---

## Public Surface

| Type | Role |
|---|---|
| `IBatchRenderingService` | Entry point: `RegisterBatch(BatchRenderingBatch) → IBatchHandle`, `Dispose()`. Pure-C#; **no DependencyContainer dependency**. |
| `BatchRenderingService` | Concrete implementation. Construct directly: `new BatchRenderingService()`. Consumers MAY register on any container they choose — that's consumer-side wiring, outside this module's surface. |
| `IBatchHandle` | Per-batch handle: `IsAlive`, `IsDegraded`, `DegradedReason`, `RegisteredInstanceCount`, `LastFrameVisibleCount`, `Dispose()`. Idempotent. |
| `BatchRenderingBatch` | Value struct (12 fields) describing mesh + material + instance source + culling + backend + render-graph participation flag. |
| `BackendKind` | Enum: `Classic` / `Indirect` / `Procedural`. |
| `CullingPolicy` | Value struct: `frustum`, `distance`, `occlusion`. Sentinels: `CullingPolicy.Default` (frustum-on), `CullingPolicy.None` (skip all). |
| `DistanceCullingConfig` | Nested value struct: `enabled`, `maxDistance`. |
| `BatchDegradedReason` | Enum used by `IBatchHandle.DegradedReason`: `MissingEnableInstancing`, `BackendUnsupported`, `OcclusionStubActive`, `InvalidSource`, `MeshDestroyed`, `MaterialDestroyed`. |
| `IBatchInstanceSource` | Strategy interface for per-instance data: `Count`, `TryGetNativeArrayView`, `TryGetComputeBuffer`, `OnTickBegin`. |
| `NativeArrayInstanceSource` / `ComputeBufferInstanceSource` / `TransformArrayInstanceSource` | Three built-in sources. `TransformArrayInstanceSource` is a **migration bridge** — not the recommended long-term API. |
| `MeshInstanceData` | Sequential-layout struct (80 B = `float4x4` + `float4`). Documented layout for `ComputeBuffer` authoring on the indirect backend. |
| `BatchRenderingFeature` | URP `ScriptableRendererFeature` subclass. Opt-in: add to a renderer asset to make `participatesInRenderGraph = true` batches execute inside a RenderGraph pass at `injectionPoint` (default `AfterRenderingOpaques`). |

---

## Backend Selection Guide

| Instance count | Data location | Recommended backend | Notes |
|---|---|---|---|
| 100–5,000 | CPU (`NativeArray`) | **Classic** | `Graphics.RenderMeshInstanced` chunked at 1023. |
| 5,000–50,000 | CPU (`NativeArray`) | **Classic** (chunked) — or **Indirect** if CPU upload bottlenecks | Classic may still win on desktop; profile. |
| 5,000–500,000 | GPU (`ComputeBuffer`) | **Indirect** | One `Graphics.RenderMeshIndirect` per chunk; service writes culled count into args buffer. |
| Any | Consumer-owned vertex pipeline | **Procedural** | `Graphics.DrawProceduralIndirect` pass-through; consumer owns args buffer when `culling = None`. |
| Any on WebGL 2 / OpenGL ES 3.0 | CPU | **Classic** (only viable) | Indirect / Procedural require `supportsComputeShaders + supportsIndirectArgumentsBuffer`. |

`MeshInstanceData` layout for the indirect backend's `ComputeBuffer`:

| Offset | Field | Size |
|---|---|---|
| 0 | `LocalToWorld` (`float4x4`) | 64 B |
| 64 | `PerInstanceColor` (`float4`) — reserved, default white in Phase 11 | 16 B |
| Total stride | | 80 B |

---

## Culling Pipeline

1. **Frustum cull** (default on) — sphere-vs-6-planes per instance. Burst-compiled `IJobParallelFor`, batch size 64, `FloatMode.Fast`. Mesh bounds projected through `LocalToWorld` to a world-space bounding sphere; conservative against the active camera's frustum planes (`GeometryUtility.CalculateFrustumPlanes`).
2. **Distance cull** (opt-in via `CullingPolicy.distance.enabled = true`) — sq-distance from camera position threshold check, chained after frustum cull in a second Burst `IJobParallelFor`.
3. **Occlusion cull** — stub-only in Phase 11. Setting `CullingPolicy.occlusion = true` emits a one-shot "not yet implemented" warning per batch; the flag has no rendering effect.

Per-camera tick: `PFound.LoopScheduler` BeforeRender → service enumerates `Camera.allCameras` → per camera per registered batch run cull → dispatch backend. Multi-camera correct; no temporal coherence (every camera-frame is fresh).

---

## RenderGraph Integration (`BatchRenderingFeature`)

Default batches use direct `Graphics.RenderMesh*` calls outside the URP render graph. Set `BatchRenderingBatch.participatesInRenderGraph = true` AND add `BatchRenderingFeature` to your URP renderer asset to route the draw through a RenderGraph pass at `injectionPoint` (default `AfterRenderingOpaques`).

Common injection-point choices:

| Use case | Injection point |
|---|---|
| Foliage / units / debris (opaque, depth-aware) | `AfterRenderingOpaques` (default) |
| Glass / transparent mesh fields | `BeforeRenderingTransparents` |
| Diagnostic overlay batches downstream of post | `AfterRenderingPostProcessing` |

The feature does NOT own the service — call `feature.AttachService(service)` from the consumer's bootstrap; `feature.DetachService()` in the matching shutdown hook.

---

## Owner-Managed Registration (CODING-STYLE.md §8)

Per the framework-wide golden rule, `BatchRenderingService.RegisterBatch(...) → IBatchHandle` follows owner-managed lifecycle:

- The consumer who registered the batch calls `handle.Dispose()` at the matching close / unload / disable hook in their own code.
- The service does **NOT** subscribe to `SceneManager.sceneUnloaded`.
- The service does **NOT** auto-clear batches when their referenced `Mesh` / `Material` / underlying source is invalidated externally.
- The service emits a **one-shot warning** + degrades the batch to no-op when it detects an invalid reference on a tick (e.g., destroyed `Mesh`, disposed `NativeArray`). The warning is a debugging aid pointing at the missing deregister call — it is NOT an auto-fix.

---

## Quick Start

See `specs/023-batch-rendering/quickstart.md` for the canonical six-example walkthrough (classic, indirect, procedural, distance-cull foliage, Transform[] bridge, RenderGraph integration) and an EditMode test snippet.

Bare-minimum classic batch:

```csharp
var service = new BatchRenderingService();
var transforms = new NativeArray<float4x4>(1000, Allocator.Persistent);
// ... populate transforms ...
var handle = service.RegisterBatch(new BatchRenderingBatch
{
    mesh = cubeMesh,
    material = cubeMaterial,    // enableInstancing = true in importer
    source = new NativeArrayInstanceSource(transforms),
    backend = BackendKind.Classic,
});
// ... later, owner pins the close side ...
handle.Dispose();
service.Dispose();
transforms.Dispose();
```

---

## Known Gaps

- **Occlusion culling** — stub-only (flag + warning). Implementation deferred.
- **Skinned mesh instancing** — out of scope.
- **`BatchRendererGroup` (BRG)** — out of scope; separate, heavier path.
- **Multi-chunk indirect** — Phase 11 ships single-chunk-per-batch indirect. Paginated multi-chunk indirect (e.g., 500k instances split across 50 args entries) deferred to 11.x.
- **SoA SIMD frustum cull** — Phase 11 uses AoS sphere-vs-plane. A 2-3× faster SoA `float4`-batched variant is deferred to 11.x behind a `[CullPolicy.SoaAccel]` flag.
- **WorldSpace `ComputeBufferInstanceSource` material clone** — same gap pattern as `Particles.Image` Phase 9.5; deferred.

---

## Dependencies

- `PFound.Render.Core` (sibling — `RenderFeatureBase`, `RenderPassBase`).
- `PFound.LoopScheduler` (BeforeRender tick).
- `Unity.RenderPipelines.Universal.Runtime` + `Unity.RenderPipelines.Core.Runtime` (URP feature).
- `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`, `Unity.Jobs` (cull jobs + native containers).
- **Intentionally no** `PFound.DependencyContainer` dependency (consumer-side wiring per spec FR-002 / FR-004).
