# Render.UIShapes — SDF UI shapes — Phase 12

A signed-distance-field (SDF) shader + support code for crisp, resolution-independent UI shapes
(rounded rects, circles, capsules, …) with layered effects (fill/gradient, outline, shadow,
noise, banding, dots). Shapes stay sharp at any RectTransform size — no 9-slice sprites. Includes
a runtime size-sync component, a material-property/keyword helper layer, and an editor bake
toolchain that flattens a configured material into a baked texture when desired.

---

## Scope

**UIShapes is for:**
- Drawing UI shapes analytically in a fragment shader (`Render/UI/Shape`) via SDF, so the
  silhouette + outline + shadow stay crisp under arbitrary scaling.
- Composing per-material effect keywords (fill / gradient / outline / shadow / noise / banding /
  dots) from strongly-typed helpers rather than magic strings.
- Optionally **baking** a configured shape material to a texture (editor tool) for cases that
  prefer a static sprite.

**UIShapes is NOT for:**
- Arbitrary vector art — it is a fixed catalogue of parametric shapes.
- Text (use the text stack) or mesh-based UI.

---

## Public Surface

### Runtime (`PFound.Render.UIShapes`)

| Type | Role |
|---|---|
| `UIShapeSizeSync` (`MonoBehaviour`, `[ExecuteAlways]`) | Auto-syncs `_QuadSize` / `_RectSize` shader properties from the `RectTransform` size (with an inset `Margin` for outline/shadow overshoot). Writes to the Graphic's active material — clone the material for per-instance sizing. |
| `ShapeType` (enum) | The shape catalogue. |
| `EffectMask` / `GradientMode` / `NoiseMode` (enums) | Effect-layer selectors. |
| `UIShapeMaterialProperties` (static) | Cached shader property IDs / setters for the `Render/UI/Shape` material. |
| `UIShapeShaderKeywords` (static) | The shader keyword string constants. |
| `UIShapeEffectComposition` (static) | Enables/disables the correct keyword set for a chosen effect composition. |

Shaders in `Runtime/Shaders/UIShape.shader` (`Render/UI/Shape`) with `Runtime/UIShapeSDF.hlsl`,
`UIShapeNoise.hlsl` (clean-room noise: Perlin/Worley from public algorithm descriptions),
`UIShapeEffects.hlsl`, plus a default `Runtime/UIShape.mat`.

### Editor (`PFound.Render.UIShapes.Editor`)

| Type | Role |
|---|---|
| `UIShapeMaterialInspector` | Custom inspector for the shape material (foldout groups per effect). |
| `UIShapeBakeWindow` | Editor window driving a bake of a configured shape material to a texture. |
| `UIShapeBakeService` (static) | The bake implementation. |
| `UIShapeFilenameValidator` (static) | Validates output filenames for the bake. |
| `BakeSettings` (struct) + `BakeColorSpace` / `BakeTargetType` (enums) | Bake configuration. |

---

## Architecture

- The shader evaluates an SDF for the selected `ShapeType`, then layers effects gated by shader
  keywords. The runtime helpers (`UIShapeMaterialProperties` / `UIShapeShaderKeywords` /
  `UIShapeEffectComposition`) exist so C# callers set properties + keywords consistently instead
  of hand-typing strings.
- `UIShapeSizeSync` keeps the quad/rect uniforms in step with the `RectTransform` on enable and
  on every dimension change, so the SDF is evaluated in the correct pixel space.
- The editor bake path is optional — it flattens a live material into a texture for projects that
  want a static asset instead of the runtime shader.

---

## Dependencies

- Runtime: `PFound.Render.Core`.
- Editor: `PFound.Render.UIShapes` + `PFound.Render.Core` (Editor-only assembly).

---

## Limitations / Known Gaps

- Fixed shape catalogue (`ShapeType`); not arbitrary vector paths.
- Shared-material sizing is last-writer-wins — clone the material per Graphic for independent
  sizes (standard Unity pattern).
- Main-thread only.

---

## Related

- `Render/MODULE.md` — parent index.
- `Render/Core/MODULE.md` — shared HLSL + material loading conventions.
