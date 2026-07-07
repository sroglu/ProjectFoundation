# PFound.TweenPresetLibrary

Authored, data-driven tween presets: a `TweenPreset` ScriptableObject bundles up to four channels
(fade, scale, move, rotate), and a pure-C# `TweenPresetPlayer` evaluates them against an
`ITweenTarget` you tick each frame. Self-contained — a built-in Penner-family `EaseEvaluator`, no
external tween library.

## Model

- **`TweenPreset`** (ScriptableObject) holds four channels: `Fade`, `Scale`, `Move`, `Rotate`.
  `HasAny` is true when at least one channel is playable; `MaxDuration` is the preset's total runtime.
- **`TweenChannel<T>`** — each channel carries `Delay`, `Duration`, `Ease`, `EndValue`, plus
  `ForceInitialValue` / `ForcedInitialValue` (start from an explicit value instead of the target's
  current one). `TotalDuration = Delay + Duration`; `IsPlayable` when that is > 0. Concrete channels:
  `FadeChannel` (`float`), `ScaleChannel` (`Vector3`), `MoveChannel` (`Vector2`), `RotateChannel`
  (`Vector3` euler degrees). `TweenChannelKind`: `Empty`, `Fade`, `Scale`, `Move`, `Rotate`.
- **`Ease`** (in the engine-free Core) — `Unset` (resolves to `OutQuad`), `Linear`, and the full
  In/Out/InOut families Sine…Bounce. `EaseEvaluator.Evaluate(ease, t, overshoot, period)` is a pure,
  allocation-free `[0..1] → float` curve.

## Public API

**Play a preset** — `TweenPresetPlayer` (pure C#):
- `new TweenPresetPlayer(TweenPreset preset, ITweenTarget target)` — fail-fast; both args required.
- `void Tick(float deltaTime)` — the **first** tick captures each channel's initial value from the
  target; each tick advances `Elapsed`, evaluates every playable channel, and writes to the target.
- `void Restart()` — re-arm; next tick recaptures initials, `Elapsed` → 0.
- `float Elapsed`, `bool IsDone` (`Elapsed >= preset.MaxDuration`).

Note the injected `deltaTime` (not `Time.deltaTime` internally) — deterministic and unit-testable.

**Target surface** — `ITweenTarget`: `float Alpha`, `Vector3 Scale`, `Vector2 Position`,
`Vector3 EulerAngles` (all get/set). The shipped adapter is `ComponentTweenTarget`:
- `new ComponentTweenTarget(Transform transform, CanvasGroup canvasGroup = null)` — `Scale` →
  `localScale`, `EulerAngles` → `localEulerAngles`, `Position` → `anchoredPosition` for a
  `RectTransform` else `localPosition.x/y`, `Alpha` → the `CanvasGroup` (reads 1 / ignores writes
  when none is supplied, so transform-only presets work).

**Lookup by key** — `ITweenPresetProvider`: `bool TryGet(string key, out TweenPreset)`,
`bool Contains(string)`, `IReadOnlyCollection<string> Keys`. Concrete
`InjectedTweenPresetProvider(IEnumerable<TweenPreset> presets, Func<TweenPreset,string> keySelector =
null)` — keyed by `preset.name` by default; null/empty keys skipped, duplicate keys last-wins.

## Setup / wiring

**1. Author a preset.** Create ▸ `PFound/Tween Preset Library/Tween Preset` (or Tools ▸
`PFound/Tween Preset Library/Create Tween Preset`), then set the delay/duration/ease/end value on
each channel you want. Reference it from a MonoBehaviour via `[SerializeField] TweenPreset`.

**2. Wire a target + player and tick it.** The player is a plain object with no lifecycle of its
own — a thin MonoBehaviour owns it and pumps `Tick`:

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
  library, build one `InjectedTweenPresetProvider` at startup from your authored presets
  (`Resources.LoadAll<TweenPreset>(...)`, an addressable/content-delivery load, or a serialized list)
  and resolve with `TryGet(key, out preset)`. There is no auto-loading seam — the provider is the DI
  point; you decide where the presets come from.

Reference `PFound.TweenPresetLibrary` from a consumer asmdef (it in turn references the engine-free
`PFound.TweenPresetLibrary.Core`).

## Editor tools

Under the `PFound/Tween Preset Library/` menu: create a preset, open the **preview window** (assign
a preset to inspect each channel's playability and see its eased 0..1 curve plotted, overshoot-aware,
non-destructive), and normalize selected preset asset names.

## Testing

- `Core/Tests/` — standalone `csc`/`mono` runner over the engine-free `EaseEvaluator`.
- `Tests/` — Unity NUnit suite for the player, preset serialization, provider, and editor tools.

## Layout

- `Core/Runtime/` — `Ease`, `EaseEvaluator`. Assembly `PFound.TweenPresetLibrary.Core` (no engine,
  no deps).
- `Runtime/` — `TweenPreset`, channels, `ITweenTarget` / `ComponentTweenTarget`, `TweenPresetPlayer`,
  `ITweenPresetProvider` / `InjectedTweenPresetProvider`. Assembly `PFound.TweenPresetLibrary`.
- `Editor/` — editor tools + preview window. Assembly `PFound.TweenPresetLibrary.Editor`.
- `Tests/` + `Core/Tests/`.
