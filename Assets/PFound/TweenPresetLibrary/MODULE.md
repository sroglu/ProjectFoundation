# TweenPresetLibrary

> **Module group — UI & Presentation.** Sibling modules in this group: `UISystem`, `ScreenRouter`, `GuidedOnboardingFlow`, `MVC`. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

Authored, data-driven tween presets. A `TweenPreset` ScriptableObject bundles up to four channels
(fade, scale, move, rotate); a pure-C# `TweenPresetPlayer` evaluates them against an `ITweenTarget`
you tick each frame. Self-contained — a built-in Penner-family `EaseEvaluator`, no external tween
library.

## Assemblies

- `PFound.TweenPresetLibrary.Core` (`Core/Runtime/`) — engine-free (`noEngineReferences: true`), no
  references. Holds `Ease` + `EaseEvaluator` so the curve math stays mono/csc-testable.
- `PFound.TweenPresetLibrary` (`Runtime/`) — the preset SO, channels, targets, player, provider.
  References `PFound.TweenPresetLibrary.Core`.
- `PFound.TweenPresetLibrary.Editor` (`Editor/`, Editor platform only) — menu tools + preview window.
  References `PFound.TweenPresetLibrary` and `PFound.TweenPresetLibrary.Core`.
- `PFound.TweenPresetLibrary.Core.Tests` (`Core/Tests/`) and `PFound.TweenPresetLibrary.Tests`
  (`Tests/`) — test assemblies.

All asmdefs are `autoReferenced: false` — a consumer references `PFound.TweenPresetLibrary` explicitly.

## Dependencies

None outside Unity. No other PFound modules, no third-party packages, no scripting defines. The Core
assembly has zero references and no engine reference; the Runtime assembly depends only on Core plus
`UnityEngine`.

## Key Types

### `PFound.TweenPresetLibrary.Core` (`Core/Runtime/`)

- `Ease` — enum of easing curves: `Unset` (resolves to `OutQuad`), `Linear`, and the full In/Out/InOut
  families Sine…Bounce. Laid out In/Out/InOut per family so the evaluator splits a value into
  (family, direction) arithmetically.
- `EaseEvaluator` — static, allocation-free `Evaluate(ease, t, overshoot, period)` `[0..1] → float`
  curve. `DefaultEase = OutQuad`; `Unset` / out-of-range resolves to it.

### `PFound.TweenPresetLibrary` (`Runtime/`)

- `TweenPreset` — `ScriptableObject` bundling `Fade`, `Scale`, `Move`, `Rotate` channels.
- `TweenChannel<T>` — abstract timed/eased channel (`Delay`, `Duration`, `Ease`, `EndValue`,
  `ForceInitialValue` / `ForcedInitialValue`). Concrete: `FadeChannel` (`float`), `ScaleChannel`
  (`Vector3`), `MoveChannel` (`Vector2`), `RotateChannel` (`Vector3` euler degrees).
- `TweenChannelKind` — `byte` enum `Empty`, `Fade`, `Scale`, `Move`, `Rotate`.
- `ITweenTarget` — the mutable surface the player drives (`Alpha`, `Scale`, `Position`, `EulerAngles`).
- `ComponentTweenTarget` — the shipped `ITweenTarget` adapter over a `Transform` + optional
  `CanvasGroup`.
- `TweenPresetPlayer` — pure-C# applier: ticks a preset against a target.
- `ITweenPresetProvider` — resolves presets by string key.
- `InjectedTweenPresetProvider` — in-memory `ITweenPresetProvider` over an injected preset set.

## Public API

### Model

- **`TweenPreset`** (ScriptableObject) holds four channels: `Fade`, `Scale`, `Move`, `Rotate`.
  `bool HasAny` is true when at least one channel is playable; `float MaxDuration` is the preset's
  total runtime (the longest channel's `TotalDuration`). On deserialize, non-playable channels reset
  to a clean default so stray authored values on an empty channel don't linger.
- **`TweenChannel<T>`** — each channel carries `Delay`, `Duration`, `Ease`, `EndValue`, plus
  `ForceInitialValue` / `ForcedInitialValue` (start from an explicit value instead of the target's
  current one). `TotalDuration = Delay + Duration`; `IsPlayable` when that is `> 0`. `Kind` reports
  the channel's `TweenChannelKind`.
- **`Ease`** (engine-free Core) — `Unset` (resolves to `OutQuad`), `Linear`, and the full In/Out/InOut
  families Sine…Bounce. `EaseEvaluator.Evaluate(Ease ease, float t, float overshoot = 1.70158f, float
  period = 0f)` is a pure, allocation-free `[0..1] → float` curve (`overshoot` drives Back/Elastic,
  `period` drives Elastic).

### Play a preset — `TweenPresetPlayer` (pure C#)

- `TweenPresetPlayer(TweenPreset preset, ITweenTarget target)` — fail-fast; both args required (throws
  `ArgumentNullException` on either).
- `void Tick(float deltaTime)` — the **first** tick captures each channel's initial value from the
  target (or `ForcedInitialValue` when forced); each tick advances `Elapsed`, evaluates every playable
  channel, and writes to the target. Non-playable channels are left untouched.
- `void Restart()` — re-arm; the next tick recaptures initials and `Elapsed` restarts from 0.
- `float Elapsed`, `bool IsDone` (`Elapsed >= preset.MaxDuration`).

The injected `deltaTime` (never `Time.deltaTime` internally) makes the player deterministic and
unit-testable.

### Target surface — `ITweenTarget`

`float Alpha`, `Vector3 Scale`, `Vector2 Position`, `Vector3 EulerAngles` (all get/set). The shipped
adapter is `ComponentTweenTarget`:

- `ComponentTweenTarget(Transform transform, CanvasGroup canvasGroup = null)` — `Scale` →
  `localScale`, `EulerAngles` → `localEulerAngles`, `Position` → `anchoredPosition` for a
  `RectTransform` else `localPosition.x/y`, `Alpha` → the `CanvasGroup` (reads `1` / ignores writes
  when none is supplied, so transform-only presets work).

### Lookup by key — `ITweenPresetProvider`

- `bool TryGet(string key, out TweenPreset preset)`, `bool Contains(string key)`,
  `IReadOnlyCollection<string> Keys`.
- Concrete `InjectedTweenPresetProvider(IEnumerable<TweenPreset> presets, Func<TweenPreset,string>
  keySelector = null)` — keyed by `preset.name` by default; null presets and null/empty keys are
  skipped; duplicate keys are last-wins.

## Setup / wiring

**1. Author a preset.** Create ▸ `PFound/Tween Preset Library/Tween Preset` (or Tools ▸
`PFound/Tween Preset Library/Create Tween Preset`), then set the delay/duration/ease/end value on each
channel you want. Reference it from a MonoBehaviour via `[SerializeField] TweenPreset`.

**2. Wire a target + player and tick it.** The player is a plain object with no lifecycle of its own —
a thin MonoBehaviour owns it and pumps `Tick`:

```csharp
public sealed class PanelIntro : MonoBehaviour
{
    [SerializeField] TweenPreset _enter;
    [SerializeField] CanvasGroup _group;   // omit for transform-only presets

    TweenPresetPlayer _player;

    void Awake() => _player = new TweenPresetPlayer(
        _enter, new ComponentTweenTarget(transform, _group));

    public void Play() => _player.Restart();

    void Update()
    {
        if (!_player.IsDone) _player.Tick(Time.deltaTime);
    }
}
```

Decisions a consumer makes:

- **Who owns the player and ticks it.** Any MonoBehaviour (or update pump) — the player is pure C#;
  gate the `Tick` on `!IsDone` and call `Restart()` to replay.
- **CanvasGroup or not.** Pass a `CanvasGroup` to `ComponentTweenTarget` only if the preset fades;
  transform-only presets need just the `Transform`.
- **Direct vs keyed lookup.** For a fixed preset, serialize the `TweenPreset` directly. For a named
  library, build one `InjectedTweenPresetProvider` at startup from your authored presets (an
  addressable / content-delivery load, or a serialized list) and resolve with `TryGet(key, out
  preset)`. There is no auto-loading seam — the provider is the DI point; you decide where the presets
  come from.

Reference `PFound.TweenPresetLibrary` from a consumer asmdef (it in turn references the engine-free
`PFound.TweenPresetLibrary.Core`).

## File Structure

```
TweenPresetLibrary/
├── Core/
│   ├── Runtime/                         PFound.TweenPresetLibrary.Core (engine-free, no deps)
│   │   ├── Ease.cs                      easing-curve enum
│   │   └── EaseEvaluator.cs             pure [0..1] → float curve evaluator
│   └── Tests/                           PFound.TweenPresetLibrary.Core.Tests
│       └── Program.cs                   standalone csc/mono runner over EaseEvaluator
├── Runtime/                             PFound.TweenPresetLibrary
│   ├── TweenPreset.cs                   ScriptableObject bundling the four channels
│   ├── TweenChannel.cs                  TweenChannel<T> + Fade/Scale/Move/RotateChannel
│   ├── TweenChannelKind.cs             channel-kind enum
│   ├── ITweenTarget.cs                  the mutable surface the player drives
│   ├── ComponentTweenTarget.cs          Transform + optional CanvasGroup adapter
│   ├── TweenPresetPlayer.cs             pure-C# applier (Tick / Restart / Elapsed / IsDone)
│   ├── ITweenPresetProvider.cs          keyed lookup interface
│   └── InjectedTweenPresetProvider.cs   in-memory provider over an injected preset set
├── Editor/                              PFound.TweenPresetLibrary.Editor (Editor only)
│   ├── TweenPresetEditorTools.cs        menu items (create / preview / normalize)
│   └── TweenPresetPreviewWindow.cs      non-destructive curve + channel preview window
└── Tests/                               PFound.TweenPresetLibrary.Tests (Unity NUnit)
    ├── TweenPresetTests.cs
    ├── TweenPresetPlayerTests.cs
    ├── InjectedTweenPresetProviderTests.cs
    └── TweenPresetEditorToolsTests.cs
```

## Downstream Dependents

- `PFound.GuidedOnboardingFlow` references `PFound.TweenPresetLibrary.Core` for its easing curves
  (shared motion vocabulary), not the full player/preset runtime.

No other PFound module depends on this one.

## Limitations / Known Gaps

- **No auto-loading seam.** The module never calls `Resources.Load`; loading presets (addressable /
  content-delivery / serialized list) and populating a provider is the consumer's job.
- **No built-in update pump.** The player is pure C# and must be ticked by a host each frame; nothing
  auto-plays, loops, or reverses beyond `Restart()`.
- **Fixed four channels.** A preset drives exactly fade / scale / move / rotate; there is no color,
  material, or arbitrary-property channel.
- **`ComponentTweenTarget` is 2D-position oriented.** `Position` is a `Vector2` (anchored for a
  `RectTransform`, else local x/y, preserving z); there is no full 3D-position channel.
- **Alpha needs a CanvasGroup.** With no `CanvasGroup`, `Alpha` reads `1` and ignores writes, so a
  fade channel is silently a no-op on such a target.

## Editor tools

Under the `PFound/Tween Preset Library/` menu:

- **Create Tween Preset** — author a new `TweenPreset` asset.
- **Open Preview Window** — the `TweenPresetPreviewWindow`: assign a preset to inspect each channel's
  playability and see its eased 0..1 curve plotted (overshoot-aware, non-destructive).
- **Normalize Selected Preset Name(s)** — tidy selected preset asset names.

## Testing

- `Core/Tests/` (`Program.cs`) — standalone `csc`/`mono` runner over the engine-free `EaseEvaluator`.
- `Tests/` — Unity NUnit suite for the player, preset serialization, the provider, and the editor
  tools.
