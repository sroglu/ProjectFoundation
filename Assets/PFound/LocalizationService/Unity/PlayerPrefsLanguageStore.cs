using UnityEngine;

namespace PFound.LocalizationService.Unity
{
    /// <summary>PlayerPrefs-backed language preference store.</summary>
    public sealed class PlayerPrefsLanguageStore : ILanguagePreferenceStore
    {
        public const string DefaultKey = "PFound.Localization.Language";

        private readonly string _prefsKey;

        public PlayerPrefsLanguageStore(string prefsKey = DefaultKey) { _prefsKey = prefsKey; }

        public bool TryGet(out string code)
        {
            code = PlayerPrefs.GetString(_prefsKey, string.Empty);
            return !string.IsNullOrEmpty(code);
        }

        public void Set(string code)
        {
            PlayerPrefs.SetString(_prefsKey, code ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(_prefsKey);
            PlayerPrefs.Save();
        }
    }
}
