# Render.Effects.Overdraw — debug view

A developer-only URP Renderer Feature that visualizes per-pixel overdraw as a color heatmap.
Each fragment adds a constant contribution into an additive HDR accumulator; a composite pass
maps the accumulated value through a threshold→color ramp. **Stripped from release player
builds** — the pass setup is gated behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

---

## Scope

**Overdraw is for:**
- Diagnosing overdraw hotspots (transparent stacking, particle fill) during development —
  toggled per URP Renderer asset in the inspector.

**Overdraw is NOT for:**
- Shipping in release players (it strips itself).
- Any gameplay-visible effect — it is a diagnostic overlay only.

---

## Public Surface

| Type | Role |
|---|---|
| `OverdrawRenderFeature` (`RenderFeatureBase`, `[Serializable]`) | URP RendererFeature. Inspector knobs: `_enable`, `_injectionPoint` (`RenderPassEvent`, default `BeforeRenderingPostProcessing`), `_contributionScalar` (default `0.0625` = 1/16, saturates at 16 contributions), `_thresholds`. `DefaultThresholds()` (static) returns the 4-tier ramp (green@1, yellow@3, orange@5, red@8). Zero per-frame cost when disabled. |
| `OverdrawPass` / `OverdrawPassData` | RenderGraph pass: additive accumulation + heatmap composite. |
| `OverdrawThresholdEntry` (`struct`) | One `(threshold, color)` tier; up to 8 tiers. |

---

## Architecture

- Builds on `PFound.Render.Core` (`RenderFeatureBase`, `RenderTexturePool`, `LoadMaterial`).
  Shaders in `Shaders/` (`Overdraw.shader` — `Hidden/Render/Overdraw` — + `Overdraw.hlsl`).
- The feature's `OnCreate` body is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so no pass
  is created (and the material is not loaded) in release players even if the asset is present.
- `DefaultThresholds()` is `static` so EditMode tests can validate the ramp contract without
  instantiating a renderer feature.

---

## Dependencies

- `PFound.Render.Core` — feature/pass bases + RT pool + material loader.
- URP `Universal.Runtime` + `Core.Runtime`.

---

## Limitations / Known Gaps

- Editor / development-build only — no visualization in release players (by design).
- Full-screen heatmap; no per-object breakdown.
- Main-thread only; RenderGraph-only.

---

## Related

- `Render/MODULE.md` — parent index.
- `Render/Core/MODULE.md` — feature/pass bases + RT pool this builds on.
- `Render/Utilities/MODULE.md` — sibling strip-gated debug helpers (`RenderDebugTools`).
