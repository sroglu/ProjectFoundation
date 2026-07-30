#if UNITY_INCLUDE_TESTS
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PFound.UISystem.Tests
{
    /// <summary>
    /// Editor menu utility to dispatch Phase 2 (spec 009) PlayMode batching tests
    /// (T013 — SdfShapePlayModeBatchingTests, two [UnityTest] methods).
    /// Uses the same persistent-recorder pattern as <see cref="Phase1PlayModeRunner"/>.
    /// </summary>
    [InitializeOnLoad]
    internal static class Phase2PlayModeRunner
    {
        public const string OutputPath = "Temp/Phase2PlayModeTests.txt";
        private const string PendingKey = "UISystem.Phase2PlayMode.Pending";
        private static TestRunnerApi _api;
        private static Recorder _rec;

        static Phase2PlayModeRunner()
        {
            if (SessionState.GetBool(PendingKey, false)) Register();
        }

        [MenuItem("Tools/UISystem/Run Phase 2 PlayMode Batching Tests")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            File.WriteAllText(OutputPath, "=== Phase 2 PlayMode (spec 009, T013) ===\n");
            Register();
            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "PFound.UISystem.Tests.PlayMode" },
                testNames = new[]
                {
                    "PFound.UISystem.Tests.Shapes.SdfShapePlayModeBatchingTests.FiftyElementsSharedConfig_BatchToAtMostThreeDrawCallDelta",
                    "PFound.UISystem.Tests.Shapes.SdfShapePlayModeBatchingTests.FiftyElementsWith15DifferentColors_StillBatchToAtMostThreeDraws",
                }
            }));
        }

        private static void Register()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _rec = new Recorder();
            _api.RegisterCallbacks(_rec);
        }

        private class Recorder : ICallbacks
        {
            public void RunStarted(ITestAdaptor t)
            {
                File.AppendAllText(OutputPath, "RUN STARTED: " + t.FullName + "\n");
            }

            public void RunFinished(ITestResultAdaptor r)
            {
                File.AppendAllText(OutputPath,
                    $"RUN FINISHED: pass={r.PassCount} fail={r.FailCount} skip={r.SkipCount} duration={r.Duration:F3}s\n");
                SessionState.EraseBool(PendingKey);
            }

            public void TestStarted(ITestAdaptor t) { }

            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                var msg = $"[{r.TestStatus.ToString().ToUpper()}] {r.Test.FullName} ({r.Duration:F3}s)";
                if (r.TestStatus == TestStatus.Failed)
                    msg += "\n    " + (r.Message ?? "(no msg)").Replace("\n", "\n    ");
                File.AppendAllText(OutputPath, msg + "\n");
            }
        }
    }
}
#endif
