using System;
using System.Collections.Generic;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// Reads a designer-authored boot set and registers its steps into a
    /// <see cref="StartupOrchestrator"/> before you call <c>RunAsync</c>.
    ///
    /// This is the engine-free core of the manifest feature: it takes any sequence of
    /// <see cref="IStartupStep"/> (from a ScriptableObject <see cref="BootStepManifest"/>, a hard-coded
    /// list, or a test) and adds each one to the orchestrator. It intentionally knows nothing about
    /// Unity or serialization, so it is testable with plain C#.
    ///
    /// The manifest is the SET of steps to run, not an ordered plan: the orchestrator runs every
    /// registered step in parallel, so the list order here only affects which step the orchestrator
    /// happens to hold first, not the run order. Ordered, dependency-aware initialization is a
    /// separate concern handled by the DI container, not by this boot runner.
    /// </summary>
    public static class BootManifestRegistrar
    {
        /// <summary>
        /// Registers every step in <paramref name="steps"/> into <paramref name="orchestrator"/>.
        /// Skips any <c>null</c> entry (a manifest row a designer left empty) so one blank row does
        /// not stop the whole boot set from loading. Returns how many steps were actually added.
        /// </summary>
        public static int RegisterAll(StartupOrchestrator orchestrator, IEnumerable<IStartupStep> steps)
        {
            if (orchestrator == null) throw new ArgumentNullException(nameof(orchestrator));
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            int added = 0;
            foreach (var step in steps)
            {
                // An empty inspector slot is a real, expected external gap in author-supplied data,
                // not a lifecycle bug in our own state — skip it rather than crash the boot.
                if (step == null) continue;
                orchestrator.Register(step);
                added++;
            }
            return added;
        }
    }
}
