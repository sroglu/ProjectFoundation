# Render.Effects.Outline — Phase 4

A URP RenderGraph depth-edge outline effect driven by a `VolumeComponent`, with an optional
priority-queue request service so multiple consumers can each request an outline and the
highest-priority spec wins. Edges are detected with a 4-tap Roberts-cross depth kernel.

---

## Scope

**Outline is for:**
- Full-screen silhouette / depth-edge outlining as a URP Renderer Feature
  (`OutlineRenderFeature`) configured through an `OutlineVolumeComponent`.
- Arbitrating multiple simultaneous outline requesters via `IOutlineRequestService`
  (`IOutlineTicket` per caller; top-priority spec resolves onto the volume).

**Outline is NOT for:**
- Per-object outlines with individual colors — this is a full-screen depth-edge pass.
- Coexisting with the `PostProcess` `OutlineAdapter` on the same volume (last writer wins).

---

## Public Surface

| Type | Role |
|---|---|
| `OutlineRenderFeature` (`RenderFeatureBase`) | URP RendererFeature; add to the Universal Renderer asset. |
| `OutlinePass` / `OutlinePassData` | RenderGraph pass: samples camera depth, 4-tap Roberts-cross gradient, thresholds into an outline mask, composites. |
| `OutlineVolumeComponent` | The `VolumeComponent` (Enable, Strength, color/thickness knobs). |
| `OutlineSpec` (`readonly struct`) | Immutable request payload. |
| `IOutlineRequestService` / `OutlineRequestService` | Priority-queue request arbitration. `Request(int priority, OutlineSpec) → IOutlineTicket`; `ActiveCount`; `IDisposable`. Same-priority collision throws. |
| `IOutlineTicket` / (internal) `OutlineTicket` | Owner-managed handle: `Priority`, `Current`, `IsActive`, `UpdateSpec(spec)`, `Dispose()`. |

---

## Architecture

- Builds on `PFound.Render.Core` (feature/pass bases, RT pool). Shaders in `Shaders/`
  (`Outline.shader` + `Outline.hlsl`). The Roberts-cross kernel samples four diagonal-neighbour
  depth taps — it catches diagonal silhouettes a cardinal-only Laplacian misses, at roughly half
  the cost of an 8-tap Sobel.
- `OutlineRequestService` is backed by `PFound.Collections.PriorityQueue<int, OutlineTicket>`; it
  re-resolves the volume to the `Max` ticket on each queue change and disables it when empty.
- **Owner-managed lifecycle** — caller owns each ticket and the service.

---

## Dependencies

- `PFound.Render.Core` — feature/pass base classes + RT pool.
- `PFound.Collections` — `PriorityQueue<TKey, TValue>`.
- URP `Universal.Runtime` + `Core.Runtime`.

---

## Limitations / Known Gaps

- Depth-edge only; needs a depth texture. No per-object color.
- Mutually exclusive with the PostProcess `OutlineAdapter` on the same volume.
- Main-thread only; RenderGraph-only.

---

## Related

- `Render/MODULE.md` — parent index.
- `Render/Core/MODULE.md` — feature/pass bases this builds on.
- `Render/PostProcess/MODULE.md` — the alternative adapter-stack path.
