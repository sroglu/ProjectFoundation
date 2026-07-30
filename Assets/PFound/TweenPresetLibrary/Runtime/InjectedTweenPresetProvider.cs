using System;
using System.Collections.Generic;

namespace PFound.TweenPresetLibrary
{
    /// <summary>
    /// An in-memory <see cref="ITweenPresetProvider"/> over an injected set of presets — the non-Resources resolve
    /// path. Keys default to each preset's asset name; pass a key selector to key by something else. Null presets and
    /// null/empty keys are skipped; a later duplicate key overwrites an earlier one (last wins).
    /// </summary>
    public sealed class InjectedTweenPresetProvider : ITweenPresetProvider
    {
        private readonly Dictionary<string, TweenPreset> _byKey = new Dictionary<string, TweenPreset>(StringComparer.Ordinal);

        public InjectedTweenPresetProvider(IEnumerable<TweenPreset> presets, Func<TweenPreset, string> keySelector = null)
        {
            if (presets == null) throw new ArgumentNullException(nameof(presets));
            if (keySelector == null) keySelector = DefaultKey;

            foreach (var preset in presets)
            {
                if (preset == null) continue;
                string key = keySelector(preset);
                if (string.IsNullOrEmpty(key)) continue;
                _byKey[key] = preset;
            }
        }

        public bool TryGet(string key, out TweenPreset preset)
        {
            if (string.IsNullOrEmpty(key)) { preset = null; return false; }
            return _byKey.TryGetValue(key, out preset);
        }

        public bool Contains(string key) => !string.IsNullOrEmpty(key) && _byKey.ContainsKey(key);

        public IReadOnlyCollection<string> Keys => _byKey.Keys;

        private static string DefaultKey(TweenPreset preset) => preset.name;
    }
}
