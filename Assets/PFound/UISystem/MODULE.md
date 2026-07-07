# PFound.UISystem — Architecture

Deep reference for the module's architecture. For the doc map see [GUIDE.md](GUIDE.md);
for component-authoring rules see [COMPONENT-GUIDE.md](COMPONENT-GUIDE.md).

## Purpose

A modular, reusable UI component library for Unity, built on UI Toolkit (UXML + USS + C#)
and inspired by Material Design 3. It provides a GPU-SDF visual foundation, a runtime theme
system, an M3 type scale, and a set of themed interactive components — plus editor tooling
to author pages and generate themes. It is not a 1:1 M3 implementation; it borrows M3's
research-backed sizing, spacing, state feedback, and color hierarchy on a Unity-native,
batched, performant base.

## Assemblies

| Assembly | Location | Platforms | Notes |
|----------|----------|-----------|-------|
| `PFound.UISystem` | `Runtime/` | all | Runtime library. `autoReferenced: true`. |
| `PFound.UISystem.Editor` | `Editor/` | Editor only | Page Builder, theme generation, menu items, inspectors. |
| `PFound.UISystem.Tests` | `Tests/` | Editor | EditMode tests. |
| `PFound.UISystem.Tests.PlayMode` | `Tests/PlayMode/` | Editor | PlayMode tests (batching / draw-call gates). |

## Dependencies

- **Unity 6.3+ (6000.3)** — required; UI Toolkit Shader Graph / SDF support landed in 6.3.
- **Universal Render Pipeline (URP)** — the `UIShape` shader is a URP UI shader; BiRP is unsupported.
- **TextMeshPro / TextCore** — referenced by the runtime asmdef; text renders via
  `TextCore.Text.FontAsset` (not `TMP_FontAsset`).
- **Odin Inspector attributes** — the asmdef precompiled-references `Sirenix.OdinInspector.Attributes.dll`
  (editor asmdef also references the editor dll) for inspector authoring.
- No hard dependency on any other PFound module. Works standalone as a submodule; can be used
  alongside AssetSystem but does not require it.

## Key Types

### `Runtime/Shapes/` — SDF foundation (`PFound.UISystem.Shapes`)
- `SdfShape` — general GPU-SDF rounded-rect / capsule / circle / pill `VisualElement` with
  shadow, outline, and palette-resolved fill. Zero M3 coupling.
- `SdfShapeConfig` — quantized 18-byte struct that is the shape-geometry identity used to look
  up a shared material category (0.5px quantization prevents float-drift material proliferation).
- `SdfShapeMaterials` — category-aware resolver; `GetMaterial(SdfShapeConfig)` caches one
  `Material` per unique category (`CategoryCount`, `ActiveMaterials`, `ClearCache`).
- `SdfShapePalette` — static 16-slot color-palette uniform; `Resolve(Color)` maps a fill color to
  a palette slot (up to 15 unique + 1 reserved default), throws on overflow (no silent quantization).
- `GpuSdfElement` / `SdfPanel` / `ThemedSdfPanel` — theme-driven, material-authored shape consumers
  for effects outside `SdfShape`'s fixed feature set (see SCOPE.md "Shape Primitives — When to Use Which").
- `UIShape.shader` + `*.hlsl` — the production `UISystem/Shape` shader with the
  `EFFECT_VERTEX_TINT_PALETTE_ON` and `EFFECT_M3_OVERLAYS_ON` keywords.

### `Runtime/Components/M3/` — M3 surface primitive (`PFound.UISystem.Components.M3`)
- `M3Surface : SdfShape` — adds M3 overlays on top of `SdfShape`: tonal elevation
  (`TonalOverlayOpacity` / `TonalOverlayColor`), state layer (`StateOverlayOpacity`), and ripple
  (`RippleCenter` / `RippleRadius` / `RippleAlpha`). Scaffolding primitive for all M3 components.
- `M3Label` — themed label element.

### `Runtime/Core/` — theme + lifecycle (`PFound.UISystem.Core`)
- `ThemeManager` — static theme applicator (no MonoBehaviour). See Public API.
- `ThemeBootstrapper` — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` auto-initializer that
  loads theme assets from `Resources/UISystem/`.
- `M3ComponentBase : VisualElement` — abstract base for every M3 component; handles theme
  subscription, state-layer lifecycle, and disabled-state color freezing.
- `StateLayerController` — plain-C# hover/press/focus/disabled overlay driver (drives
  `M3Surface.StateOverlayOpacity`); integrates with `RippleElement`.
- `RippleElement` — child `VisualElement` that renders the M3 press ripple.
- `M3Animate` — schedule-based tween helper (`M3Animate.Float(...)`, ease-out cubic, no DOTween).
- `TypographyResolver` — applies a type role to a `Label`.
- `ThemeSwitchButton` — sample light/dark toggle control.

### `Runtime/ScriptableObjects/` (`PFound.UISystem`)
- `ThemeData` — SO holding the 27-role color palette, elevation presets, shape tokens, motion presets.
- `TypographyConfig` — SO holding the 15-role type scale + font references.

### `Runtime/Utils/` (`PFound.UISystem.Utils`)
- `MaterialSymbols` — static class of 60+ Unicode codepoint constants for Material Symbols glyphs
  (use with the `.m3-icon` USS class; no Painter2D icon drawing).

### `Runtime/Enums/` (`PFound.UISystem.Enums`)
- Component variant enums (`ButtonVariant`, etc.).

### `Runtime/Components/` (`PFound.UISystem.Components`)
- ~30 M3 component elements (`M3Button`, `M3Card`, `M3Toggle`, `M3TextField`, `M3Dialog`,
  `M3Snackbar`, navigation, pickers, …), each extending `M3ComponentBase`.

## Public API

### `ThemeManager` (static, `PFound.UISystem.Core`)
```csharp
static bool IsInitialized { get; }
static ThemeData ActiveTheme { get; }
static TypographyConfig TypographyConfig { get; }
static event Action<ThemeData> OnThemeChanged;

static void Initialize(ThemeData lightTheme, ThemeData darkTheme,
                       StyleSheet lightSheet, StyleSheet darkSheet,
                       TypographyConfig typographyConfig = null, Font defaultFont = null);
static void SetTheme(ThemeData theme);   // swaps USS + notifies subscribers same frame
static void ToggleLightDark();
static void RegisterPanel(UIDocument doc);    // idempotent
static void UnregisterPanel(UIDocument doc);
static void SyncToPanel(UIDocument doc);
```

### `M3ComponentBase` extension surface (`PFound.UISystem.Core`)
```csharp
public bool Disabled { get; set; }                 // toggles .m3-disabled, freezes SDF colors
protected virtual void BuildVisualTree();          // build hierarchy here
protected virtual void RefreshThemeColors();       // apply non-CSS (exception-registry) colors
protected virtual void OnDisabledChanged(bool disabled);
protected void InitStateLayer(VisualElement container, RippleElement ripple = null);
public StateLayerController StateLayer { get; }
```
`M3ComponentBase` auto-subscribes to `ThemeManager.OnThemeChanged` on `AttachToPanelEvent`,
unsubscribes and detaches the state layer on `DetachFromPanelEvent`, and calls
`RefreshThemeColors()` after each theme swap. The authoring rules (USS-only theming, exception
registry, icon/animation/typography conventions) live in [COMPONENT-GUIDE.md](COMPONENT-GUIDE.md) —
this doc does not restate them.

### Component construction
Components are `[UxmlElement]` custom elements — usable from UXML or constructed directly:
```csharp
using PFound.UISystem.Components;
using PFound.UISystem.Enums;

var button = new M3Button { Text = "Click Me", Variant = ButtonVariant.Filled };
button.OnClick += () => Debug.Log("clicked");
rootVisualElement.Add(button);
```

### SDF foundation (for non-M3 UI)
```csharp
int slot = SdfShapePalette.Resolve(Color.cyan);      // palette-index a fill color
Material mat = SdfShapeMaterials.GetMaterial(config); // shared material per SdfShapeConfig
```

## Setup / wiring

`ThemeManager` is a **static class — there is no host MonoBehaviour to place and nothing to keep
alive across scenes.** It has two initialization paths:

**Automatic (recommended, zero code).** Put the theme assets under a `Resources/UISystem/` folder;
`ThemeBootstrapper` self-initializes via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` before
the first scene loads — no bootstrap code, no scene object. Expected asset names:

```
Resources/UISystem/DefaultLight        (ThemeData)
Resources/UISystem/DefaultDark         (ThemeData)
Resources/UISystem/light               (StyleSheet)
Resources/UISystem/dark                (StyleSheet)
Resources/UISystem/DefaultTypography   (TypographyConfig, optional)
Resources/UISystem/Roboto-Regular      (Font, optional)
```

**Manual.** If you don't use the Resources convention, call `Initialize` once from your own boot
code (guard on `ThemeManager.IsInitialized`):

```csharp
ThemeManager.Initialize(lightTheme, darkTheme, lightSheet, darkSheet /*, typographyConfig, defaultFont */);
```

Either way, `ThemeManager` syncs `ThemeData` values to USS custom properties for every managed
panel, so all `var(--m3-*)` variables resolve automatically. Panels present at init are picked up
automatically. Any `UIDocument` **spawned at runtime** must register itself to receive the theme
sync — call `ThemeManager.RegisterPanel(doc)` from its `Start()` (idempotent; safe to call twice).

Switch themes at runtime with `ThemeManager.SetTheme(themeData)` or
`ThemeManager.ToggleLightDark()`; USS custom properties update and components restyle instantly.

## File Structure

```
UISystem/
├── README.md                  ← thin landing page
├── MODULE.md                  ← this file (architecture)
├── GUIDE.md                   ← doc map
├── COMPONENT-GUIDE.md         ← component-authoring rules
├── GUIDELINES.md              ← implementation lessons / gotchas
├── SCOPE.md                   ← scope, work packages, roadmap
├── TODO.md, PAPERCUT*.md      ← issue tracker + Papercut sample theme docs
├── Runtime/                   (PFound.UISystem)
│   ├── Shapes/                ← SdfShape, SdfShapeConfig/Materials/Palette,
│   │                            GpuSdfElement, SdfPanel, ThemedSdfPanel, UIShape.shader + *.hlsl
│   ├── Components/            ← ~30 M3 component elements
│   │   └── M3/                ← M3Surface : SdfShape, M3Label
│   ├── Core/                  ← ThemeManager, ThemeBootstrapper, M3ComponentBase,
│   │                            StateLayerController, RippleElement, M3Animate, TypographyResolver
│   ├── ScriptableObjects/     ← ThemeData, TypographyConfig
│   ├── Utils/                 ← MaterialSymbols
│   ├── Enums/, Data/, Themes/ ← variant enums, data structs, Papercut theme runtime
├── Styles/                    ← USS: Themes/ (light, dark, Papercut), Components/, typography, state-layer
├── UXML/                      ← component templates + showcase + Papercut pages
├── Editor/                    (PFound.UISystem.Editor)
│   ├── PageBuilder/           ← Page Builder window + component registry/palette/exporter
│   ├── ColorGeneration/       ← DynamicColorGenerator + HCT/tonal-palette math
│   ├── MenuItems/             ← UISystemMenuItems, visual baseline capture
│   ├── Inspectors/, Setup/, Shapes/
├── Resources/UISystem/        ← auto-bootstrap theme assets
├── Assets/                    ← Themes, Typography (fonts), PanelSettings
├── Scenes/                    ← Showcase scene
└── Tests/, Tests/PlayMode/    ← EditMode + PlayMode suites
```

## Editor tooling

- **Page Builder** (`Game Tools > Page Builder`) — categorized M3 component palette; creates UXML
  pages with correct style references and inserts components via XML manipulation.
- **Context-menu shortcuts** (`Assets > Create > UISystem > …`) — generate configured UXML templates.
- **Dynamic Color / Material You** (`Assets > UISystem > Generate Theme from Seed Color`) — pure-C#
  HCT color math produces light + dark `ThemeData` assets from a seed color (author-time only).

## Downstream Dependents

None required within PFound (standalone submodule). Consumed by game projects (the Playnest shell
is the reference consumer). An editor asset-provider hook (`Editor/Setup/UISystemAssetProvider.cs`)
lets a host project supply GameSpecific theme assets without an asmdef dependency.

## Limitations / Known Gaps

- **UI Toolkit only — no uGUI support.** All components render via `VisualElement` + USS + `M3Surface`.
  uGUI `Canvas`-based projects cannot reuse them.
- **No drag-and-drop primitives built in.** No drag source / drop target / drag preview API; implement
  custom `PointerDown/Move/Up` handlers or a game-specific drag utility. No touch drag gestures provided.
- **DOTween does not work with `VisualElement`.** Use USS `transition` for simple state changes or
  `IVisualElementScheduler` / `M3Animate.Float()` for schedule-based tweens. `VisualElement` is not a `Transform`.
- **Flexbox layout — no list virtualization.** `M3List` / `M3ListItem` render all items into the tree.
  For 100+ item lists, use Unity's `ListView` or custom virtualization.
- **Text uses TextCore SDF fonts only.** No TMP integration; wrap TMP fonts for UI Toolkit separately.
- **`ThemeManager` is global.** No per-panel theme override API; multi-theme apps must implement their
  own theme scope.
- **No built-in localized-string binding.** `Label.text` does not auto-bind to a localization module;
  fetch the string in C# and assign it manually (or write a binding helper).
- **Dynamic color generation is Editor-time only.** Runtime seed-color palette switching is not supported.
- **URP required.** Built-in Render Pipeline is unsupported (the shader is a URP UI shader).

## M3 reference

Primary design reference: [m3.material.io](https://m3.material.io). UISystem does not copy M3
pixel-for-pixel — consult the M3 site for dp values, color-role mappings, state opacities, and
motion durations when a value is unknown. Design principles (elevation hierarchy, state feedback,
typography scale, shape identity, motion-as-context, color roles) are followed, not cloned.
