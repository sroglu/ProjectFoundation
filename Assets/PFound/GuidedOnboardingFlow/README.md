# PFound.GuidedOnboardingFlow

A guided, step-driven onboarding/tutorial engine for Unity. A `TutorialManager` runs one tutorial
at a time — an ordered list of steps (dialog, focus-an-element, open-a-screen, wait-for-signal) —
driven manually or by a trigger. The state machine is engine-free (`Core`); the Unity layer adds
overlay UI seams, ScriptableObject authoring, and a DI installer.

## Quick reference

```csharp
// After wiring the TutorialInstaller's inspector slots, from your composition root:
ITutorialManager manager = _installer.Install(container);   // router/signals/store optional
manager.TutorialStarted += id => { /* ... */ };
manager.TryStart(new TutorialId(1));                        // or let an Automatic trigger fire
// A bound TutorialRunnerHost calls manager.Tick(...) every frame — the manager does not self-tick.
```

## Dependencies

`PFound.DependencyContainer`, `PFound.Signaling`, `PFound.ScreenRouter`,
`PFound.TweenPresetLibrary.Core`, `PFound.UserPrefs` (the engine-free `Core` assembly has none).

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, key types, full public API, and the complete
Setup / wiring guide (installer, tick host, overlay canvas, catalog, anchors).
</content>
