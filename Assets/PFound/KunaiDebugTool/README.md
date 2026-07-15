# KunaiDebugTool

Immediate-mode in-game debug overlay for Unity — the whole UI renders in **one draw call** with
**zero per-frame GC** via a Burst-compiled vertex pipeline. Runtime namespace `Kunai`, static entry
point `KUI.*`; sanctioned asmdef `PFound.KunaiDebugTool`.

## Quick reference

```csharp
using Kunai;

KUI.Initialize(fontAtlasTexture, fontMetricsTextAsset);   // once, at startup
KuiConsole.Initialize();                                  // optional: capture Unity logs
KUI.RegisterWindow(new KuiConsoleWindow());               // mount a tool window
// toggle at runtime with backtick (`) / F1, or KUI.IsVisible = true
```

## Dependencies

`Unity.Burst` + `Unity.Collections` + `Unity.Mathematics`. Standalone leaf — no other PFound
dependency.

## Setup at a glance

- Add `Hidden/KUI-Combined` to *Graphics → Always Included Shaders* (else it is stripped from builds).
- *Player → Active Input Handling* must be `Input Manager (Old)` or `Both` (legacy `Input` drives the
  toggle keys / touch).
- A `Camera.main` must exist (drives layout + the ortho projection).

Full detail in [MODULE.md](MODULE.md).

## Docs

- Deep reference: [MODULE.md](MODULE.md)
- History: [CHANGELOG.md](CHANGELOG.md)
