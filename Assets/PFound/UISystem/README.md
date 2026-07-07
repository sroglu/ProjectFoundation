# UISystem

A modular, reusable UI component library for Unity, inspired by Material Design 3 (M3) principles.

This is not a 1:1 implementation of the M3 specification. It leverages Google's years of UX research — sizing, spacing, state feedback, color hierarchy, typography scale — to provide a Unity-native, performant, and flexible UI foundation.

Built on **UI Toolkit** (UXML + USS + C#) with **URP Shader Graph** for custom visual effects.

## Features

**SdfShape + M3Surface** *(spec 009 — Phase 4 closed 2026-05-29; legacy `SDFRectElement` deleted)* — The GPU-SDF-backed visual foundation of all components. Two-layer architecture:
- **`SdfShape`** (`PFound.UISystem.Shapes`) — general rounded rect / capsule / circle / pill element with shadow, outline, fill-color override. Anyone (M3 components, future design languages, custom UI) can use it directly.
- **`M3Surface : SdfShape`** (`PFound.UISystem.Components.M3`) — adds M3-specific overlays: tonal elevation, state layer (hover/focus/press), ripple animation. The scaffolding primitive for the 14 M3 components (M3Card, M3Button, M3Toggle, etc.).
- Powered by the production `UISystem/Shape` shader extended with the `EFFECT_VERTEX_TINT_PALETTE_ON` keyword (palette-indexed per-instance fill colors) and `EFFECT_M3_OVERLAYS_ON` keyword (M3 composite passes).
- **`SdfShapePalette`** static manager — 16-slot color palette uniform; up to 15 user-assignable unique fill colors + 1 reserved default. Throws on overflow (no silent quantization).
- **`SdfShapeConfig`** quantized struct — 18-byte shape geometry identity used to look up shared materials per category. 0.5px quantization prevents float-drift material proliferation.
- **`SdfShapeMaterials`** category-aware resolver — caches one `Material` per unique shape category, palette atomic-updates across all categories on theme swap.
- Verified per spec 009 Phase 2 acceptance: 25/25 tests PASS (EditMode 22 + PlayMode 3, including 50× shared-material batching at ≤3 draw calls AND 50× elements with 15 unique fill colors STILL batching at ≤3 via palette). See `specs/009-sdf-unification/` for spec, plan, data-model, quickstart.

**Theme System** — USS custom properties powered by ScriptableObject data. `ThemeData` SO holds a 27-role color palette, elevation presets, shape tokens, and motion presets. `ThemeManager` (static class, no MonoBehaviour required) applies `light.uss` or `dark.uss` stylesheets to managed panels at runtime — all `var(--m3-*)` variables resolve automatically. Light/dark toggle via `ThemeManager.ToggleLightDark()`.

**M3ComponentBase** — Abstract base class for all M3 components. Handles `ThemeManager` subscription, `StateLayerController` attach/detach lifecycle, and disabled state management. Automatic `FreezeSDFColors` on disable prevents USS `:hover` from corrupting disabled appearance. Extend it to build new components with zero boilerplate. See `COMPONENT-GUIDE.md` for the mandatory USS-only theming rule.

**M3Animate** — Lightweight schedule-based animation utility (`Runtime/Core/M3Animate.cs`). Provides `M3Animate.Float()` for smooth property transitions using ease-out cubic easing via `IVisualElementScheduler`. Used for M3Surface color/geometry animation where USS transitions are unreliable. No DOTween dependency.

**Typography** — Full M3 15-role type scale (Display L/M/S, Headline L/M/S, Title L/M/S, Body L/M/S, Label L/M/S). Roles defined as USS classes (`m3-display-large`, `m3-body-medium`, etc.) using TextCore SDF fonts.

**StateLayerController** — Plain C# interaction feedback controller (not MonoBehaviour). Manages hover (0.08), pressed (0.10), focused (0.10), and disabled (0.38) state overlays by setting `M3Surface.StateOverlayOpacity` directly — clipped to the rounded rect boundary. Integrates with `RippleElement` for M3 press ripple. Attach/Detach lifecycle for safe callback management.

**Flexbox Layouts** — UI Toolkit's native Flexbox engine handles all layout. No manual layout groups needed — just USS flex properties.

**Page Builder** — Editor tool for composing M3 pages visually. Open via `Game Tools > Page Builder`. Create new UXML pages with correct M3 style references, add components from a categorized palette, and edit in Unity's native UI Builder. Each added component gets a unique name for easy identification.

**MaterialSymbols** — Static class (`PFound.UISystem.Utils.MaterialSymbols`) providing 60+ Unicode codepoint constants for Material Symbols font glyphs. Use with `.m3-icon` USS class — no Painter2D drawing needed for icons.

**Dynamic Color / Material You** — Generate M3 color schemes from any seed color via `Assets > UISystem > Generate Theme from Seed Color`. Uses pure C# HCT color math (no external dependencies) to produce light + dark `ThemeData` ScriptableObject assets.

**Unified Showcase** — A single `Showcase.unity` scene demonstrates all components with Overview/Specs/Guidelines tabs and M3 design principles documentation.

## Components

### Implemented (v0.3.0)

| Component | Variants | Description |
|-----------|----------|-------------|
| M3Button | Filled, Outlined, Text, Tonal, Elevated | Primary and secondary actions |
| M3Card | Elevated, Filled, Outlined | Content grouping |
| M3Checkbox | Unchecked, Checked, Indeterminate | Multi-value selection |
| M3Chip | Assist, Filter, Input, Suggestion | Compact choice input |
| M3Dialog | — | Modal notification and confirmation |
| M3FAB | Small, Regular, Large, Extended | Prominent floating action |
| M3NavigationBar | 3–5 items | Bottom navigation |
| M3RadioButton / M3RadioGroup | — | Single-choice selection (label clickable per WCAG) |
| M3Slider | Continuous, Stepped | Value range input |
| M3Snackbar | Message, WithAction, WithClose | Temporary notification |
| M3TextField | Filled, Outlined | Text input with floating label |
| M3Toggle | Off, On | Binary switch |
| M3ProgressIndicator | Linear, Circular | Determinate + indeterminate |
| M3TopAppBar | Small, CenterAligned | Screen title + actions bar |
| M3Tabs / M3TabItem | Primary, Secondary | Tab navigation |
| M3Menu / M3MenuItem | — | Contextual action menu |
| M3Divider | Horizontal, Vertical | Visual separator |
| M3Badge | Small (dot), Large (count) | Status indicator overlay |
| M3NavigationDrawer | Modal, Standard | Side navigation panel |
| M3BottomSheet | Modal | Slide-up action sheet |
| M3SearchBar | — | Full-width search input |
| M3ListItem / M3List | OneLine, TwoLine, ThreeLine | List rows |
| M3SegmentedButton / M3SegmentedItem | Single, Multi | Button group toggle |
| M3NavigationRail | 3–7 items + FAB slot | Vertical side navigation |
| M3BottomAppBar | With/without FAB | Bottom action bar |
| M3Tooltip | Plain, Rich | Hover informational overlay |
| M3DatePicker | Modal | Calendar date selection |
| M3TimePicker | Modal | Clock-face time selection |

## Requirements

- Unity 6.3+ (6000.3)
- Universal Render Pipeline (URP)
- TextMeshPro (Unity package)

## Installation

### As a Git Submodule (recommended)

```bash
git submodule add https://github.com/sroglu/UISystem.git Assets/Submodules/UISystem
```

### Via Unity Package Manager (UPM)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.sroglu.uisystem": "https://github.com/sroglu/UISystem.git"
  }
}
```

## Quick Start

### 1. Create a Theme

Use `Assets > Create > UISystem > Theme Data` to create a new theme asset. Set the Primary, Secondary, and Surface colors along with elevation presets. Included `DefaultLight` and `DefaultDark` assets are ready to use.

### 2. Set Up ThemeManager

`ThemeManager` is a **static class — there is no host MonoBehaviour to place and nothing to keep alive across scenes.** It has two initialization paths:

**Automatic (recommended, zero code).** Put the theme assets under a `Resources/UISystem/` folder and `ThemeBootstrapper` self-initializes via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` before the first scene loads — no bootstrap code, no scene object. Expected asset names:

```
Resources/UISystem/DefaultLight        (ThemeData)
Resources/UISystem/DefaultDark         (ThemeData)
Resources/UISystem/light               (StyleSheet)
Resources/UISystem/dark                (StyleSheet)
Resources/UISystem/DefaultTypography   (TypographyConfig, optional)
Resources/UISystem/Roboto-Regular      (Font, optional)
```

**Manual.** If you don't use the Resources convention, call it once from your own boot code (guard on `ThemeManager.IsInitialized`):

```csharp
ThemeManager.Initialize(lightTheme, darkTheme, lightSheet, darkSheet /*, typographyConfig, defaultFont */);
```

Either way, `ThemeManager` syncs the `ThemeData` values to USS custom properties for every managed panel. Any `UIDocument` **spawned at runtime** must register itself so it receives the theme sync — call `ThemeManager.RegisterPanel(uiDocument)` from its `Start()`. Panels present at init are picked up automatically.

### 3. Add Components

**Method A — Context Menu:**

`Assets > Create > UISystem > Button (Filled)` to generate a configured UXML template.

**Method B — Page Builder:**

Open via `Game Tools > Page Builder`. Click "New Page" to create an M3-ready UXML, then click components in the palette to add them. The UXML opens in Unity's native UI Builder for visual editing.

**Method C — UXML / C#:**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:components="PFound.UISystem.Components">
    <components:M3Button variant="Filled" text="Click Me" />
</ui:UXML>
```

```csharp
using PFound.UISystem.Components;
using PFound.UISystem.Enums;

var button = new M3Button { Text = "Click Me", Variant = ButtonVariant.Filled };
button.OnClick += () => Debug.Log("clicked");
rootVisualElement.Add(button);
```

### 4. Switch Themes

```csharp
ThemeManager.SetTheme(darkThemeData);
```

All USS custom properties update automatically. Components restyle instantly.

## Architecture

```
┌─────────────────────────────────────────────┐
│           EDITOR TOOLING                     │
│   Page Builder, Context Menu Shortcuts,      │
│   Dynamic Color Generator                    │
├─────────────────────────────────────────────┤
│              COMPONENTS                      │
│   Button, Card, Toggle, TextField,           │
│   Dialog, Snackbar, Navigation               │
│   (UXML templates + C# custom elements)      │
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

## Design Principles

UISystem is not a copy of M3, but builds on the following principles:

- **Hierarchy through elevation:** Important elements get higher elevation. Depth conveyed through shadow + tonal overlay.
- **State feedback:** Every interactive element provides visual feedback. States expressed through specific opacity overlays.
- **Typography scale:** Roles (Display, Body, Label…) instead of arbitrary font sizes. Limited set of proportional sizes.
- **Identity through shape:** Corner radius on a consistent scale. Buttons use pill shape, cards use medium radius, dialogs use large radius.
- **Context through motion:** Animations provide context, not decoration. Consistent easing curves throughout.
- **Color roles:** Colors assigned through roles (Primary, Surface, Error…), not direct hex values. Theme changes propagate automatically.

## M3 Reference

The primary design reference is [m3.material.io](https://m3.material.io). When implementing or extending components, consult the M3 site for exact specifications — dp values, color role mappings, state opacities, animation durations, and component anatomy.

UISystem does not copy M3 pixel-for-pixel. The M3 site serves as a research-backed starting point: if you don't know what value to use, what size something should be, or how a state transition should feel, pull the answer from there.

## Known Limitations / Gaps

When planning a project on top of UISystem, be aware of these constraints:

- **UI Toolkit only — no uGUI support.** All components render via UI Toolkit `VisualElement` + USS + `M3Surface`. uGUI `Canvas`-based projects cannot reuse these components.
- **No drag-and-drop primitives built in.** There is no out-of-the-box drag source / drop target / drag preview API. For interactive UI that requires dragging (e.g., packing a backpack, arranging items on a canvas), implement custom `PointerDown/PointerMove/PointerUp` handlers on top of M3 components, or build a game-specific drag utility. Apple-style mobile-friendly touch drag gestures are NOT provided.
- **`AnimationSystem` (DOTween-based) does NOT work with `VisualElement`.** For UI Toolkit element animation, use USS `transition` properties for simple state changes, or `IVisualElementScheduler` (`M3Animate.Float()` is the built-in helper) for schedule-based tweens. Do not attempt to plug in DOTween — `VisualElement` is not a `Transform`.
- **Flexbox-based layout — no list virtualization.** `M3List` / `M3ListItem` render all items into the visual tree. For very long lists (100+ items), consider Unity's `ListView` primitive or custom virtualization — UISystem does not wrap these.
- **Text uses TextCore SDF fonts only.** No TextMeshPro integration. If your project requires custom TMP fonts, you must wrap them for UI Toolkit separately.
- **`ThemeManager` is a static class.** Theming is global — there is no per-panel theme override API built in. For multi-theme apps (e.g., kids vs. adult modes in the same process), implement your own theme scope.
- **No built-in `LocalizedString` binding to labels.** UI Toolkit text fields (`Label.text`) do not auto-bind to `Localization` module keys. Call `Localization.GetLocalizedText(key)` in C# and assign `Label.text` manually (or write a custom binding helper).
- **Dynamic color generation is Editor-time only.** `Generate Theme from Seed Color` creates `ThemeData` assets at author time. Runtime color palette switching (e.g., user picks a seed color in-app) is not supported out-of-the-box.

## Scope and Roadmap

For detailed work packages, technical decisions, and development order, see [SCOPE.md](SCOPE.md).

## License

MIT
