using System.Collections.Generic;
using UnityEngine;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// A designer-authored boot set: an asset a designer fills with the steps that should run at app
    /// start. Each entry can be turned on or off, so you can keep a step in the list but skip it for a
    /// build without deleting it.
    ///
    /// The manifest is the SET of steps to run, not an ordered plan — the orchestrator runs every
    /// enabled step in parallel and the list order does not decide the run order (see
    /// <see cref="StartupOrchestrator"/>). Feed it to a <see cref="StartupOrchestrator"/> with
    /// <see cref="BootManifestRegistrar.RegisterAll"/> (or the <see cref="RegisterInto"/> shortcut)
    /// before you call <c>RunAsync</c>.
    ///
    /// Steps are stored with <see cref="SerializeReference"/> so a designer can pick a concrete
    /// <see cref="IStartupStep"/> type in the inspector and set its fields inline — one asset holds the
    /// whole boot set instead of one asset per step.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BootStepManifest",
        menuName = "PFound/Startup Orchestration/Boot Step Manifest",
        order = 0)]
    public sealed class BootStepManifest : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The steps to run at boot. Turn a row off to keep it but skip it. Order does not " +
                 "decide run order — every enabled step runs in parallel.")]
        private List<BootStepEntry> _steps = new();

        /// <summary>The authored entries, including the ones turned off.</summary>
        public IReadOnlyList<BootStepEntry> Steps => _steps;

        /// <summary>
        /// Yields the concrete <see cref="IStartupStep"/> of every entry that is turned on. Turned-off
        /// or empty entries are left out.
        /// </summary>
        public IEnumerable<IStartupStep> EnabledSteps()
        {
            foreach (var entry in _steps)
            {
                if (entry.Enabled && entry.Step != null)
                    yield return entry.Step;
            }
        }

        /// <summary>
        /// Registers this manifest's enabled steps into <paramref name="orchestrator"/> and returns how
        /// many were added. Call this before <c>RunAsync</c>.
        /// </summary>
        public int RegisterInto(StartupOrchestrator orchestrator)
            => BootManifestRegistrar.RegisterAll(orchestrator, EnabledSteps());

#if PF_STARTUP_TESTS
        // Seeds the authored entries directly, so the standalone mono/csc runner can build a manifest
        // without a real .asset / inspector. Compiled out of normal (Unity) builds.
        internal void SetStepsForTest(List<BootStepEntry> steps) => _steps = steps;
#endif
    }
}
