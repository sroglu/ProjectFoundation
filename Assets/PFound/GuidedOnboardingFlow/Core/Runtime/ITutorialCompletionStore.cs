using System.Threading.Tasks;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Persistence seam recording which tutorials a user has finished. The manager consults it to skip
    /// already-completed tutorials and stamps completion when a run ends normally. Adapters back it with
    /// device prefs, plain memory, or a single session — the core only depends on this interface.
    /// </summary>
    public interface ITutorialCompletionStore
    {
        /// <summary>Load persisted state before the first eligibility check. May be a no-op for volatile stores.</summary>
        Task HydrateAsync();

        bool IsCompleted(TutorialId id);

        void MarkCompleted(TutorialId id);

        /// <summary>Forget one tutorial's completion (e.g. a debug "replay tutorial" action).</summary>
        void Clear(TutorialId id);
    }
}
