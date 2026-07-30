# Render.Utilities — Phase 1

Small, focused render-adjacent helpers that don't fit inside a single render pass and don't require URP. Two surfaces: a `Graphics.Blit`-based texture downscaler with a disposable handle, and a set of `[Conditional]`-gated debug-draw helpers that strip in release builds.

> **Status:** v0.1.0 — shipped 2026-05-15. See `Render/CHANGELOG.md` (entry `[0.1.0]`).

---

## Scope

**Utilities is for:**
- Downscaling a `Texture2D` so neither dimension exceeds a clamp, with a uniform disposable-handle calling convention (`using var h = TextureResizer.Resize(src, max);`) regardless of whether the path produced a fresh texture or a pass-through.
- Drawing world-space debug primitives (lines, rays, boxes, arrows) from gameplay code that **cannot ship in release players** — call sites are stripped at compile time outside `UNITY_EDITOR` / `DEVELOPMENT_BUILD`.

**Utilities is NOT for:**
- URP / RenderGraph authoring — that's `PFound.Render.Core`'s job. Utilities is **independent of Core** by design (FR-003 in the Phase 1 spec; verified by the asmdef having only `Unity.Mathematics` as a reference — no URP, no Core).
- Replacement for Unity's native debug-draw — `RenderDebugTools` is a strip-safe wrapper, not a faster / fancier draw system. It composes on top of `UnityEngine.Debug.DrawLine` / `DrawRay`.
- Allocation-free downscaling — the blit path **must** allocate one `Texture2D` to hold the readback. Only the pass-through path is zero-allocation (asserted by `TextureResizerZeroAllocTests`).
- Cropping, padding, or format conversion — `TextureResizer` is downscale-only (max-dimension clamp; aspect ratio preserved).
- Generic `Material` / screen-space helpers — those live in the sibling `PFound.Utilities.RenderingTools` module under the `Utilities` submodule.
- GameSpecific assets — Phase 1 produces no runtime assets (Constitution III).

---

## Public Surface

| Type | Role |
|---|---|
| `TextureResizer` (`Runtime/`, `static`) | `Resize(Texture2D source, int maxDimension) → TextureResizeHandle`. Aspect-preserving downscale clamp via `Graphics.Blit` + `Texture2D.ReadPixels`. Argument validation: `source` non-null, `maxDimension > 0`. Pass-through when `max(width, height) ≤ maxDimension`. |
| `TextureResizeHandle` (`Runtime/`, `readonly struct`, `IDisposable`) | Uniform return type — owner-managed via `OwnsTexture` flag. Pass-through (no copy) returns `OwnsTexture = false` → `Dispose()` is a no-op. Downscale path returns `OwnsTexture = true` → `Dispose()` calls `Object.Destroy` (Play mode) or `Object.DestroyImmediate` (EditMode). Idempotent. `default(TextureResizeHandle)` is dispose-safe. |
| `RenderDebugTools` (`Runtime/`, `static`) | Strip-gated debug draw. Every public method carries `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` — call sites are removed at compile time in release player builds (SC-009). API: `DrawWorldLine`, `DrawWorldRay`, `DrawWorldBox`, `DrawWorldArrow`. |

---

## Architecture

### `TextureResizer.Resize` flow

```text
        ┌─ source.width / source.height ≤ maxDimension? ─┐
        │                                                │
       YES                                              NO
        │                                                │
        ▼                                                ▼
  new TextureResizeHandle             RenderTexture.GetTemporary(dstW, dstH, 0, ARGB32)
    (source, ownsTexture=false)         Graphics.Blit(source, rt)
    [pass-through — no GPU work]        RenderTexture.active = rt
                                        new Texture2D(dstW, dstH, source.format, false)
                                        ReadPixels(...) → Apply(no-mipmap, keep-readable)
                                        ReleaseTemporary(rt) + restore prev active
                                        new TextureResizeHandle(result, ownsTexture=true)
```

- **Aspect preserved**. Scale factor is `maxDimension / max(width, height)`; both dimensions are `Mathf.RoundToInt`-rounded and floored at `1` so neither collapses to zero.
- **Pass-through is zero-alloc.** Asserted by `TextureResizerZeroAllocTests` (PlayMode, 60-frame Profiler Recorder soft-threshold).
- **Downscale path allocates one `Texture2D`** — this is the unavoidable cost of returning a CPU-readable result. Documented in `Render/MODULE.md` Limitations: *"TextureResizer blit-path result texture allocation is unavoidable — only the pass-through path is asserted zero-allocation."*
- **No `RenderTexturePool` dependency.** Uses `RenderTexture.GetTemporary` directly so the asmdef stays independent of Core (FR-003).
- **Active-RT restore is guarded** — `prevActive` is captured before `Graphics.Blit` and restored in a `finally` block so an exception inside `ReadPixels` doesn't leak the active RT state.

### `TextureResizeHandle` disposal contract

The handle exists to give callers a **uniform calling convention**: always `using` (or otherwise `Dispose()`) the handle, regardless of whether the underlying `Texture` is shared with the caller or freshly-allocated.

- `OwnsTexture = false` (pass-through): `Dispose` is a no-op. Caller's original `Texture2D` is preserved untouched.
- `OwnsTexture = true` (downscale): `Dispose` destroys the texture via the Play/EditMode-aware path. Idempotent — calling `Dispose` twice (or on a `default` handle) is safe; the second call's `Texture == null` short-circuits the destroy.

### `RenderDebugTools` strip discipline

Every public method uses **two** `[Conditional]` attributes (`"UNITY_EDITOR"` + `"DEVELOPMENT_BUILD"`) — both gating the call from being emitted at the caller's site in release player builds. Inside the methods, calls forward to `UnityEngine.Debug.DrawLine` / `DrawRay`. The composite drawers (`DrawWorldBox`, `DrawWorldArrow`) construct their primitives entirely with `Vector3` math + `Quaternion` rotation — no GameObject allocation, no Gizmos dependency.

| Method | Signature | Behaviour |
|---|---|---|
| `DrawWorldLine` | `(Vector3 from, Vector3 to, Color color, float duration = 0f)` | Single `Debug.DrawLine`. |
| `DrawWorldRay` | `(Vector3 origin, Vector3 direction, Color color, float duration = 0f)` | Single `Debug.DrawRay`. |
| `DrawWorldBox` | `(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration = 0f)` | 12-edge wireframe using 8 corners precomputed from `center + rotation * (±h.x, ±h.y, ±h.z)` where `h = size * 0.5f`. 12 `DrawLine` calls. |
| `DrawWorldArrow` | `(Vector3 from, Vector3 to, Color color, float headSize = 0.25f, float duration = 0f)` | Body line + two head segments via `Quaternion.LookRotation(dir) * Quaternion.Euler(0, ±150f, 0) * Vector3.forward * headSize`. Early-return when `dir.magnitude < 1e-4f`. |

**Critical detail:** `[Conditional]` strips the **call site**, not the body. So a release build still contains the (now-unreachable) method bodies. The strip works only if call sites consume no side effects beyond the conditional methods — passing a pre-computed `Vector3` that would otherwise be unused is the standard pattern.

**Duration semantics** follow Unity's `Debug.DrawLine` contract — `0f` means "one frame", positive values persist on the Scene-view gizmo overlay for the given duration.

### Asmdef independence (FR-003)

The Phase 1 spec requires `PFound.Render.Utilities` to be consumable **without** pulling in `PFound.Render.Core`, URP, or RenderGraph dependencies. Verified by the asmdef:

```json
{
  "name": "PFound.Render.Utilities",
  "rootNamespace": "PFound.Render.Utilities",
  "references": [ "Unity.Mathematics" ],
  ...
}
```

Zero URP references, zero Core references. A consumer project can take only this asmdef (without `PFound.Render.Core`) and use `TextureResizer` + `RenderDebugTools` standalone.

---

## Owner-Managed Conventions

`TextureResizer` returns an `IDisposable` handle — callers own it and **must** `Dispose` it. The owner-managed contract is the same shape as Phase 11 `BatchRendering` and Phase 10 `RenderContext`:

- `using var h = TextureResizer.Resize(src, 256);` is the canonical call.
- The library does **not** track handles internally — no leak detection, no auto-disposal.
- A leaked downscale handle leaks one `Texture2D` until the next domain reload.

`RenderDebugTools` has no allocations to manage; the strip-gated contract is the entire lifecycle.

---

## Tests

`PFound.Render.Tests` (EditMode, namespace `PFound.Render.Tests`):

- `TextureResizerMathTests` — argument validation (`null source` → `ArgumentNullException`; `maxDimension ≤ 0` → `ArgumentOutOfRangeException`); pass-through `OwnsTexture = false` semantics; aspect-preserving dimensions on the downscale path.
- `TextureResizeHandleTests` — pass-through `Dispose` is no-op + preserves source; `default(TextureResizeHandle).Dispose()` doesn't throw.
- `RenderDebugToolsSmokeTests` — `DrawWorldLine` / `DrawWorldBox` / `DrawWorldRay` / `DrawWorldArrow` smoke (no-throw). Visual rendering verification is documented as manual per SC-009 — the automated suite covers strip-safety + no-throw only.

`PFound.Render.Tests.PlayMode` (PlayMode):

- `TextureResizerRoundTripTests` — 256→64 downscale on a solid-color source: handle owns the result, dimensions match, pixel readback contains non-zero samples.
- `TextureResizerZeroAllocTests` — 60-frame zero-alloc on the pass-through path via the shared `ZeroAllocAssertions` helper.

---

## Known Gaps / Deferred

- **Downscale path allocates one `Texture2D` per call** — documented as unavoidable. Callers needing per-frame downscaling should pool the result themselves or switch to a `RenderTexture`-only flow.
- **No upscale path.** Pass-through is the only behaviour for `max(W, H) ≤ maxDimension` sources. Upscaling intentionally out of scope (better handled by sampler `filterMode` than CPU readback).
- **No format conversion.** Result inherits `source.format`. If you need a specific format on the downscale, convert separately.
- **No cropping / padding / aspect-correction.** Aspect is always preserved.
- **No `RenderTexture` overload.** Only `Texture2D → Texture2D`. A `RenderTexture` overload could land if a concrete consumer needs it.
- **`RenderDebugTools` is line-art only.** No filled-volume primitives (no shaded sphere, no filled box). Gizmos / custom mesh remain the path for filled debug visualizations.
- **`RenderDebugTools` body code is NOT stripped, only call sites.** Release builds contain unreachable bodies (acceptable cost — bodies are short and unreferenced). If body-strip ever becomes important, wrap the entire class in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- **Main-thread only.** Both surfaces assume the Unity main thread. No thread-safety guarantees.
- **No editor companion.** This asmdef has no `<Module>.Editor` sibling — Phase 1 didn't need one. Future helpers requiring editor-only surface should follow the established `<Module>.Editor/` sibling convention (see `Render/MODULE.md`).

---

## Dependencies

- `Unity.Mathematics` (asmdef reference) — pulled in as a low-cost helper but **not currently used** in the public surface (`Vector3` / `Mathf` from `UnityEngine` cover the Phase 1 needs). Reserved for future Burst-friendly variants.
- **Intentionally NOT** `PFound.Render.Core` — independence is a hard contract (FR-003).
- **Intentionally NOT** URP packages — the asmdef has zero URP refs; verified by `Utilities/PFound.Render.Utilities.asmdef`.

---

## Related

- `Render/MODULE.md` — top-level Render submodule index + phase roadmap. Limitations section calls out the `TextureResizer` blit-path allocation explicitly.
- `Render/Core/MODULE.md` — sibling Core asmdef (`RenderFeatureBase`, `RenderTexturePool`, `GlobalShaderParameterManager`, shared HLSL).
- `Render/CHANGELOG.md` — release notes; Phase 1 entry is `[0.1.0]` (2026-05-15).
- `specs/012-render-core-foundation/` — full Phase 1 spec; Utilities is part of the same feature.
- `Assets/PFound/Utilities/RenderingTools/` (separate submodule) — generic `Material` + screen-space helpers; distinct from this module's pipeline-aware focus.
