// Standalone mono/csc test runner for the engine-free boot-manifest core.
//
//   csc -nologo -warn:0 -out:/tmp/pf_startup.exe \
//       Assets/PFound/StartupOrchestration/Runtime/*.cs \
//       Assets/PFound/StartupOrchestration/Tests/*.cs && mono /tmp/pf_startup.exe
//
// Runtime/*.cs references a few UnityEngine symbols (Debug, Mathf, ScriptableObject,
// SerializeField, ...). This file supplies a tiny stand-in for exactly those symbols under the
// PF_STARTUP_TESTS define so the runner needs no Unity/dotnet — see the shim at the bottom.

#if PF_STARTUP_TESTS
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using PFound.StartupOrchestration;

internal static class BootManifestTests
{
    private static int _passed;
    private static int _failed;

    private static void Check(bool condition, string label)
    {
        if (condition) { _passed++; }
        else { _failed++; Console.WriteLine("FAIL: " + label); }
    }

    // A trivial step that finishes immediately.
    private sealed class NoopStep : IStartupStep
    {
        public WaitReason Reason => WaitReason.Systems;
        public Task ExecuteAsync(IProgress<float> progress, CancellationToken ct)
        {
            progress.Report(1f);
            return Task.CompletedTask;
        }
    }

    private static void RegisterAll_SkipsNullAndCounts()
    {
        var orchestrator = new StartupOrchestrator();
        var steps = new List<IStartupStep> { new NoopStep(), null, new NoopStep() };
        int added = BootManifestRegistrar.RegisterAll(orchestrator, steps);
        Check(added == 2, "RegisterAll skips the null entry and counts only real steps");
    }

    private static void RegisterAll_NullArgsThrow()
    {
        bool threwOnOrchestrator = false;
        try { BootManifestRegistrar.RegisterAll(null, new List<IStartupStep>()); }
        catch (ArgumentNullException) { threwOnOrchestrator = true; }
        Check(threwOnOrchestrator, "RegisterAll throws on null orchestrator");

        bool threwOnSteps = false;
        try { BootManifestRegistrar.RegisterAll(new StartupOrchestrator(), null); }
        catch (ArgumentNullException) { threwOnSteps = true; }
        Check(threwOnSteps, "RegisterAll throws on null step sequence");
    }

    private static void RegisteredSteps_ActuallyRun()
    {
        var orchestrator = new StartupOrchestrator();
        BootManifestRegistrar.RegisterAll(orchestrator, new List<IStartupStep> { new NoopStep(), new NoopStep() });

        float finalOverall = 0f;
        var progress = new Progress01<StartupAggregate>(agg => finalOverall = agg.Overall01);
        orchestrator.RunAsync(progress, CancellationToken.None).GetAwaiter().GetResult();
        Check(finalOverall >= 1f, "steps added via the registrar are actually run to completion");
    }

    private static void Manifest_EnabledSteps_FiltersOffAndEmpty()
    {
        var manifest = ScriptableObject.CreateInstance<BootStepManifest>();
        var on = new NoopStep();
        manifest.SetStepsForTest(new List<BootStepEntry>
        {
            new BootStepEntry(on, enabled: true),
            new BootStepEntry(new NoopStep(), enabled: false), // turned off -> excluded
            new BootStepEntry(null, enabled: true),            // empty row  -> excluded
        });

        var enabled = new List<IStartupStep>(manifest.EnabledSteps());
        Check(enabled.Count == 1 && ReferenceEquals(enabled[0], on),
            "EnabledSteps yields only turned-on, non-empty entries");

        var orchestrator = new StartupOrchestrator();
        int added = manifest.RegisterInto(orchestrator);
        Check(added == 1, "RegisterInto registers exactly the enabled steps");
    }

    public static int Main()
    {
        RegisterAll_SkipsNullAndCounts();
        RegisterAll_NullArgsThrow();
        RegisteredSteps_ActuallyRun();
        Manifest_EnabledSteps_FiltersOffAndEmpty();

        Console.WriteLine($"passed={_passed} failed={_failed}");
        return _failed == 0 ? 0 : 1;
    }

    // Minimal IProgress adapter (Progress<T> is fine too, but keeps the test single-threaded/simple).
    private sealed class Progress01<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public Progress01(Action<T> handler) { _handler = handler; }
        public void Report(T value) => _handler(value);
    }
}

// ---- Minimal UnityEngine stand-in: ONLY the symbols the runtime touches ------------------------
namespace UnityEngine
{
    internal static class Debug
    {
        public static void LogError(object message) => Console.WriteLine("[Debug.LogError] " + message);
    }

    internal static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Max(float a, float b) => a > b ? a : b;
    }

    // ScriptableObject stand-in with a test hook to seed the private step list without a real asset.
    public class ScriptableObject
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class SerializeReference : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }
}
#endif
