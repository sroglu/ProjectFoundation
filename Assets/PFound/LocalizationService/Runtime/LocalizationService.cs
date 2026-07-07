using System;
using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Resolves localization keys to text for the active language, with fallback to a base language
    /// and optional parameter formatting. Language tables come from a pluggable
    /// <see cref="ILocalizationSource"/> (INI/LZMA/CDN sources live in the IO layer); loaded tables
    /// are cached. Engine-independent and synchronous. Main-thread only.
    /// </summary>
    public sealed class LocalizationService
    {
        private readonly ILocalizationSource _source;
        private readonly Dictionary<LanguageKey, IReadOnlyDictionary<string, string>> _cache =
            new Dictionary<LanguageKey, IReadOnlyDictionary<string, string>>();

        private readonly LanguageKey _fallback;
        private readonly IReadOnlyDictionary<string, string> _fallbackTable;
        private IReadOnlyDictionary<string, string> _activeTable;

        public LanguageKey ActiveLanguage { get; private set; }

        public LocalizationService(ILocalizationSource source, LanguageKey fallbackLanguage)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if (!IsLanguageSupported(fallbackLanguage))
                throw new ArgumentException("Fallback language not available: " + fallbackLanguage, nameof(fallbackLanguage));

            _fallback = fallbackLanguage;
            _fallbackTable = GetTable(fallbackLanguage);
            ActiveLanguage = fallbackLanguage;
            _activeTable = _fallbackTable;
        }

        public IReadOnlyList<LanguageKey> SupportedLanguages => _source.AvailableLanguages;

        public bool IsLanguageSupported(LanguageKey language)
        {
            var langs = _source.AvailableLanguages;
            for (int i = 0; i < langs.Count; i++) if (langs[i].Equals(language)) return true;
            return false;
        }

        /// <summary>Switches the active language. Throws if it is not supported.</summary>
        public void SwitchLanguage(LanguageKey language)
        {
            if (!IsLanguageSupported(language))
                throw new ArgumentException("Language not available: " + language, nameof(language));
            ActiveLanguage = language;
            _activeTable = GetTable(language);
        }

        /// <summary>
        /// Localized text for <paramref name="key"/>: active language, then fallback, then the raw
        /// key string if missing. Formats with <paramref name="args"/> when supplied.
        /// </summary>
        public string Get(LocalizationKey key, params object[] args)
        {
            if (!TryGetUnprocessed(key, out var text)) return key.Value;
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        /// <summary>Raw (unformatted) text for <paramref name="key"/> from active or fallback language.</summary>
        public bool TryGetUnprocessed(LocalizationKey key, out string text)
            => TryGetUnprocessed(key, out text, out _);

        /// <summary>
        /// Raw (unformatted) text for <paramref name="key"/>, also reporting whether the value came from
        /// the fallback language rather than the active one.
        /// </summary>
        public bool TryGetUnprocessed(LocalizationKey key, out string text, out bool fromFallback)
        {
            fromFallback = false;
            if (key.IsValid)
            {
                if (_activeTable.TryGetValue(key.Value, out text)) return true;
                if (!ReferenceEquals(_activeTable, _fallbackTable) && _fallbackTable.TryGetValue(key.Value, out text))
                {
                    fromFallback = true;
                    return true;
                }
            }
            text = null;
            return false;
        }

        private IReadOnlyDictionary<string, string> GetTable(LanguageKey language)
        {
            if (!_cache.TryGetValue(language, out var table))
            {
                table = _source.Load(language) ?? throw new InvalidOperationException("Source returned no table for " + language);
                _cache[language] = table;
            }
            return table;
        }
    }
}
