using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// Volatile store that remembers completion for the lifetime of the instance only. Useful in tests
    /// and for tutorials that should replay on every fresh app object graph. Treats both
    /// <see cref="ReplayPolicy.OnceAccount"/> and <see cref="ReplayPolicy.OnceSession"/> the same (there
    /// is no persistence boundary here); <see cref="ReplayPolicy.Repeatable"/> is never recorded.
    /// </summary>
    public sealed class InMemoryCompletionStore : ITutorialCompletionStore
    {
        private readonly HashSet<int> _done = new HashSet<int>();

        public Task HydrateAsync(CancellationToken ct = default) => Task.CompletedTask;

        public bool IsCompleted(TutorialId id) => _done.Contains(id.handle);

        public void MarkCompleted(TutorialId id, ReplayPolicy mode)
        {
            if (mode == ReplayPolicy.Repeatable)
                return;
            _done.Add(id.handle);
        }

        public void Clear(TutorialId id) => _done.Remove(id.handle);

        public IReadOnlyCollection<TutorialId> GetAllCompleted()
        {
            var result = new List<TutorialId>(_done.Count);
            foreach (int handle in _done)
                result.Add(new TutorialId(handle));
            return result;
        }
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

        public Task HydrateAsync(CancellationToken ct = default) => Task.CompletedTask;

        public bool IsCompleted(TutorialId id) => _done.Contains(id.handle);

        public void MarkCompleted(TutorialId id, ReplayPolicy mode)
        {
            if (mode == ReplayPolicy.Repeatable)
                return;
            _done.Add(id.handle);
        }

        public void Clear(TutorialId id) => _done.Remove(id.handle);

        public IReadOnlyCollection<TutorialId> GetAllCompleted()
        {
            var result = new List<TutorialId>(_done.Count);
            foreach (int handle in _done)
                result.Add(new TutorialId(handle));
            return result;
        }

        /// <summary>Begin a fresh session, discarding everything recorded in the previous one.</summary>
        public void ResetSession()
        {
            _done.Clear();
            _session++;
        }
    }

    /// <summary>
    /// Routes each completion to the right tier by policy: <see cref="ReplayPolicy.OnceAccount"/> goes to
    /// a persistent inner store (survives restarts), <see cref="ReplayPolicy.OnceSession"/> stays in an
    /// in-memory set forgotten on restart, and <see cref="ReplayPolicy.Repeatable"/> is dropped entirely.
    /// Reads and <see cref="GetAllCompleted"/> union both tiers, so one manager can host a mix of
    /// once-ever and once-per-session tutorials against a single persistence backend.
    /// </summary>
    public sealed class CompositeCompletionStore : ITutorialCompletionStore
    {
        private readonly ITutorialCompletionStore _persistent;
        private readonly HashSet<int> _session = new HashSet<int>();

        public CompositeCompletionStore(ITutorialCompletionStore persistent)
        {
            _persistent = persistent;
        }

        // The persistent tier does its own hydration; the in-memory session tier has nothing to load.
        public Task HydrateAsync(CancellationToken ct = default) =>
            _persistent != null ? _persistent.HydrateAsync(ct) : Task.CompletedTask;

        public bool IsCompleted(TutorialId id) =>
            (_persistent != null && _persistent.IsCompleted(id)) || _session.Contains(id.handle);

        public void MarkCompleted(TutorialId id, ReplayPolicy mode)
        {
            switch (mode)
            {
                case ReplayPolicy.OnceAccount:
                    _persistent?.MarkCompleted(id, mode);
                    break;
                case ReplayPolicy.OnceSession:
                    _session.Add(id.handle);
                    break;
                case ReplayPolicy.Repeatable:
                    break; // remember nothing
            }
        }

        public void Clear(TutorialId id)
        {
            _persistent?.Clear(id);
            _session.Remove(id.handle);
        }

        public IReadOnlyCollection<TutorialId> GetAllCompleted()
        {
            var union = new HashSet<int>();
            if (_persistent != null)
            {
                foreach (TutorialId id in _persistent.GetAllCompleted())
                    union.Add(id.handle);
            }
            foreach (int handle in _session)
                union.Add(handle);

            var result = new List<TutorialId>(union.Count);
            foreach (int handle in union)
                result.Add(new TutorialId(handle));
            return result;
        }
    }
}
