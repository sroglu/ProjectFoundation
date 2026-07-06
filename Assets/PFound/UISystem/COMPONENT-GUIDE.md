# UISystem Component Creation Guide

**Version**: 1.1.0 | **Updated**: 2026-04-11 | **Feature**: 006-m3-uisystem-overhaul

This guide is the authoritative reference for creating or modifying M3 components in UISystem. All rules are constitution-level unless marked as recommendations.

---

## 1. Mandatory Rules

These rules are enforced by constitution and must not be violated without an explicit entry in the plan's Complexity Tracking section.

| Rule | Enforcement |
|------|------------|
| **USS-Only Theming** — All visual properties resolved via USS `var(--m3-*)` | `grep style.color` in CI must return 0 |
| **Zero Cross-Assembly Dependencies** — No `PFound.*` references | `PFound.UISystem.asmdef` refs checked |
| **ScriptableObject Configuration** — No `static readonly` color/float constants | Grep check before merge |
| **Extend M3ComponentBase** — All new components must extend the base class | Code review gate |
| **USS Transition Durations** — All `transition:` durations use `var(--m3-motion-*)` tokens | USS lint check |
| **No DOTween in Runtime** — Use USS `transition` or `AnimationCurve` + `IVisualElementScheduler` | Asmdef deps check |
| **Material Symbols for Icons** — Use `MaterialSymbols` constants + `.m3-icon` CSS class, not Painter2D paths | Exception registry required for Painter2D icons |

---

## 2. USS-Only Theming Rule

### The Rule

**All visual properties (colors, opacity, background colors, border colors, text colors) MUST be resolved through USS custom properties.** Components MUST NOT assign theme colors via C# `style.color`, `style.backgroundColor`, or similar inline assignments.

### Why

The dual-source model (USS + inline C#) creates:
- Maintenance burden: two places to update when a color token changes
- Theme-switching bugs: USS updates instantly, C# updates only on next render or `RefreshThemeColors()` call
- Inconsistency: some properties animate via USS `transition`, C# assignments are instant

### How

**Correct approach** — let USS resolve the color:
```css
/* button.uss */
.m3-button--filled .m3-button__label {
    color: var(--m3-on-primary);
}
.m3-button--outlined .m3-button__label {
    color: var(--m3-primary);
}
```

No C# required. When theme switches, `light.uss` or `dark.uss` is swapped and USS re-resolves all `var(--m3-*)` values automatically.

**Wrong approach** — do NOT do this:
```csharp
// ❌ WRONG — inline C# color assignment
_label.style.color = new StyleColor(theme.GetColor(ColorRole.Primary));
```

### Documented Exceptions

These properties **cannot** be USS and must remain in C#. Each exception is justified below.

| Property | Component | Justification | Allowed In |
|----------|-----------|---------------|-----------|
| `M3Surface.StateOverlayOpacity` | All interactive | Overlay opacity is packed into the element's `Vertex.tint` byte 1 and consumed by the `EFFECT_M3_OVERLAYS_ON` shader path — clipped to the SDF shape automatically. CSS `opacity` affects the whole element including children, not just the overlay layer. | `StateLayerController.cs` only |
| `M3Surface.TonalOverlayOpacity` / `TonalOverlayColor` | Card (elevated), others | Opacity packs into `Vertex.tint` byte 3; color lives on the shared material uniforms (category-level theme swap). Cannot be expressed via USS. | `RefreshThemeColors()` only |
| `M3Surface.RippleCenter` / `RippleRadius` / `RippleAlpha` | All interactive | Per-frame values driving an expanding circle on a per-event material instance (reverts to shared material when alpha returns to 0). These change 60 times/second during a ripple — USS cannot express center-relative expanding animations. | `RippleElement.cs` only |
| `M3Surface.FillColorOverride` | Card, Toggle | Per-instance palette-resolved fill colour packed into `Vertex.tint` byte 0; fully batching-preserving. The override is used when the element needs a custom fill colour that cannot be expressed via USS alone (e.g., tonal surface colours computed from elevation). | Justified components only |
| `_floatingLabel.style.backgroundColor` (TextField) | M3TextField | The Outlined variant floating label needs a surface-colored background to create the "notch" effect where the label overlaps the border. This requires reading the panel's actual surface color at runtime, which `var(--m3-surface)` alone cannot guarantee for nesting scenarios. | M3TextField only |

**Adding a new exception**: Document it in this registry with justification before implementing.

---

## 3. Component Anatomy

All M3 components follow this visual tree structure:

```
ComponentRoot (extends M3ComponentBase)
├── _container : M3Surface       ← main visual, handles bg + shadow + outline + ripple
│   ├── RippleElement                 ← child of container, absolute positioned, pointer-none
│   └── [content children]           ← labels, icons, slots
└── [optional outer elements]        ← touch target wrappers, scrim overlays
```

### Naming Conventions

- Private fields: `_camelCase` (e.g., `_container`, `_label`, `_iconEl`)
- USS classes on container: `m3-[component]` (e.g., `m3-button`, `m3-card`)
- USS element classes: `m3-[component]__[element]` (e.g., `m3-button__label`, `m3-button__icon`)
- USS modifier classes: `m3-[component]--[modifier]` (e.g., `m3-button--filled`, `m3-button--large`)
- State classes added/removed by C#: `m3-[component]--active`, `m3-[component]--selected`, `m3-[component]--disabled`

### Required Files Per Component

| File | Location | Purpose |
|------|----------|---------|
| `M3[Name].cs` | `Runtime/Components/` | Component logic |
| `[name].uss` | `Styles/Components/` | Component styles |
| `M3[Name].uxml` | `UXML/` | Optional reusable template |
| `showcase/[Name]-overview.uxml` | `UXML/showcase/` | Showcase Overview tab |
| `showcase/[Name]-specs.uxml` | `UXML/showcase/` | Showcase Specs tab |
| `showcase/[Name]-guidelines.uxml` | `UXML/showcase/` | Showcase Guidelines tab |

---

## 4. M3ComponentBase Extension Pattern

### Basic Component Template

```csharp
using UnityEngine.UIElements;
using PFound.UISystem.Core;
using PFound.UISystem.Data;
using PFound.UISystem.ScriptableObjects;

namespace PFound.UISystem.Components
{
    [UxmlElement]
    public partial class M3MyComponent : M3ComponentBase
    {
        // ── UXML Attributes ──────────────────────────────────────────────
        private MyVariant _variant = MyVariant.Default;

        [UxmlAttribute]
        public MyVariant Variant
        {
            get => _variant;
            set { _variant = value; ApplyVariant(); }
        }

        // ── Visual Elements ──────────────────────────────────────────────
        private M3Surface _container;
        private Label _label;

        // ── Lifecycle ────────────────────────────────────────────────────
        public M3MyComponent()
        {
            AddToClassList("m3-mycomponent");
            BuildVisualTree();
        }

        protected override void BuildVisualTree()
        {
            _container = new M3Surface();
            _container.AddToClassList("m3-mycomponent__container");

            var ripple = new RippleElement();
            _container.Add(ripple);

            _label = new Label("Label");
            _label.AddToClassList("m3-mycomponent__label");
            _container.Add(_label);

            // Set up StateLayerController via base class
            InitStateLayer(_container, ripple);

            hierarchy.Add(_container);
        }

        protected override void ApplyTheme(ThemeData theme)
        {
            // ✅ CORRECT: Only set non-CSS properties (exceptions from registry)
            // _container.TonalOverlayColor = theme.GetColor(ColorRole.Primary); // only if elevated

            // ❌ WRONG: Do NOT assign colors here
            // _label.style.color = new StyleColor(theme.GetColor(ColorRole.OnSurface));
        }

        private void ApplyVariant()
        {
            RemoveFromClassList("m3-mycomponent--default");
            // ... add correct variant class
            AddToClassList($"m3-mycomponent--{_variant.ToString().ToLower()}");
        }
    }
}
```

### What M3ComponentBase Provides

- `InitStateLayer(M3Surface, RippleElement)` — attaches StateLayerController
- `StateLayer` property — access the attached controller
- Automatic `ThemeManager.OnThemeChanged` subscription on `AttachToPanelEvent`
- Automatic unsubscription on `DetachFromPanelEvent`
- Automatic `StateLayer.Detach()` on `DetachFromPanelEvent` and `StateLayer.Attach()` on re-attach
- Calls `RefreshThemeColors()` when theme changes (after USS is applied)
- Handles `Disabled` property toggling `.m3-disabled` class
- **SDF Disabled Color Freeze** — automatically freezes all child `M3Surface.FillColorOverride` when disabled, preventing USS `:hover` pseudo-class from altering the disabled appearance

### Disabled State: FreezeSDFColors

When `Disabled = true`, M3ComponentBase calls `OnDisabledChanged(true)` which recursively traverses the component tree and sets every child `M3Surface.FillColorOverride` to its current `resolvedStyle.backgroundColor`. This "freezes" the rendered colour — USS `:hover` or any other pseudo-class change cannot affect the shader output (which reads its palette index from `Vertex.tint`, frozen by the override).

When `Disabled = false`, `FillColorOverride` is cleared (`null`) and USS resumes control.

**Why this is needed:** M3Surface encodes its fill colour into `Vertex.tint` byte 0 (via the palette index) — `style.backgroundColor` IS that tint, written via `EncodeTint`. UI Toolkit applies `:hover` pseudo-class regardless of disabled state, `pickingMode`, or `SetEnabled(false)`. Without freezing, USS would overwrite the backgroundColor (and therefore the tint byte) on hover.

**Override for custom disabled colors:** Subclass `OnDisabledChanged(bool)` to apply M3-specific disabled palette instead of freezing current colors:

```csharp
protected override void OnDisabledChanged(bool disabled)
{
    if (disabled)
    {
        _track.FillColorOverride = new Color(onSurface.r, onSurface.g, onSurface.b, 0.12f);
        _thumb.FillColorOverride = themeSurface;
    }
    else
    {
        _track.FillColorOverride = null;
        _thumb.FillColorOverride = null;
    }
}
```

---

## 5. Icon Usage

### Rule: Use Material Symbols font, not Painter2D paths

For all standard M3 icons, use font glyphs via the `MaterialSymbols` constants class and `.m3-icon` CSS class.

```csharp
// ✅ CORRECT
var iconLabel = new Label(MaterialSymbols.Search);
iconLabel.AddToClassList("m3-icon");  // applies MaterialSymbols-Filled font, 24px, centered
```

```csharp
// ❌ WRONG — Painter2D hardcoded paths
protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
{
    var p = ctx.painter2D;
    p.MoveTo(new Vector2(12, 6));
    p.LineTo(new Vector2(18, 12));
    // ... 20 more lines for a search icon
}
```

### Available CSS Classes

| Class | Font | Use For |
|-------|------|---------|
| `.m3-icon` | MaterialSymbols-Filled | Standard filled icons (most components) |
| `.m3-icon-outlined` | MaterialSymbols-Standard | Outlined/unfilled icon variant |

### Decision Tree: Font vs Painter2D

```
Does the icon exist in Material Symbols codepoints file?
├── YES → Use MaterialSymbols constant + .m3-icon
└── NO → Is it a simple geometric shape (checkmark, dash, circle, line)?
    ├── YES → Use Painter2D in generateVisualContent (document in exception registry)
    └── NO → Request addition to codepoints or find closest alternative icon
```

**Components using Painter2D icons by exception (geometric only)**:
- `M3Checkbox` — checkmark (✓) and dash (—) are geometric shapes, not font icons
- `M3Surface` — shadow approximation, rounded rect fill, ripple circle

---

## 6. Animation Guidelines

### Decision Tree: USS Transition vs M3Animate vs C# Scheduler

```
Does the element use M3Surface (GPU SDF shader, palette-tinted fill)?
├── YES → M3Surface encodes its fill into Vertex.tint via style.backgroundColor.
│         USS transitions on background-color may cause visual issues (e.g. :hover on disabled).
│         Use M3Animate for color/geometry animation.
└── NO — Is it a plain VisualElement?
    ├── YES → Use USS `transition` in component's .uss file (preferred)
    └── Is it a continuous expanding animation from a specific point?
        ├── YES → Use C# IVisualElementScheduler (schedule.Execute + MarkDirtyRepaint)
        └── NO → Use M3Animate.Float() for custom easing
```

### M3Surface + USS Limitation

**Critical**: `M3Surface` uses `style.backgroundColor` as the carrier for `Vertex.tint` (palette index in byte 0; state/tonal opacity in bytes 1-3 via `EncodeTint`). USS `:hover` pseudo-class applies regardless of disabled state, `pickingMode`, or `SetEnabled(false)`. This means:

- **USS transitions on M3Surface `background-color` are unreliable** — hover can corrupt disabled appearance
- **Use `FillColorOverride` or `style.backgroundColor` (inline C#)** for M3Surface color control
- **USS transitions work correctly** on plain `VisualElement` (e.g., tab indicator opacity)

### M3Animate Utility (`Runtime/Core/M3Animate.cs`)

Lightweight schedule-based animation utility for M3 components. Uses `IVisualElementScheduler` (~60fps), **not DOTween**.

```csharp
// Single property animation (200ms ease-out cubic)
M3Animate.Float(owner, fromValue, toValue, 200f, currentValue =>
{
    _thumb.style.left = currentValue;
});

// Multi-property animation via normalized 0→1 progress
M3Animate.Float(owner, 0f, 1f, 200f, t =>
{
    _thumb.style.left = Mathf.Lerp(curLeft, targetLeft, t);
    _track.style.backgroundColor = Color.Lerp(curColor, targetColor, t);
    _track.OutlineThickness = Mathf.Lerp(curOutline, targetOutline, t);
});
```

**Easing**: Hardcoded ease-out cubic (`1 - (1-t)^3`). This is intentional — a single consistent easing curve across all M3 animations avoids per-component easing configuration overhead. The curve matches M3 "standard" motion.

**When to use M3Animate vs USS transition:**

| Scenario | Use |
|----------|-----|
| Plain VisualElement property change (opacity, position) | USS `transition` |
| M3Surface color or geometry animation | `M3Animate.Float()` |
| Ripple expansion, custom Painter2D animation | `IVisualElementScheduler` directly |

### USS Transition Animatable Properties (Unity 6.3)

✅ **Confirmed working on plain VisualElement**: `background-color`, `color`, `opacity`, `left`, `top`, `width`, `height`, `font-size`, `translate`, `scale`

❌ **Cannot transition**: CSS custom properties (`var(--m3-*)` values), computed layout properties, Painter2D content

⚠️ **Unreliable on M3Surface**: `background-color` (read via `resolvedStyle` at paint time — `:hover` can override)

### Transition Duration Tokens

**MANDATORY for USS**: Always use `var(--m3-motion-*)` tokens, never hardcoded ms values.

```css
/* ✅ CORRECT */
transition: opacity var(--m3-motion-duration-standard) ease-in-out;

/* ❌ WRONG */
transition: opacity 200ms ease-in-out;
```

**For M3Animate (C#)**: Hardcoded duration values are acceptable. M3Animate uses ms directly because C# cannot read USS custom property values at runtime.

| Token / Duration | Value | Use For |
|------------------|-------|---------|
| `--m3-motion-duration-short` / `100f` | 100ms | Checkbox, chip, radio, press feedback |
| `--m3-motion-duration-float` / `150f` | 150ms | TextField floating label |
| `--m3-motion-duration-standard` / `200f` | 200ms | Toggle, navigation bar, most state changes |
| `--m3-motion-duration-long` / `300f` | 300ms | Complex animations, ripple |

### State Layer Hover Token

For components without `StateLayerController` (e.g., `M3TabItem`), use the `--m3-state-hover` USS token for hover background:

```css
.m3-tab-item:hover {
    background-color: var(--m3-state-hover, rgba(28, 27, 31, 0.08));
}
```

This token is defined in both `light.uss` and `dark.uss` with theme-appropriate on-surface color at 8% opacity.

---

## 7. Typography Usage

### 15 M3 Type Roles

Apply by adding the USS class to a `Label` element:

```csharp
label.AddToClassList("m3-display-large");   // 57px, Regular
label.AddToClassList("m3-headline-medium"); // 28px, Regular
label.AddToClassList("m3-title-medium");    // 16px, Medium weight
label.AddToClassList("m3-body-large");      // 16px, Regular
label.AddToClassList("m3-label-small");     // 11px, Medium weight
```

Or in UXML:
```xml
<Label class="m3-title-large" text="Card Title" />
<Label class="m3-body-medium" text="Card body text goes here." />
```

### Role Selection Guide

| When to use | Role |
|-------------|------|
| Page title, hero text | Display Large/Medium/Small |
| Section headings | Headline Large/Medium/Small |
| Component titles, dialog headlines | Title Large |
| Field labels, chip labels, tab labels | Title Medium/Small |
| Body copy, description text | Body Large/Medium |
| Secondary body, caption | Body Small |
| Button labels, form labels | Label Large |
| Compact labels, badge counts | Label Medium/Small |

### Backward-Compatible Aliases (still valid)

| Old class | Maps to |
|-----------|---------|
| `.m3-display` | `.m3-display-large` |
| `.m3-headline` | `.m3-headline-large` |
| `.m3-title` | `.m3-title-large` |
| `.m3-body` | `.m3-body-large` |
| `.m3-label` | `.m3-label-large` |
| `.m3-caption` | `.m3-body-small` |

---

## 8. Performance Baseline — M3Surface Rendering

`M3Surface` uses the production **`UISystem/Shape`** shader (GPU SDF) for all shadow, overlay, fill, outline, and ripple rendering. This is the post-spec-009 architecture; the original CPU `Painter2D` pipeline was deleted in Phase 4 (2026-05-29).

### Acceptance gates (spec 009)

| Test | Result |
|------|--------|
| 50× shared-material elements → draw-call delta | ≤ 3 (PASS) |
| 50× elements × 15 unique fill colours via palette | Still ≤ 3 draw-call delta (PASS) |
| Phase 2 EditMode + PlayMode suite | 25/25 PASS |

### How the batching is preserved

- Per-instance fill colour is **palette-indexed**: `SdfShapePalette` exposes a 16-slot uniform; `FillColorOverride` resolves into byte 0 of `Vertex.tint`. No per-element material instance is needed.
- Per-instance M3 overlay opacities (state, tonal) pack into bytes 1 and 3 of the same tint, consumed by `EFFECT_M3_OVERLAYS_ON`.
- Shape geometry (corner radii / shadow / outline thickness) selects a **shared material category** via `SdfShapeMaterials.GetMaterial(SdfShapeConfig)`; instances within the same category batch into one draw call.
- Ripple is the only case that creates a per-event material instance (lifecycle = while `RippleAlpha > 0`); it reverts to the shared material the moment alpha returns to 0.

### Performance envelope

| Configuration | Expected behaviour |
|---|---|
| 50 elements sharing a material category | 1–3 draw calls (verified) |
| 50 elements with 15 unique fill colours | 1–3 draw calls via palette (verified) |
| Many shape categories (different corner radii / shadows) | One draw call per distinct `SdfShapeConfig` — monitor `SdfShapeMaterials.CategoryCount` and avoid per-frame mutation of category-driving fields (`CornerRadius*`, `Shadow*`, `OutlineThickness`). |
| Active ripple | +1 transient material instance per concurrent ripple; collapses back on completion. |

### Historical note

The previous edition of this guide documented `Painter2D` as the accepted shadow implementation and deferred the GPU shader path. Spec 009 reversed that decision after empirical evidence that vertex-color encoding plus selective per-color-group material instances cleared the batching gate. See `specs/009-sdf-unification/research.md` for the full trade-off analysis.

---

## 9. Exception Registry

All exceptions to the mandatory rules must be documented here.

| Rule | Exception | Component | Justification | Date Added |
|------|-----------|-----------|---------------|------------|
| USS-Only Theming | `M3Surface.StateOverlayOpacity` | All interactive | SDF-clipped overlay — cannot be CSS property | 2026-04-03 |
| USS-Only Theming | `M3Surface.TonalOverlayOpacity/Color` | Elevated components | GPU-rendered elevation tint layer | 2026-04-03 |
| USS-Only Theming | `M3Surface.RippleCenter/Radius/Alpha` | All interactive | Per-frame expanding circle animation | 2026-04-03 |
| USS-Only Theming | `M3Surface.FillColorOverride` | Card, Toggle, all (disabled) | SDF fill rendered before CSS background-color; also used by `FreezeSDFColors` to lock disabled appearance against `:hover` | 2026-04-03 |
| USS-Only Theming | `_floatingLabel.style.backgroundColor` | M3TextField | Notch effect requires reading runtime surface color | 2026-04-03 |
| Material Symbols | Checkmark + Dash in M3Checkbox | M3Checkbox | Geometric shapes, not font icons | 2026-04-03 |
| ~~GPU Shader~~ *(closed 2026-05-29)* | ~~Painter2D CPU shadow rendering~~ | M3Surface | Resolved by spec 009 — production `UISystem/Shape` shader uses palette-tinted vertex encoding to preserve batching while running on the GPU. | 2026-04-03 |
| USS Transition Durations | M3Animate uses hardcoded ms (e.g. `200f`) | M3Toggle, M3TextField | C# cannot read USS custom property values at runtime; durations documented in animation guidelines table | 2026-04-11 |
