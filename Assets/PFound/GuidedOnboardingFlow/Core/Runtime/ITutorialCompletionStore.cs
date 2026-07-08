using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Persistence seam recording which tutorials a user has finished. The manager consults it to skip
    /// already-completed tutorials and stamps completion when a run ends. Adapters back it with device
    /// prefs, plain memory, a single session, or a composite of those — the core only depends on this
    /// interface. Completion is recorded under a <see cref="ReplayPolicy"/> so the same store can hold a
    /// mix of once-ever, once-per-session, and never-remembered tutorials.
    /// </summary>
    public interface ITutorialCompletionStore
    {
        /// <summary>
        /// Load persisted state before the first eligibility check. A remote/server-backed adapter fetches
        /// its completion set here; local backends complete synchronously. The token lets a slow fetch be
        /// cancelled on teardown. Callers MUST await this before the manager begins ticking, or a slow
        /// backend would briefly mis-report finished tutorials as not-done and replay them.
        /// </summary>
        Task HydrateAsync(CancellationToken ct = default);

        bool IsCompleted(TutorialId id);

        /// <summary>
        /// Record the tutorial as completed under the given policy. <see cref="ReplayPolicy.Repeatable"/>
        /// is a no-op (nothing is remembered); <see cref="ReplayPolicy.OnceSession"/> is forgotten on the
        /// next session; <see cref="ReplayPolicy.OnceAccount"/> persists.
        /// </summary>
        void MarkCompleted(TutorialId id, ReplayPolicy mode);

        /// <summary>Forget one tutorial's completion (e.g. a debug "replay tutorial" action).</summary>
        void Clear(TutorialId id);

        /// <summary>Every id currently recorded as completed (across whatever tiers the store spans).</summary>
        IReadOnlyCollection<TutorialId> GetAllCompleted();
    }
}
