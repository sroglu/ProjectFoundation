# Render.ShaderWarmup — boot shader pre-warm

A time-sliced shader pre-warm controller that wraps Unity's
`ShaderVariantCollection.WarmUpProgressively(int)` across one or more collections so a game can
compile its shader variants at boot / loading screens without a single-frame hitch. Ticks once
per frame via `PFound.LoopScheduler` BeforeRender; pipeline-agnostic (Built-in / URP / HDRP).

---

## Scope

**ShaderWarmup is for:**
- Progressive, budgeted warmup of one or more `ShaderVariantCollection`s at boot, spread across
  frames (per-tick variant budget), with a progress-observable session.

**ShaderWarmup is NOT for:**
- Authoring the variant collections (that is a build/editor concern).
- Runtime shader loading — it only warms already-referenced variants.

---

## Public Surface

| Type | Role |
|---|---|
| `IShaderWarmupController` / `ShaderWarmupController` | Orchestrator (`IDisposable`). `BeginSession(params WarmupBatch[])` / `BeginSession(IEnumerable<WarmupBatch>) → IShaderWarmupSession`; `DiagnosticMode` (toggles `GraphicsSettings.logWhenShaderIsCompiled`, restored on dispose); `ActiveSessions` (auto-pruned live view). Ticks via `PFound.LoopScheduler` BeforeRender. |
| `IShaderWarmupSession` / (internal) `WarmupSession` | Per-run handle: progress / completion observation. |
| `WarmupBatch` (`readonly struct`) | `(ShaderVariantCollection Collection, int BatchSize)` — per-tick variant budget (≥ 1). Eagerly validated: null collection → `ArgumentNullException`, `BatchSize < 1` → `ArgumentOutOfRangeException`. |
| `RenderShaderWarmupRegistration` (static) | `Register(DependencyContainer registry) → IShaderWarmupController` — constructs + registers the controller. |

---

## Architecture

- `BeginSession` validates its batches eagerly (invalid batches never reach a session); an empty
  batch list yields an already-complete session.
- Each frame the controller advances every active session by its per-batch budget via
  `WarmUpProgressively(BatchSize)`, prunes completed sessions from `ActiveSessions`, and stops
  ticking when idle. Per-frame tick registered against `PFound.LoopScheduler` BeforeRender and
  deregistered on `Dispose`.
- `DiagnosticMode` captures and restores the original
  `GraphicsSettings.logWhenShaderIsCompiled` value around its window.
- **Owner-managed** — caller owns the controller (`Dispose` restores diagnostic state).

---

## Dependencies

- `PFound.Render.Core` — shared render infrastructure.
- `PFound.DependencyContainer` — registration helper.
- `PFound.LoopScheduler` — BeforeRender per-frame tick.

---

## Limitations / Known Gaps

- Warms only variants present in the supplied `ShaderVariantCollection`s.
- Main-thread only; progress granularity is per tick / per batch budget.

---

## Related

- `Render/MODULE.md` — parent index.
