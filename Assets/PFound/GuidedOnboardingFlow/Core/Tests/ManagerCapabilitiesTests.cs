using System.Collections.Generic;

namespace PFound.GuidedOnboardingFlow.Core.Tests
{
    /// <summary>Covers the manager-level parity capabilities: multi-trigger AND + re-arm, replay policy,
    /// completion-respecting start, step-change signalling, and startup id validation.</summary>
    internal static class ManagerCapabilitiesTests
    {
        private sealed class Fixture
        {
            public ProbeStep Last;
            public readonly TutorialBlueprint Blueprint;

            public Fixture(
                int id,
                TriggerMode mode,
                IReadOnlyList<ITutorialTrigger> triggers,
                ReplayPolicy replay = ReplayPolicy.OnceAccount,
                int stepCount = 1)
            {
                Blueprint = new TutorialBlueprint(
                    new TutorialId(id), "T" + id, mode,
                    () =>
                    {
                        var steps = new List<ITutorialStep>();
                        for (int i = 0; i < stepCount; i++)
                        {
                            Last = new ProbeStep();
                            steps.Add(Last);
                        }
                        return steps;
                    },
                    triggers, replay: replay);
            }
        }

        public static void Run()
        {
            TestKit.Run("Multiple triggers combine with AND", () =>
            {
                var t1 = new ManualTrigger();
                var t2 = new ManualTrigger { Armed = false };
                var f = new Fixture(1, TriggerMode.Automatic, new ITutorialTrigger[] { t1, t2 });
                var m = new TutorialManager(new[] { f.Blueprint }, new InMemoryCompletionStore());

                m.Tick(0.1f);
                TestKit.IsFalse(m.IsRunning, "not started while one trigger is unarmed");

                t2.Armed = true;
                m.Tick(0.1f);
                TestKit.IsTrue(m.IsRunning, "starts once every trigger fires");
            });

            TestKit.Run("Triggers are re-armed when the run ends", () =>
            {
                var t = new ManualTrigger();
                var f = new Fixture(1, TriggerMode.Automatic, new ITutorialTrigger[] { t }, ReplayPolicy.Repeatable);
                var m = new TutorialManager(new[] { f.Blueprint }, new InMemoryCompletionStore());

                m.Tick(0.1f);              // fires and starts
                TestKit.IsTrue(m.IsRunning, "running");
                f.Last.FinishNext();
                m.Tick(0.1f);              // completes → Retire → ResetTriggers
                TestKit.IsTrue(t.Resets >= 1, "trigger reset on end");
            });

            TestKit.Run("OnceAccount blocks a re-run; Repeatable allows it", () =>
            {
                var store = new InMemoryCompletionStore();
                var once = new Fixture(1, TriggerMode.Manual, null, ReplayPolicy.OnceAccount);
                var repeat = new Fixture(2, TriggerMode.Manual, null, ReplayPolicy.Repeatable);
                var m = new TutorialManager(new[] { once.Blueprint, repeat.Blueprint }, store);

                // Run the OnceAccount tutorial to completion.
                TestKit.IsTrue(m.TryStart(new TutorialId(1)), "once started");
                m.Tick(0.1f);
                once.Last.FinishNext();
                m.Tick(0.1f);
                TestKit.IsFalse(m.TryStart(new TutorialId(1)), "once refuses to replay");

                // Repeatable can run again and again.
                TestKit.IsTrue(m.TryStart(new TutorialId(2)), "repeatable started");
                m.Tick(0.1f);
                repeat.Last.FinishNext();
                m.Tick(0.1f);
                TestKit.IsTrue(m.TryStart(new TutorialId(2)), "repeatable replays");
            });

            TestKit.Run("Skip records completion under the replay policy", () =>
            {
                var store = new InMemoryCompletionStore();
                var f = new Fixture(1, TriggerMode.Manual, null, ReplayPolicy.OnceAccount);
                var m = new TutorialManager(new[] { f.Blueprint }, store);

                m.TryStart(new TutorialId(1));
                m.Tick(0.1f);
                m.Skip();
                TestKit.IsTrue(store.IsCompleted(new TutorialId(1)), "skip marks completed");
                TestKit.IsFalse(m.TryStart(new TutorialId(1)), "skipped tutorial won't re-nag");
            });

            TestKit.Run("StepChanged fires on start and each advance", () =>
            {
                var f = new Fixture(1, TriggerMode.Manual, null, ReplayPolicy.OnceAccount, stepCount: 2);
                var m = new TutorialManager(new[] { f.Blueprint }, new InMemoryCompletionStore());
                var indices = new List<int>();
                m.StepChanged += (id, i) => indices.Add(i);

                m.TryStart(new TutorialId(1));   // fires StepChanged(0)
                // The two ProbeSteps are built together; drive them via the runner's current step.
                ((ProbeStep)m.Active.CurrentStep).FinishNext();
                m.Tick(0.1f);                    // step0 completes → step1 begins → StepChanged(1)

                TestKit.IsTrue(indices.Count >= 2, "at least two step-change signals");
                TestKit.AreEqual(0, indices[0], "first is index 0");
                TestKit.AreEqual(1, indices[1], "then index 1");
            });

            TestKit.Run("Startup validation warns on duplicate and empty ids", () =>
            {
                var log = new RecordingLog();
                var a = new Fixture(1, TriggerMode.Manual, null);
                var dup = new Fixture(1, TriggerMode.Manual, null);     // duplicate id
                var empty = new TutorialBlueprint(
                    TutorialId.None, "Nameless", TriggerMode.Manual,
                    () => new List<ITutorialStep> { new ProbeStep() });

                _ = new TutorialManager(new[] { a.Blueprint, dup.Blueprint, empty }, new InMemoryCompletionStore(), log);

                int warnings = 0;
                foreach (LogSeverity s in log.Severities)
                    if (s == LogSeverity.Warning)
                        warnings++;
                TestKit.AreEqual(2, warnings, "one dup + one empty-id warning");
            });
        }
    }
}
