# PFound.GuidedOnboardingFlow

A guided, step-driven onboarding/tutorial engine for Unity. A `TutorialManager` runs one tutorial
at a time — an ordered list of steps (dialog, focus-an-element, open-a-screen, wait-for-signal) —
driven manually or by one or more triggers (AND-combined). The state machine is engine-free
(`Core`); the Unity layer adds overlay UI seams, ScriptableObject authoring, and a DI installer.

Capabilities: per-step readiness verdicts (proceed / defer / abort / **skip+advance** /
**go-back-one**); per-step **timeout outcome** (advance vs abort-run) with a per-tutorial default;
**replay policies** (once-account / once-session / repeatable) over an in-memory, session, or
composite completion store; **auto-click** fast-forward that synthesizes a real pointer event on the
target; a focus step with anchor-or-`GameObject.Find` targeting, sprite-shaped spotlight, hand hint,
and per-anchor offset/padding/mask; a dialog with a **typewriter** reveal and tap/auto/both dismiss;
an input blocker that re-executes press/release/click on the real target so the game's own handler
fires; multi-anchor-per-tag registration; and `LogVerbosity`-gated lifecycle logging with startup
dup/empty-id validation.

## Quick reference

```csharp
// After wiring the TutorialInstaller's inspector slots, from your composition root:
ITutorialManager manager = _installer.Install(container);   // router/signals/store optional
manager.StepChanged += (id, index) => { /* ... */ };
manager.SetAutoAdvance(true, perStepDelaySeconds: 0.5f);    // scripted fast-forward / regression
manager.TryStart(new TutorialId(1));                        // respects completion; or an Automatic trigger fires
// A bound TutorialRunnerHost calls manager.Tick(...) every frame — the manager does not self-tick.
```

## Dependencies

`PFound.DependencyContainer`, `PFound.Signaling`, `PFound.ScreenRouter`,
`PFound.TweenPresetLibrary.Core`, `PFound.UserPrefs` (the engine-free `Core` assembly has none).

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, key types, full public API, and the complete
Setup / wiring guide (installer, tick host, overlay canvas, catalog, anchors).
</content>
