# Render.PostProcess — post-process stack

A small orchestration service that lets gameplay/UI code issue **typed** post-process requests
(`BlurRequest`, `OutlineRequest`, …) and have them routed to per-effect adapters that resolve a
priority-ranked stack onto the underlying `VolumeComponent`s. The service ticks all adapters once
per frame via `PFound.LoopScheduler` BeforeRender. Ships with two built-in adapters (Blur +
Outline); callers can register additional adapters.

> **ColorGrading adapter intentionally not shipped.** URP's built-in `ColorLookup` covers LUT
> color grading, so the legacy ColorGrading effect + its PostProcess adapter/request are a
> recorded sanctioned drop. The stack is otherwise complete and extensible via `extraAdapters`.

---

## Scope

**PostProcess is for:**
- A single front door for stackable full-screen effects: `IRenderPostProcess.Request<TRequest>(...)`
  returns a ticket; disposing it removes the request on the next tick.
- Blend-policy arbitration per effect (`HighestPriorityWins`, `WeightedSum`, `LatestWins`).

**PostProcess is NOT for:**
- Being used *together with* the `Effects.Blur` / `Effects.Outline` request services on the same
  volume — both paths write the same `VolumeComponent` (last writer wins). Pick one path.
- Per-object effects.

---

## Public Surface

| Type | Role |
|---|---|
| `IRenderPostProcess` / `RenderPostProcessService` | Orchestrator. `Request<TRequest>(TRequest request, int priority = 0) → IRenderPostProcessTicket`. Ticks adapters via `PFound.LoopScheduler` BeforeRender. Throws if no adapter is registered for `TRequest`. |
| `IRenderPostProcessTicket` / `RenderPostProcessTicket` | Owner-managed handle; dispose to drop the request from the stack. |
| `RenderPostProcessRegistration` (static) | `Register(DependencyContainer registry, RenderPostProcessOptions options = null, params IRenderPostProcessAdapter[] extraAdapters) → IRenderPostProcess`. Wires the built-in Blur + Outline adapters + any extras and registers the service instance. |
| `RenderPostProcessOptions` | Per-adapter blend policies (`BlurPolicy`, `OutlinePolicy`), `MaxConcurrentRequestsPerEffect` (default 256), `WarnOnMissingVolumeComponent`. `Default`. |
| `PostProcessBlendPolicy` (enum) | `HighestPriorityWins`, `WeightedSum`, `LatestWins`. |
| `IRenderPostProcessAdapter` / `IRenderPostProcessAdapter<TRequest>` / `RenderPostProcessAdapterBase<TRequest>` / `RenderPostProcessAdapterCore` | Adapter contract + base class. Custom adapters extend `RenderPostProcessAdapterBase<TRequest>`. |
| `BlurAdapter` / `OutlineAdapter` | Built-in adapters mapping `BlurRequest` / `OutlineRequest` onto the Blur / Outline volume components. |
| `BlurRequest` / `OutlineRequest` (`readonly struct`) | Typed request payloads. |
| `ActiveRequest<TRequest>` (`readonly struct`) | One live entry in an adapter's stack (request + priority + fade state). |

---

## Architecture

- The service resolves each effect's stack via the adapter's declared `PostProcessBlendPolicy`
  and writes the winning parameters onto the corresponding `VolumeComponent`. Custom adapters
  must derive from `RenderPostProcessAdapterBase<TRequest>` — a bare `IRenderPostProcessAdapter<TRequest>`
  implementation is rejected.
- Per-frame ticking is registered against `PFound.LoopScheduler.RegisterBeforeRenderLoop(...)` and
  deregistered on `Dispose`.
- Registration is wired through `PFound.DependencyContainer` (`RegisterInstance<IRenderPostProcess>`),
  but the container is only a registration convenience — the service itself is `new`-able.

---

## Dependencies

- `PFound.Render.Core` — shared render infrastructure.
- `PFound.Render.Effects.Blur` / `PFound.Render.Effects.Outline` — the built-in adapters target
  their volume components.
- `PFound.DependencyContainer` — registration helper.
- `PFound.LoopScheduler` — BeforeRender per-frame tick.
- URP `Universal.Runtime` + `Core.Runtime`.

---

## Limitations / Known Gaps

- **No ColorGrading adapter** — sanctioned drop (URP built-in `ColorLookup`); register a custom
  adapter via `extraAdapters` if a project needs one.
- Mutually exclusive with the standalone Blur/Outline request services on shared volumes.
- Main-thread only.

---

## Related

- `Render/MODULE.md` — parent index.
- `Render/Effects.Blur/MODULE.md`, `Render/Effects.Outline/MODULE.md` — the effects the built-in
  adapters drive (and the mutually-exclusive standalone request-service path).
