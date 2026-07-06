using System.Collections.Generic;
using System.Threading.Tasks;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Volatile store that remembers completion for the lifetime of the instance only. Useful in tests
    /// and for tutorials that should replay on every fresh app object graph.
    /// </summary>
    public sealed class InMemoryCompletionStore : ITutorialCompletionStore
    {
        private readonly HashSet<int> _done = new HashSet<int>();

        public Task HydrateAsync() => Task.CompletedTask;

        public bool IsCompleted(TutorialId id) => _done.Contains(id.handle);

        public void MarkCompleted(TutorialId id) => _done.Add(id.handle);

        public void Clear(TutorialId id) => _done.Remove(id.handle);
    }

    /// <summary>
    /// Session-scoped store: completion sticks until <see cref="ResetSession"/> is called (a new "session"),
    /// after which everything is forgotten. Distinct from <see cref="InMemoryCompletionStore"/> in that a
    /// session boundary is an explicit, testable event rather than object lifetime.
    /// </summary>
    public sealed class SessionCompletionStore : ITutorialCompletionStore
    {
        private readonly HashSet<int> _done = new HashSet<int>();
        private int _session;

        /// <summary>Which session the currently-remembered completions belong to.</summary>
        public int SessionToken => _session;

        public Task HydrateAsync() => Task.CompletedTask;

        public bool IsCompleted(TutorialId id) => _done.Contains(id.handle);

        public void MarkCompleted(TutorialId id) => _done.Add(id.handle);

        public void Clear(TutorialId id) => _done.Remove(id.handle);

        /// <summary>Begin a fresh session, discarding everything recorded in the previous one.</summary>
        public void ResetSession()
        {
            _done.Clear();
            _session++;
        }
    }
}
