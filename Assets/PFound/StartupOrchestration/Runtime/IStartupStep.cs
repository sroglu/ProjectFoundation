using System;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.StartupOrchestration
{
    /// <summary>
    /// A self-contained unit of parallel boot preparation. The orchestrator runs every registered
    /// step concurrently; order is undefined.
    ///
    /// Contract:
    /// <list type="bullet">
    /// <item>Implementations MUST be dependency-free — no DI lookups, no assumptions about other
    /// services or other steps having run. (Work that needs a built service belongs in that
    /// service's own initialization, not here.)</item>
    /// <item>Implementations MUST NOT throw on user-facing failure (offline, fetch failure): report
    /// <c>progress.Report(1f)</c> and complete normally; the owner applies its own fallback.</item>
    /// <item>A thrown exception IS caught by the orchestrator (logged, counted as completed) so boot
    /// cannot hang — but that is a safety net, not the intended failure path.</item>
    /// <item>No deadline. A step that needs a time limit enforces its own and reports success on
    /// expiry.</item>
    /// </list>
    ///
    /// Returns a plain <see cref="Task"/> so this contract carries no third-party async dependency;
    /// UniTask-native owners adapt at the boundary (e.g. <c>.AsTask()</c>).
    /// </summary>
    public interface IStartupStep
    {
        /// <summary>The wait reason this step reports under. Drives the LoadingScreen status text.</summary>
        WaitReason Reason { get; }

        /// <summary>
        /// Relative share this step contributes to the overall progress bar (weighted mean across all
        /// steps). Default 1 = equal weight. Override so a long step (e.g. a big asset download)
        /// dominates the bar and a quick step (catalog ping) contributes little — the bar then tracks
        /// real elapsed work instead of step count. Weight 0 lets a step participate in the run (the
        /// orchestrator still awaits it) without moving the bar.
        /// </summary>
        float Weight => 1f;

        /// <summary>
        /// Stateless, dependency-free async preparation. Report 0..1 for THIS step only.
        /// </summary>
        Task ExecuteAsync(IProgress<float> progress, CancellationToken ct);
    }
}
