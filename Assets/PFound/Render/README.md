# PFound.Render

Rendering building blocks for Unity URP. Four independent sub-modules, each its own assembly — take
only what you need.

## Sub-modules

| Sub-module | Assembly | What it is |
|---|---|---|
| [Core](Core/MODULE.md) | `PFound.Render.Core` | Render-texture pool, global shader parameter manager, URP RenderGraph feature/pass base classes, shared HLSL includes. |
| [BatchRendering](BatchRendering/MODULE.md) | `PFound.Render.BatchRendering` | Burst frustum/distance-culled GPU instancing service (classic / indirect / procedural backends). |
| [RenderContext](RenderContext/MODULE.md) | `PFound.Render.RenderContext` | Off-screen camera → `RenderTexture` bound to a `RawImage` / `MeshRenderer` / UI Toolkit element. |
| [Utilities](Utilities/MODULE.md) | `PFound.Render.Utilities` | Texture creation, GPU resize/blit, readback, strip-gated render debug helpers. |

## Docs

Deep reference: **[MODULE.md](MODULE.md)** — subsystem scope, sub-module map, asmdef dependency model,
GameSpecific hook, per-sub-module setup/wiring, and verification. Per-sub-module depth lives in
[Core](Core/MODULE.md) · [BatchRendering](BatchRendering/MODULE.md) ·
[RenderContext](RenderContext/MODULE.md) · [Utilities](Utilities/MODULE.md).

## Dependencies

Core and Utilities are independent (Utilities needs no URP or Core); BatchRendering and RenderContext
build on Core + `PFound.LoopScheduler` (RenderContext optionally uses `PFound.DependencyContainer`) —
see [MODULE.md](MODULE.md) for the full per-assembly table.
