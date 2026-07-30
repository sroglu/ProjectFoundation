#if UNITY_INCLUDE_TESTS
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PFound.UISystem.Tests
{
    /// <summary>
    /// Editor menu utility to dispatch spec 009 T044 — M3 component batching tests
    /// (SC-002 sub-budgets: no-shadow, with-shadow, animated steady state). Uses the
    /// same persistent-recorder pattern as <see cref="Phase1PlayModeRunner"/>.
    /// </summary>
    [InitializeOnLoad]
    internal static class T044ComponentBatchingRunner
    {
        public const string OutputPath = "Temp/T044ComponentBatching.txt";
        private const string PendingKey = "UISystem.T044ComponentBatching.Pending";
        private static TestRunnerApi _api;
        private static Recorder _rec;

        static T044ComponentBatchingRunner()
        {
            if (SessionState.GetBool(PendingKey, false)) Register();
        }

        [MenuItem("Tools/UISystem/Run T044 Component Batching Tests")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            File.WriteAllText(OutputPath, "=== T044 Component Batching (spec 009 SC-002) ===\n");
            Register();
            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "PFound.UISystem.Tests.PlayMode" },
                testNames = new[]
                {
                    "PFound.UISystem.Tests.Components.M3ComponentBatchingTests.FiftyM3Cards_NoShadow_Variant_BatchesToAtMostThreeDraws",
                    "PFound.UISystem.Tests.Components.M3ComponentBatchingTests.FiftyM3Cards_WithShadow_BatchesWithinElevationBudget",
                    "PFound.UISystem.Tests.Components.M3ComponentBatchingTests.M3Slider_AnimatedThumb_DrawCallGrowthWithinNoShadowBudget",
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
                var msg = $"[{r.TestStatus.ToString().ToUpper()}] {r.Test.Name} ({r.Duration:F3}s)";
                if (r.TestStatus == TestStatus.Failed)
                    msg += "\n    " + (r.Message ?? "(no msg)").Replace("\n", "\n    ");
                File.AppendAllText(OutputPath, msg + "\n");
            }
        }
    }
}
#endif
