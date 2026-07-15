using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// An <see cref="ILocalizationSource"/> backed entirely by in-memory tables. Handy for tests, for
    /// code-authored strings, and as the target of a parsed content file.
    /// </summary>
    public sealed class InMemoryLocalizationSource : ILocalizationSource
    {
        private readonly Dictionary<LanguageKey, IReadOnlyDictionary<string, string>> _tables =
            new Dictionary<LanguageKey, IReadOnlyDictionary<string, string>>();
        private readonly List<LanguageKey> _languages = new List<LanguageKey>();

        public IReadOnlyList<LanguageKey> AvailableLanguages => _languages;

        public InMemoryLocalizationSource() { }

        public InMemoryLocalizationSource Add(LanguageKey language, IReadOnlyDictionary<string, string> table)
        {
            if (!_tables.ContainsKey(language)) _languages.Add(language);
            _tables[language] = table;
            return this;
        }

        public IReadOnlyDictionary<string, string> Load(LanguageKey language) => _tables[language];
    }
}
