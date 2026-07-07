# PFound.TweenPresetLibrary

Authored, data-driven tween presets: a `TweenPreset` ScriptableObject bundles up to four channels
(fade, scale, move, rotate), and a pure-C# `TweenPresetPlayer` evaluates them against an
`ITweenTarget` you tick each frame. Self-contained — a built-in Penner-family `EaseEvaluator`, no
external tween library.

## Quick reference

Author a preset (Create ▸ `PFound/Tween Preset Library/Tween Preset`), then own the player from a host
and tick it:

```csharp
[SerializeField] TweenPreset _enter;
[SerializeField] CanvasGroup _group;   // omit for transform-only presets

var player = new TweenPresetPlayer(_enter, new ComponentTweenTarget(transform, _group));

void Update() { if (!player.IsDone) player.Tick(Time.deltaTime); }  // player.Restart() to replay
```

## Dependencies

None outside Unity — Runtime depends only on the engine-free `PFound.TweenPresetLibrary.Core`.

## Docs

Deep reference: [MODULE.md](MODULE.md)
