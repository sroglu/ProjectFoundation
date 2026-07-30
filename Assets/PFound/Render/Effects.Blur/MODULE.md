# Render.Effects.Blur — Phase 3

A URP RenderGraph blur effect driven by a `VolumeComponent`, plus an optional priority-queue
request service so multiple gameplay/UI consumers can each ask for blur and the highest-priority
request wins. Separable multi-tap Gaussian downsample with selectable output routing (screen
composite or a global texture for snapshot-style consumers).

---

## Scope

**Blur is for:**
- Full-screen blur as a URP Renderer Feature (`BlurRenderFeature`) configured through a
  `BlurStrengthVolumeComponent` on the active Volume Profile.
- Arbitrating multiple simultaneous blur requesters (menus, pause, focus overlays) via
  `IBlurRequestService` — each caller gets an `IBlurTicket`; the top-priority spec resolves onto
  the volume; releasing a ticket falls back to the next highest.

**Blur is NOT for:**
- Per-object / masked blur — this is a full-screen pass.
- Coexisting with the `PostProcess` `BlurAdapter` on the same volume — both write the same
  `VolumeComponent`, last writer wins. Pick the request-service path **or** the PostProcess path
  per project.

---

## Public Surface

| Type | Role |
|---|---|
| `BlurRenderFeature` (`RenderFeatureBase`) | URP RendererFeature; add to the Universal Renderer asset. Enqueues `BlurPass` when the volume is active. |
| `BlurPass` / `BlurPassData` | RenderGraph pass: separable N-tap Gaussian downsample + composite. |
| `BlurStrengthVolumeComponent` | The `VolumeComponent` (Enable, Strength, Downsample, Iterations, OutputMode override-quality knobs). |
| `BlurSpec` (`readonly struct`) | Immutable request payload: strength + optional downsample/iterations + output mode. |
| `BlurDownsample` / `BlurOutputMode` (enums) + their `VolumeParameter` wrappers | Quality tier and output routing (screen vs global texture). |
| `IBlurRequestService` / `BlurRequestService` | Priority-queue request arbitration over the volume. `Request(int priority, BlurSpec) → IBlurTicket`; `ActiveCount`; `IDisposable`. Same-priority collision throws. |
| `IBlurTicket` / (internal) `BlurTicket` | Owner-managed handle: `Priority`, `Current`, `IsActive`, `UpdateSpec(spec)`, `Dispose()` (removes from queue → next-highest takes over). |

---

## Architecture

- `BlurRenderFeature` / `BlurPass` build on `PFound.Render.Core` (`RenderFeatureBase` /
  `RenderPassBase`, `RenderTexturePool` for transient targets). Shaders live in `Shaders/`
  (`Blur.shader` + `Blur.hlsl`, a separable 5-tap Gaussian helper set).
- `BlurRequestService` is backed by `PFound.Collections.PriorityQueue<int, BlurTicket>`. On each
  queue change it re-resolves the volume to the `Max` (highest-priority) ticket's spec; when the
  queue empties it disables the volume. Ticket disposal removes the entry (by reference) and
  re-resolves.
- **Owner-managed lifecycle** — the caller owns each `IBlurTicket` and the `BlurRequestService`
  itself. Nothing subscribes to `SceneManager`.

---

## Dependencies

- `PFound.Render.Core` — feature/pass base classes + RT pool.
- `PFound.Collections` — `PriorityQueue<TKey, TValue>` for request arbitration.
- URP `Universal.Runtime` + `Core.Runtime`.

---

## Limitations / Known Gaps

- Full-screen only; no per-object masking.
- Mutually exclusive with the PostProcess `BlurAdapter` on the same volume.
- Main-thread only; RenderGraph-only (no Built-in / Compatibility Mode fallback).

---

## Related

- `Render/MODULE.md` — parent index.
- `Render/Core/MODULE.md` — feature/pass bases + RT pool this builds on.
- `Render/PostProcess/MODULE.md` — the alternative adapter-stack path (mutually exclusive on the volume).
