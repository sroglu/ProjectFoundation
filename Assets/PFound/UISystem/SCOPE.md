# UISystem

A modular, reusable UI component library for Unity, inspired by Material Design 3 (M3) principles.

This is **not** a 1:1 implementation of the M3 specification. It leverages Google's years of UX research — sizing, spacing, state feedback, color hierarchy, typography scale — to provide a Unity-native, performant, and flexible UI foundation.

**Target platforms:** Mobile (Android/iOS) — casual and hypercasual games as priority, utility apps supported.

**Dependencies:** URP (Universal Render Pipeline). Works as a standalone submodule. Can be used alongside AssetSystem but does not require it.

**UI Backend:** UI Toolkit (UXML + USS + C#). Unity 6.3 introduced Shader Graph support for UI Toolkit, making custom visual effects possible. Combined with USS variables for theming and Flexbox layout, UI Toolkit provides the strongest foundation for an M3-inspired design system.

> **Note:** This document defines scope and work plan. Detailed technical decisions for each work package will be finalized during implementation.

---

## M3 Reference Guide

**Primary reference:** [https://m3.material.io](https://m3.material.io)

This is the official Material Design 3 documentation by Google. It contains detailed specifications for every component, color role, typography token, elevation level, state layer, and motion curve used in the M3 system.

**How to use this reference during development:**

When implementing a work package, if you are unsure about how a specific component should look, behave, or be sized — **do not guess**. Go to [m3.material.io](https://m3.material.io), find the relevant component or style page, and study the spec. The site provides exact dp values, color role mappings, state layer opacities, animation durations, and anatomy breakdowns for every component.

Key pages to bookmark:

- **Components:** `m3.material.io/components` — anatomy, specs, and guidelines for every M3 component.
- **Color system:** `m3.material.io/styles/color` — color roles, tonal palettes, dynamic color.
- **Typography:** `m3.material.io/styles/typography` — type scale, font sizes, line heights, weights.
- **Elevation:** `m3.material.io/styles/elevation` — shadow and tonal overlay levels.
- **Shape:** `m3.material.io/styles/shape` — corner radius scale and shape categories.
- **Motion:** `m3.material.io/styles/motion` — easing curves, duration tokens.
- **State layers:** `m3.material.io/foundations/interaction/states` — overlay opacities per state.

**The rule:** implement things your way in Unity, but when you don't know what value to use, what size something should be, or how a state transition should feel — pull the answer from M3. That's the whole point: not copying M3 pixel-for-pixel, but using its research-backed decisions as your starting point.

---

## Architecture Layers

```
┌─────────────────────────────────────────────┐
│           EDITOR TOOLING                     │
│   Component Builder Wizard,                  │
│   Layout Preset Generator,                   │
│   Custom Inspectors                          │
├─────────────────────────────────────────────┤
│              COMPONENTS                      │
│   Button, Card, Toggle, TextField,           │
│   Dialog, Snackbar, Navigation ...           │
│   (UXML templates + C# logic)               │
├─────────────────────────────────────────────┤
│              CORE SYSTEMS                    │
│   ThemeManager, StateLayerController,        │
│   TypographyResolver, MotionPresets          │
├─────────────────────────────────────────────┤
│              FOUNDATION                      │
│   UI Shader Graph, USS Theme Variables,      │
│   Design Token Definitions                   │
└─────────────────────────────────────────────┘
```

Dependencies flow top-down. Foundation depends on nothing. Core Systems depend only on Foundation. Components depend on both. Editor Tooling accesses all layers but runs only in the Editor — it is excluded from runtime builds.

---

## Technical Decisions (Resolved)

The following architectural questions were investigated and resolved before development:

| Question | Decision | Rationale |
|----------|----------|-----------|
| UI Backend | UI Toolkit | USS variables map naturally to M3 tokens; Flexbox layout replaces manual layout groups; Shader Graph support landed in Unity 6.3; future-proof. |
| Per-element visual parameters | USS custom properties + Shader Graph | UI Toolkit handles batching internally; USS custom properties (`--corner-radius`, `--shadow-blur`, etc.) feed into Shader Graph via material overrides. No UV channel packing needed. |
| dp → pixel conversion | Canvas Scaler equivalent via Panel Settings + scale mode | UI Toolkit's `PanelSettings` with `Scale With Screen Size` and 1080×1920 reference resolution. Values in theme defined as reference-resolution pixels; scaling handled automatically. |
| Font asset management | Separate TMP font asset per weight, linked via Font Weights table | Roboto Regular (400) + Medium (500) + Bold (700); Inter as alternative. Fake bold is low quality. Two atlases at 512×512 with Unicode range `20-7E,A0-FF,100-17F` for Turkish+Latin support. |
| Toggle thumb | Separate VisualElement | Simpler animation via USS transitions, independent state layer support, trivial icon insertion. Negligible rendering cost. |

---

## Work Packages

### WP-1: Foundation — UI Shader Graph

**Goal:** Create a Shader Graph-based UI shader that serves as the visual foundation for all components.

**Shader Capabilities:**

- **Rounded Rectangle SDF** — Independent radius per corner. Based on Inigo Quilez's `sdRoundedBox` function, implemented as a Custom Function node in Shader Graph.
- **Shadow / Glow** — Soft shadow via offset, blur, and color parameters. M3's 5 elevation levels defined as presets.
- **Outline / Border** — Thickness and color. Inner/outer selection based on SDF distance.
- **State Overlay** — Color + opacity for hover, pressed, focused, disabled states. Blended on top of base color.
- **Ripple Effect** — Expanding circle from touch point, driven from C# script.

**Technical Details:**

- Created as a URP UI Shader Graph (new in Unity 6.3). Uses the `Render Type Branch` node for handling different UI render types.
- Per-element parameters exposed as material properties, overridden per VisualElement via USS or C#.
- The `Custom Function` node wraps the SDF math (Quilez's rounded box, circle distance, smoothstep blending).

**Deliverables:**

- `Shaders/SDFRect.shadergraph`
- `Materials/SDFRect.mat` — Default material instance.
- ~~`Scripts/SDFRectElement.cs`~~ *(deleted spec 009 Phase 4 — replaced by `Runtime/Shapes/SdfShape.cs` + `Runtime/Components/M3/M3Surface.cs`)* — Custom VisualElement that exposes SDF parameters (corner radius, shadow, overlay) as USS custom properties and drives the material.
- Sample UXML + USS demonstrating various combinations.

**To Research / Clarify:**

- [ ] Shader Graph UI node set: exact capabilities and limitations of the new URP UI material type in 6.3. What render type branches are available? Can we access vertex data for per-element parameters?
- [ ] Batching behavior: does assigning a material with different property overrides per VisualElement break batching in UI Toolkit? If so, evaluate alternative approaches (USS-driven vertex tinting, Painter2D fallback for shadows).
- [ ] Custom VisualElement rendering: `generateVisualContent` callback vs material-based approach. Which gives more control while preserving batching?
- [ ] SDF fragment shader performance on mobile: same concern as before — smoothstep + shadow + ripple fill rate on Mali GPUs.

---

### WP-2: Foundation — Theme System (Design Tokens)

**Goal:** Build a theming infrastructure using USS custom properties, inspired by M3's token hierarchy.

**Token Hierarchy (simplified):**

```
Reference Tokens (raw colors)
  └─ System Tokens (roles: primary, secondary, surface, error...)
       └─ Component Tokens (--button-container-color: var(--m3-primary))
```

**Dual approach: USS variables + ScriptableObject backup.**

Theme tokens defined as USS custom properties in root-level `.uss` files. A `ThemeData` ScriptableObject mirrors these values for C# access (animation targets, runtime logic). The ScriptableObject is the source of truth; a `ThemeManager` syncs SO values to USS variables at runtime.

**USS Theme File Example:**

```css
:root {
    --m3-primary: #6750A4;
    --m3-on-primary: #FFFFFF;
    --m3-primary-container: #EADDFF;
    --m3-surface: #FFFBFE;
    --m3-on-surface: #1C1B1F;
    --m3-elevation-1-shadow-blur: 3px;
    --m3-elevation-1-shadow-offset-y: 1px;
    --m3-shape-small: 8px;
    --m3-shape-medium: 12px;
    --m3-shape-large: 16px;
    --m3-shape-full: 9999px;
    --m3-motion-duration-short: 200ms;
    --m3-motion-duration-medium: 400ms;
}
```

**ThemeData ScriptableObject contents:**

- **Color Palette** — Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer, Secondary (same set), Tertiary (optional), Surface, OnSurface, SurfaceVariant, OnSurfaceVariant, Error, OnError, Outline, OutlineVariant, Background.
- **Elevation Presets** — 6 levels (0–5): shadow offset, blur, color, tonal overlay alpha.
- **Shape Presets** — None (0), ExtraSmall (4), Small (8), Medium (12), Large (16), ExtraLarge (28), Full (9999).
- **Motion Presets** — AnimationCurve + duration (ms): Emphasized, Standard, EmphasizedDecelerate, StandardDecelerate.

**ThemeManager:**

- Holds active `ThemeData` reference.
- On theme change, iterates USS custom properties and updates them from SO values.
- Light/dark switching: swap SO, re-apply USS variables.
- `OnThemeChanged` C# event for components that need procedural updates.

**Deliverables:**

- `ScriptableObjects/ThemeData.cs`
- `Scripts/ThemeManager.cs`
- `Styles/Themes/light.uss` and `dark.uss`
- `Editor/ThemeDataEditor.cs` — Custom inspector with color palette preview.
- 2 sample theme assets: DefaultLight, DefaultDark.

**To Research / Clarify:**

- [ ] USS custom property performance: is setting 30+ custom properties on `:root` per frame (during theme transition animation) performant? Or should theme transitions be instant (swap USS file) rather than animated?
- [ ] USS variable cascading: do custom properties defined on `:root` cascade to all nested VisualElements as expected? Any known issues with specificity or override behavior?
- [ ] PanelSettings scale mode: confirm that `Scale With Screen Size` with 1080×1920 reference works correctly on both Android and iOS with UI Toolkit. Test on different aspect ratios (16:9, 19.5:9, tablets).

---

### WP-3: Foundation — Typography System

**Goal:** Model M3's typography scale using UI Toolkit's text system and TMP font assets.

**Typography Scale (M3-inspired, simplified):**

| Role       | Size | Weight   | Usage                          |
|------------|------|----------|--------------------------------|
| Display    | 36+  | Regular  | Splash screen, large numbers   |
| Headline   | 28   | Regular  | Section headings               |
| Title      | 22   | Medium   | Card titles, dialog titles     |
| Body       | 16   | Regular  | General text                   |
| Label      | 14   | Medium   | Button labels, captions        |
| Caption    | 12   | Regular  | Helper text, timestamps        |

> **M3 Reference:** See `m3.material.io/styles/typography` for the full 15-token type scale (5 roles × 3 sizes). The simplified 6-role table above covers initial needs. Expand to the full scale if needed.

**USS Typography Classes:**

```css
.m3-display  { font-size: 36px; -unity-font-style: normal; }
.m3-headline { font-size: 28px; -unity-font-style: normal; }
.m3-title    { font-size: 22px; -unity-font-style: bold; }
.m3-body     { font-size: 16px; -unity-font-style: normal; }
.m3-label    { font-size: 14px; -unity-font-style: bold; }
.m3-caption  { font-size: 12px; -unity-font-style: normal; }
```

Font weight switching via USS: `-unity-font-definition` pointing to the appropriate TMP font asset (Regular or Medium).

**Font Assets (resolved):**

- Roboto: Regular (400), Medium (500), Bold (700) — static `.ttf` files from Google Fonts.
- Inter: Regular (400), Medium (500), Bold (700) — `Inter_18pt-*.ttf` variant.
- TMP atlas settings: 44pt sampling, 5px padding, Optimum packing, 512×512, Unicode range `20-7E,A0-FF,100-17F`, SDFAA, Get Font Features enabled.

**Deliverables:**

- `Styles/typography.uss` — USS classes for all type roles.
- `Scripts/TypographyResolver.cs` — Optional C# helper to apply type role to a VisualElement.
- `Enums/TextRole.cs`
- Pre-generated TMP font assets for Roboto and Inter (6 total).

**To Research / Clarify:**

- [ ] UI Toolkit font rendering: does UI Toolkit use TMP font assets natively in Unity 6.3, or is there a separate text rendering path? Confirm the correct way to reference font assets in USS (`-unity-font-definition`).
- [ ] USS class-based typography vs inline styles: is applying a USS class (`.m3-label`) to a Label element sufficient, or does UI Toolkit override font properties?

---

### WP-4: Core Systems — State Layer Controller

**Goal:** Manage interaction states and provide visual feedback following M3's state layer principles.

**M3 State Layer Opacities:**

| State    | Overlay Opacity |
|----------|----------------|
| Enabled  | 0%             |
| Hovered  | 8%             |
| Focused  | 10%            |
| Pressed  | 10%            |
| Disabled | Element: 38%, container: 12% |

> **M3 Reference:** See `m3.material.io/foundations/interaction/states` for the full state layer specification and visual examples.

**UI Toolkit Approach:**

USS pseudo-classes (`:hover`, `:active`, `:focus`, `:disabled`) map directly to M3 states. State overlay is a child VisualElement with `position: absolute` covering the parent, its background-color opacity controlled by USS transitions.

```css
.m3-state-layer {
    position: absolute;
    top: 0; right: 0; bottom: 0; left: 0;
    background-color: rgba(0, 0, 0, 0);
    transition: background-color var(--m3-motion-duration-short);
}
.m3-interactive:hover > .m3-state-layer {
    background-color: rgba(var(--m3-on-surface-rgb), 0.08);
}
.m3-interactive:active > .m3-state-layer {
    background-color: rgba(var(--m3-on-surface-rgb), 0.10);
}
```

**Ripple Effect:**

Driven from C# via `generateVisualContent` or a dedicated `RippleElement` custom VisualElement. On pointer down, records touch position; on update, expands circle radius and fades alpha. Draws into the Shader Graph material or as an overlay mesh.

**Deliverables:**

- `Styles/state-layer.uss` — Base USS for state overlay transitions.
- `Scripts/StateLayerController.cs` — C# component for ripple and programmatic state management.
- `Scripts/RippleElement.cs` — Custom VisualElement for ripple rendering.

**To Research / Clarify:**

- [ ] USS `:hover` on mobile: touch devices don't have hover. Verify that `:hover` is not triggered on touch-down on mobile builds. If it is, conditional USS or C# filtering may be needed.
- [ ] USS `transition` support: confirm that `background-color` transitions work smoothly in UI Toolkit. Check if custom properties can be transitioned.
- [ ] Ripple rendering: `generateVisualContent` provides a `MeshGenerationContext` — can this be used to draw a circle overlay that animates? Or is a Shader Graph approach needed?
- [ ] Disabled state: how does UI Toolkit handle `:disabled` pseudo-class? Does setting `SetEnabled(false)` automatically apply it?

---

### WP-5: Components — Button

**Goal:** First component and proof-of-concept for the full stack.

**M3 Button Types:**

| Type      | Usage                           | Visual                         |
|-----------|---------------------------------|--------------------------------|
| Filled    | Primary action                  | Container: Primary, text: OnPrimary |
| Outlined  | Secondary/alternative action    | Transparent, border, text: Primary |
| Text      | Lowest emphasis                 | No container, text: Primary    |
| Tonal     | Between filled and outlined     | Container: SecondaryContainer  |

> **M3 Reference:** See `m3.material.io/components/buttons` for full anatomy, specs, sizing, and state behavior.

**Implementation:**

- UXML template: `M3Button.uxml` defining the element hierarchy (container > state-layer + label + optional icon).
- USS styles: `m3-button.uss` with variants `.m3-button--filled`, `.m3-button--outlined`, `.m3-button--text`, `.m3-button--tonal`.
- C# class: `M3Button : VisualElement` — custom element registered with `UxmlElement` attribute. Exposes `ButtonStyle` enum, label text, icon, theme binding.
- Sizing: min-height 40px (ref), horizontal padding 24px, corner radius `var(--m3-shape-full)` (pill), label uses `.m3-label` typography class.

**Deliverables:**

- `Scripts/Components/M3Button.cs`
- `UXML/M3Button.uxml`
- `Styles/Components/m3-button.uss`
- Sample: 4 button types × light/dark theme.

**To Research / Clarify:**

- [ ] Custom VisualElement registration: `[UxmlElement]` attribute workflow in Unity 6.3. How to expose custom properties (ButtonStyle enum) in UI Builder.
- [ ] Icon integration: how to include an SVG or image inside a VisualElement alongside text? FlexBox row with gap?
- [ ] Auto-sizing: does UI Toolkit handle intrinsic content sizing automatically (like CSS `width: fit-content`)?

---

### WP-6: Components — Card

**Goal:** Content grouping and information display.

> **M3 Reference:** See `m3.material.io/components/cards` for card anatomy, types, and interaction patterns.

**M3 Card Types:** Elevated (shadow), Filled (SurfaceVariant, no shadow), Outlined (border).

**Implementation:**

- UXML template with content slot using `<Slot>` or child content area.
- USS: `.m3-card--elevated`, `.m3-card--filled`, `.m3-card--outlined`.
- Corner radius: `var(--m3-shape-medium)` (12px).
- Flexbox column layout for internal anatomy (header / media / content / actions).

**Deliverables:**

- `Scripts/Components/M3Card.cs`, UXML, USS.
- Sample: vertical scroll card list.

**To Research / Clarify:**

- [ ] Content slotting: what is the best pattern for a card that accepts arbitrary child content in UI Toolkit? `contentContainer` override?
- [ ] Clickable card with state layer: attaching `Clickable` manipulator to the card root while still allowing child button clicks.

---

### WP-7: Components — Toggle & TextField

**Goal:** Form elements — binary selection and text input.

> **M3 Reference:** See `m3.material.io/components/switch` and `m3.material.io/components/text-fields`.

**Toggle (Switch):**

- Track = VisualElement with pill shape via USS `border-radius`.
- Thumb = child VisualElement (circle), animated with USS `transition` on `left` property.
- State colors via USS custom properties from theme.

**TextField:**

- Built on UI Toolkit's native `TextField` element.
- Visual layer via USS: outlined or filled variants.
- Floating label: USS transitions on `translate` and `scale` triggered by `:focus` and a `.has-value` class.
- Helper/error text as sibling Labels below the field.

**Deliverables:**

- `M3Toggle.cs`, `M3TextField.cs`, UXML templates, USS files.

**To Research / Clarify:**

- [ ] TextField floating label: can USS `transition` on `translate` + `scale` achieve the M3 floating label effect? Or is C# animation needed?
- [ ] Mobile keyboard: does UI Toolkit's TextField handle `TouchScreenKeyboard` properly on Android/iOS?

---

### WP-8: Components — Dialog & Snackbar

**Goal:** Overlay UI for notification and confirmation.

> **M3 Reference:** See `m3.material.io/components/dialogs` and `m3.material.io/components/snackbar`.

**Dialog:**

- Scrim: full-screen VisualElement, `background-color: rgba(0,0,0,0.32)`.
- Dialog container: Surface color, elevation level 3, corner radius ExtraLarge (28px).
- Open/close: USS transitions on `opacity` and `scale`.

**Snackbar:**

- Container: InverseSurface, corner radius ExtraSmall (4px).
- Auto-dismiss with configurable duration.
- Queue system via `SnackbarManager`.

**Deliverables:**

- `M3Dialog.cs`, `M3Snackbar.cs`, `SnackbarManager.cs`, UXML, USS.

**To Research / Clarify:**

- [ ] Overlay rendering order: how to ensure dialog/snackbar renders on top of everything in UI Toolkit? `BringToFront()` or separate UIDocument with higher sort order?
- [ ] Scrim click handling: pointer events on the scrim should close the dialog. Confirm event propagation behavior.

---

### WP-9: Components — Navigation

**Goal:** Navigation bar and tab components.

> **M3 Reference:** See `m3.material.io/components/navigation-bar` and `m3.material.io/components/tabs`.

**Bottom Navigation Bar:** 3–5 items, active indicator with pill shape, icon + optional label.

**Tab Bar:** Fixed or scrollable, active underline indicator.

**Deliverables:**

- `M3BottomNav.cs`, `M3TabBar.cs`, UXML, USS.
- Visual-only + `OnItemSelected(int index)` callback. No screen management.

**To Research / Clarify:**

- [ ] Active indicator animation: USS transition on `translate` and `width` for the sliding indicator.
- [ ] Safe area: `Screen.safeArea` integration with UI Toolkit panel. Bottom nav must not overlap system gesture bar.

---

### WP-10: Editor Tooling — Page Builder + Context Menu

**Goal:** Editor tools for rapidly composing M3 pages and creating component templates.

**Status:** ✅ Implemented (007-page-builder)

**Implemented (007-page-builder):**

**A) Page Builder Window:** `Game Tools > Page Builder` — categorized M3 component palette (35 components across 6 categories). Creates UXML pages with correct style references, inserts M3 components via XML manipulation, integrates with Unity's native UI Builder for visual editing. Auto-names components (`m3-button-1`), saves UI Builder state before modifications, Clear page support.

- `Editor/PageBuilder/PageBuilderWindow.cs`
- `Editor/PageBuilder/ComponentRegistry.cs`
- `Editor/PageBuilder/ComponentPalette.cs`
- `Editor/PageBuilder/UxmlExporter.cs`

**Implemented (005-button):**

**B) Context Menu Shortcuts:**

```
Assets > Create > UISystem > Button (Filled)
Assets > Create > UISystem > Card (Elevated)
...
```

- `Editor/MenuItems/UISystemMenuItems.cs`

**Deferred:**

- Layout presets (Button Row, Card Grid, Form Layout) — planned for P3
- UXML import/round-trip editing — planned for P2

---

## Development Order and Dependencies

```
WP-1 (UI Shader Graph) ────────┐
                                 ├──► WP-4 (State Layer) ──► WP-5 (Button) ──► WP-6 (Card)
WP-2 (Theme System) ───────────┤                                │
                                 │                                ▼
WP-3 (Typography) ─────────────┘                        WP-7 (Toggle/TextField)
                                                                  │
                                                                  ▼
                                                         WP-8 (Dialog/Snackbar)
                                                                  │
                                                                  ▼
                                                         WP-9 (Navigation)

WP-10 (Component Builder Wizard) ◄── Can start after WP-5 is complete,
                                      expands as each new component is added.
```

**Phase 1 — Foundation:** WP-1, WP-2, WP-3 in parallel.

**Phase 2 — Core + First Component:** WP-4 + WP-5. Button is the proof-of-concept. Issues here may require revisiting Foundation.

**Phase 2.5 — Editor Tooling Foundation:** WP-10 starts after WP-5. Grows with each new component.

**Phase 3 — Component Expansion:** WP-6 → WP-7 → WP-8 → WP-9 sequentially.

---

## Folder Structure

```
UISystem/
├── README.md
├── SCOPE.md                          ← this file
├── Runtime/
│   ├── Shaders/
│   │   └── SDFRect.shadergraph
│   ├── Materials/
│   │   └── SDFRect.mat
│   ├── ScriptableObjects/
│   │   ├── ThemeData.cs
│   │   └── TypographyConfig.cs
│   ├── Core/
│   │   ├── ThemeManager.cs
│   │   ├── StateLayerController.cs
│   │   ├── RippleElement.cs
│   │   ├── TypographyResolver.cs
│   │   └── MotionPresets.cs
│   ├── Components/
│   │   ├── M3Button.cs
│   │   ├── M3Card.cs
│   │   ├── M3Toggle.cs
│   │   ├── M3TextField.cs
│   │   ├── M3Dialog.cs
│   │   ├── M3Snackbar.cs
│   │   ├── M3BottomNav.cs
│   │   └── M3TabBar.cs
│   ├── Enums/
│   │   ├── ButtonStyle.cs
│   │   ├── CardStyle.cs
│   │   ├── ElevationLevel.cs
│   │   ├── ShapePreset.cs
│   │   └── TextRole.cs
│   └── Data/
│       ├── ElevationPreset.cs
│       └── TextStyleData.cs
├── Styles/
│   ├── Themes/
│   │   ├── light.uss
│   │   └── dark.uss
│   ├── typography.uss
│   ├── state-layer.uss
│   └── Components/
│       ├── m3-button.uss
│       ├── m3-card.uss
│       ├── m3-toggle.uss
│       ├── m3-textfield.uss
│       ├── m3-dialog.uss
│       ├── m3-snackbar.uss
│       └── m3-navigation.uss
├── UXML/
│   ├── M3Button.uxml
│   ├── M3Card.uxml
│   ├── M3Toggle.uxml
│   ├── M3TextField.uxml
│   ├── M3Dialog.uxml
│   ├── M3Snackbar.uxml
│   └── M3BottomNav.uxml
├── Editor/
│   ├── Inspectors/
│   │   ├── ThemeDataEditor.cs
│   │   └── M3ButtonEditor.cs
│   └── Wizard/
│       ├── ComponentCreatorWindow.cs
│       ├── LayoutComposerWindow.cs
│       ├── UISystemMenuItems.cs
│       └── ComponentFactory.cs
├── Assets/
│   ├── Themes/
│   │   ├── DefaultLight.asset
│   │   └── DefaultDark.asset
│   ├── Typography/
│   │   └── Fonts/
│   │       ├── Roboto-Regular SDF.asset
│   │       ├── Roboto-Medium SDF.asset
│   │       ├── Roboto-Bold SDF.asset
│   │       ├── Inter-Regular SDF.asset
│   │       ├── Inter-Medium SDF.asset
│   │       └── Inter-Bold SDF.asset
│   └── PanelSettings/
│       └── DefaultPanelSettings.asset
├── Samples~/
│   ├── AllComponents/
│   ├── ThemeSwitching/
│   └── MobileDemo/
└── package.json
```

---

## Out of Scope (For Now)

- ~~**GPU SDF Shader (WP-1 deferred):**~~ *(SHIPPED in spec 009 — Phase 4 closed 2026-05-29)*. The GPU SDF shader (`UISystem/Shape` production shader with `EFFECT_VERTEX_TINT_PALETTE_ON` + `EFFECT_M3_OVERLAYS_ON` keywords) replaced the CPU `Painter2D` concentric fill loop. Per-element overlay opacities pack into `Vertex.tint` for batching preservation; per-instance fill colors resolved through `SdfShapePalette` (16-slot uniform). Acceptance: 50× shared-material elements at ≤3 draw calls AND 50× elements with 15 unique fills STILL batching at ≤3 via palette.
- **Screen/Page management:** Navigation components are visual only.
- **Localization integration:** Typography manages fonts/sizes; string management is external.
- **Accessibility:** M3 contrast and touch target requirements followed in principle; WCAG testing out of scope.
- **Asset Store release:** Documentation and packaging deferred.
- **Built-in Render Pipeline support:** UISystem requires URP for Shader Graph UI support.

## Shape Primitives — When to Use Which *(post spec 009)*

Five sibling SDF primitives live under `Runtime/Shapes/` + `Runtime/Components/M3/`. Pick by intent, not by familiarity:

- **`SdfShape`** (`PFound.UISystem.Shapes`) — general GPU-SDF rounded-rect / capsule / circle / pill with shadow, outline, and palette-resolved fill. Zero M3 coupling; use it when building non-M3 UI (custom design systems, debug overlays, marketing pages) that still want batched SDF rendering.

- **`M3Surface : SdfShape`** (`PFound.UISystem.Components.M3`) — adds M3-specific tonal-elevation overlay, hover/focus/press state layer, and ripple. The scaffolding primitive for all 14 M3 components. Use whenever you're authoring an M3-flavoured component.

- **`GpuSdfElement`** (`PFound.UISystem.Shapes`) — theme-driven, free-shape material consumer (binds any externally-authored SDF material — gradients, banding, noise, complex effects). Use when the shape needs an effect that's outside `SdfShape`'s fixed feature set (e.g., `Render.UIShapes` shader chains).

- **`SdfPanel`** (`PFound.UISystem.Shapes`) — composable theme-driven container with deferred config; reads its visual parameters from a USS-bound theme key and renders via `GpuSdfElement` internally. Use for theme-swap-driven containers without writing C# shape code.

- **`ThemedSdfPanel`** (`PFound.UISystem.Shapes`) — `SdfPanel` with first-class theme-key resolver (`ThemeManager.OnThemeChanged` subscription baked in). Use as the top-level decorative container when both runtime theme swap and zero-boilerplate are required.

Quick rule: M3 component? → `M3Surface`. Generic SDF UI? → `SdfShape`. Material-authored effect? → `GpuSdfElement` (raw) or `SdfPanel` / `ThemedSdfPanel` (theme-bound).

---

## Implemented (v0.3.0 — 006-m3-uisystem-overhaul)

- **USS-Only Theming:** All 14 original components migrated to pure USS `var(--m3-*)` — no inline C# color assignments.
- **M3ComponentBase:** Abstract base class eliminates boilerplate (ThemeManager subscription, StateLayerController lifecycle).
- **ThemeManager static API:** No MonoBehaviour dependency.
- **Typography 15-Role Scale:** Full M3 Display/Headline/Title/Body/Label L/M/S scale in USS and TypographyConfig SO.
- **TextCore FontAsset:** Migrated from TMP_FontAsset to TextCore.Text.FontAsset.
- **USS Transitions:** Motion duration tokens (`--m3-motion-duration-*`) replace hardcoded `ms` values in all component USS files.
- **MaterialSymbols:** Font-based icon rendering via `MaterialSymbols` static class; FAB and Chip migrated from Painter2D.
- **Dynamic Color / Material You:** `DynamicColorGenerator` Editor tool generates ThemeData from seed color using HCT math.
- **27 Color Tokens:** Added Tertiary family, SurfaceContainerLowest, Scrim, SurfaceTint.
- **Unified Showcase Scene:** Single scene with per-component Overview/Specs/Guidelines tabs and M3 design principles.
- **New Components (16 total):** ProgressIndicator, TopAppBar, Tabs, Menu, Divider, Badge, NavigationDrawer, BottomSheet, SearchBar, ListItem/List, SegmentedButton, NavigationRail, BottomAppBar, Tooltip, DatePicker, TimePicker.

---

## Nice to Have (Optional)

### Design Token Importer

Editor tool that reads design tokens from web design systems (Bootstrap, Tailwind CSS, Chakra UI, Ant Design) and converts them into `ThemeData` ScriptableObject + USS variable files. Useful for rapid prototyping with different visual identities. Deferred until ThemeData SO is well-established.
