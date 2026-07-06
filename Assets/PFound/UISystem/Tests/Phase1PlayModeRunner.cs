#if UNITY_INCLUDE_TESTS
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PFound.UISystem.Tests
{
    /// <summary>
    /// Editor utility to dispatch the Phase 1 PlayMode acceptance test (#6) and persist results
    /// to disk across the PlayMode domain reload that would otherwise GC a transient callback.
    /// Survives reload via <see cref="InitializeOnLoadAttribute"/> — the recorder re-registers
    /// itself on every domain rebuild while a run is in flight.
    /// </summary>
    [InitializeOnLoad]
    internal static class Phase1PlayModeRunner
    {
        public const string OutputPath = "Temp/Phase1TestRun.txt";
        private const string PendingKey = "UISystem.Phase1PlayMode.Pending";

        private static TestRunnerApi _api;
        private static Recorder _rec;

        static Phase1PlayModeRunner()
        {
            if (SessionState.GetBool(PendingKey, false))
                RegisterCallback();
        }

        [MenuItem("Tools/UISystem/Run Phase 1 PlayMode Acceptance Test")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            File.AppendAllText(OutputPath, "\n=== PlayMode (Phase 1 #6) ===\n");
            RegisterCallback();
            _api.Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.PlayMode,
                // PlayMode asmdef split 2026-05-28 — GpuSdfElementBatchingTests now lives in
                // PFound.UISystem.Tests.PlayMode.dll (sibling asmdef under Tests/PlayMode/).
                assemblyNames = new[] { "PFound.UISystem.Tests.PlayMode" },
                testNames = new[] { "PFound.UISystem.Tests.Shapes.GpuSdfElementBatchingTests.FiftyElementsSharingOneMaterial_BatchToAtMostThreeDrawCallDelta" }
            }));
        }

        private static void RegisterCallback()
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
