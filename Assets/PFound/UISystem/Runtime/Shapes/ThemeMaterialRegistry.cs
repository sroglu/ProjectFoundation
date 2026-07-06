using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Designer-authored mapping from <c>theme-key</c> strings to <see cref="Material"/> assets,
    /// stored as a <see cref="ScriptableObject"/>. One registry = one theme's material set
    /// (e.g. <c>M3Default</c>, <c>Papercut</c>, <c>Candypop</c>). At runtime, swap the active
    /// registry via <see cref="ActiveMaterialTheme.Active"/> and every <see cref="ThemedSdfPanel"/>
    /// rebinds automatically.
    /// </summary>
    /// <remarks>
    /// Designer workflow:
    /// <list type="number">
    ///   <item>Right-click in Project view → <b>Create → UISystem → Theme Material Registry</b>.</item>
    ///   <item>Set <see cref="ThemeName"/> + fill the entries list with <c>(key, material)</c> pairs.</item>
    ///   <item>The same key set is what every theme's registry must populate so panels can switch
    ///         themes without rewiring (e.g. all themes expose <c>card-elevated</c>, <c>button</c>, …).</item>
    /// </list>
    /// </remarks>
    [CreateAssetMenu(fileName = "ThemeMaterialRegistry", menuName = "UISystem/Theme Material Registry")]
    public sealed class ThemeMaterialRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Theme-key string used by ThemedSdfPanel.theme-key UxmlAttribute.")]
            public string Key;
            [Tooltip("Material returned when this key is requested via the active theme.")]
            public Material Material;
        }

        [Tooltip("Human-readable theme name (M3Default, Papercut, Candypop, …). Diagnostic only.")]
        public string ThemeName;

        [SerializeField, Tooltip("Designer-filled list of (key → material) entries.")]
        private List<Entry> _entries = new();

        private Dictionary<string, Material> _lookup;

        /// <summary>Resolves <paramref name="key"/> to the assigned material, or <c>null</c> if unmapped.</summary>
        public Material Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureLookup();
            _lookup.TryGetValue(key, out var m);
            return m;
        }

        /// <summary>Enumerates all keys defined in this registry (for diagnostics / Inspector tooling).</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                EnsureLookup();
                return _lookup.Keys;
            }
        }

        /// <summary>Number of (key, material) entries currently registered.</summary>
        public int Count
        {
            get
            {
                EnsureLookup();
                return _lookup.Count;
            }
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Material>(_entries?.Count ?? 0);
            if (_entries == null) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (string.IsNullOrEmpty(e.Key) || e.Material == null) continue;
                _lookup[e.Key] = e.Material;
            }
        }

        private void OnValidate()
        {
            // Editor-side: invalidate the lookup cache so re-Get reflects edits.
            _lookup = null;
        }

        private void OnEnable()
        {
            _lookup = null;
        }
    }
}
