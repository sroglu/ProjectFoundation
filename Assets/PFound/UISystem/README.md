# PFound.UISystem

A modular, reusable UI component library for Unity, built on UI Toolkit (UXML + USS + C#)
and inspired by Material Design 3 (M3). It pairs a GPU-SDF visual foundation with a runtime
theme system, an M3 type scale, and ~30 themed interactive components. Not a 1:1 M3
implementation — it borrows M3's research-backed sizing, spacing, state feedback, and color
hierarchy on a Unity-native, batched base.

## Quick reference

```csharp
using PFound.UISystem.Components;
using PFound.UISystem.Enums;

// 1. Create theme: Assets > Create > UISystem > Theme Data (or use bundled DefaultLight/DefaultDark).
// 2. Bootstrap ThemeManager — automatic: drop assets under Resources/UISystem/ (auto-inits before
//    first scene). Or manual, once from boot code:
ThemeManager.Initialize(lightTheme, darkTheme, lightSheet, darkSheet);

// 3. Add a component (UXML or C#):
var button = new M3Button { Text = "Click Me", Variant = ButtonVariant.Filled };
button.OnClick += () => Debug.Log("clicked");
rootVisualElement.Add(button);

// Runtime UIDocuments must register to receive theme sync:
ThemeManager.RegisterPanel(doc);   // from Start(); idempotent
ThemeManager.ToggleLightDark();    // or SetTheme(themeData)
```

## Requirements

Unity 6.3+ (6000.3), Universal Render Pipeline (URP), TextMeshPro / TextCore.

## Dependencies

No hard PFound-module dependency — standalone submodule (URP + TextMeshPro packages; Odin
Inspector attributes for authoring). Optional alongside AssetSystem.

## Docs

- **Architecture:** [MODULE.md](MODULE.md) — assemblies, key types, public API, ThemeManager wiring, limitations.
- **Doc map:** [GUIDE.md](GUIDE.md) — index of every doc + a "when to read what" table.
- **Authoring components:** [COMPONENT-GUIDE.md](COMPONENT-GUIDE.md) · **Implementation lessons:** [GUIDELINES.md](GUIDELINES.md) · **Scope & roadmap:** [SCOPE.md](SCOPE.md).

## License

MIT
