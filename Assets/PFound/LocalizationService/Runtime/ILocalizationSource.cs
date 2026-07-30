using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Supplies localized strings per language. The core service is format/IO-agnostic; concrete
    /// sources (INI+LZMA file, CDN download, in-memory) live in the IO/Unity layer and plug in here.
    /// </summary>
    public interface ILocalizationSource
    {
        IReadOnlyList<LanguageKey> AvailableLanguages { get; }

        /// <summary>Returns the key→text table for <paramref name="language"/>.</summary>
        IReadOnlyDictionary<string, string> Load(LanguageKey language);
    }
}
