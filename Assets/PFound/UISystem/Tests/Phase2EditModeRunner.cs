#if UNITY_INCLUDE_TESTS
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PFound.UISystem.Tests
{
    /// <summary>
    /// Editor menu utility to dispatch Phase 2 (spec 009) EditMode tests + write results
    /// to disk. Mirrors <see cref="Phase1PlayModeRunner"/>'s persistent-recorder pattern
    /// — TestRunnerApi callbacks fire across domain reloads only when the recorder lives
    /// in a persistent assembly (not a dynamic Unity_RunCommand script).
    /// </summary>
    [InitializeOnLoad]
    internal static class Phase2EditModeRunner
    {
        public const string OutputPath = "Temp/Phase2Tests.txt";
        private const string PendingKey = "UISystem.Phase2EditMode.Pending";
        private static TestRunnerApi _api;
        private static Recorder _rec;

        static Phase2EditModeRunner()
        {
            if (SessionState.GetBool(PendingKey, false)) Register();
        }

        [MenuItem("Tools/UISystem/Run Phase 2 EditMode Tests")]
        public static void Run()
        {
            SessionState.SetBool(PendingKey, true);
            File.WriteAllText(OutputPath, "=== Phase 2 EditMode (spec 009, T011 + T012) ===\n");
            Register();
            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "PFound.UISystem.Tests" },
                // Unity TestRunner quirk: assemblyNames filter alone yields 0 tests; explicit
                // testNames list is required for discovery to pick up these methods.
                testNames = new[]
                {
                    // T011 — SdfShape API surface (14 methods)
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.CornerRadius_DefaultIs12",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.CornerRadius_SetGet_RoundTrips",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.PerCornerRadii_DefaultIsMinusOne_MeaningInheritUniform",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.Shadow_AllSurfacesRoundTrip",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.Shadow_RGBA_UxmlAttrs_AffectShadowColorStruct",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.ShadowBlur_NegativeClampsToZero",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.ShadowPadding_NegativeClampsToZero",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.Outline_RoundTrips",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.OutlineThickness_NegativeClampsToZero",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.FillColorOverride_NullByDefault",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.FillColorOverride_SetGet_RoundTrips",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.FillColorOverride_Null_FallsBackToDefault_PaletteSlot0",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.FR009_NoShadowConfig_UsesNoShadowMaterial",
                    "PFound.UISystem.Tests.Shapes.SdfShapeApiSurfaceTests.FR009_AnyShadowField_TriggersWithShadowMaterial",
                    // T012 — M3Surface layer (7 methods)
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.M3Surface_IsA_SdfShape",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.M3Surface_InheritsSdfShapeProperties",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.TonalOverlayOpacity_DefaultZero_ClampsToZeroOne",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.StateOverlayOpacity_DefaultZero_ClampsToZeroOne",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.OverlayColors_RoundTrip",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.Ripple_PropertiesRoundTrip",
                    "PFound.UISystem.Tests.Shapes.M3SurfaceLayerTests.TintEncoding_StateAndTonalOpacities_PackIntoGAndBChannels_AlphaForcedTo255",
                    // T013 — SdfShape batching (1 EditMode test — PaletteOverflow; PlayMode tests run via separate runner)
                    "PFound.UISystem.Tests.Shapes.SdfShapeBatchingTests.PaletteOverflow_ThrowsUISystemPaletteOverflowException",
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
