using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PFound.GuidedOnboardingFlow.Core;
using PFound.UserPrefs;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// Persists tutorial completion to PFound UserPrefs. Because UserPrefs is schema-keyed (one typed
    /// <see cref="PrefKey{T}"/> per value) and tutorial ids are open-ended, the whole completed set is
    /// stored under a single string key as a comma-separated list of handles, hydrated into an in-memory
    /// set on startup and written back on each change. This is the persistent (once-account) tier — wrap
    /// it in a <see cref="CompositeCompletionStore"/> to also get in-memory once-session behavior.
    /// </summary>
    public sealed class UserPrefsCompletionStore : ITutorialCompletionStore
    {
        /// <summary>The single key this adapter owns; register it on the store's builder before use.</summary>
        public static readonly PrefKey<string> CompletedKey = new PrefKey<string>("gof.completed", string.Empty);

        private readonly IPrefsStore _store;
        private readonly PrefKey<string> _key;
        private readonly HashSet<int> _done = new HashSet<int>();

        public UserPrefsCompletionStore(IPrefsStore store, PrefKey<string> key)
        {
            _store = store;
            _key = key;
        }

        public UserPrefsCompletionStore(IPrefsStore store) : this(store, CompletedKey)
        {
        }

        public Task HydrateAsync(CancellationToken ct = default)
        {
            _done.Clear();
            string packed = _store.Get(_key) ?? string.Empty;
            foreach (string token in packed.Split(','))
            {
                if (int.TryParse(token, out int handle))
                    _done.Add(handle);
            }
            return Task.CompletedTask;
        }

        public bool IsCompleted(TutorialId id) => _done.Contains(id.handle);

        public void MarkCompleted(TutorialId id, ReplayPolicy mode)
        {
            // Only account-scoped completions belong in persistent storage; session/repeatable are handled
            // by the composite's in-memory tier, so this leaf never persists them.
            if (mode != ReplayPolicy.OnceAccount)
                return;
            if (_done.Add(id.handle))
                Persist();
        }

        public void Clear(TutorialId id)
        {
            if (_done.Remove(id.handle))
                Persist();
        }

        public IReadOnlyCollection<TutorialId> GetAllCompleted()
        {
            var result = new List<TutorialId>(_done.Count);
            foreach (int handle in _done)
                result.Add(new TutorialId(handle));
            return result;
        }

        private void Persist()
        {
            _store.Set(_key, string.Join(",", _done));
        }
    }
}
