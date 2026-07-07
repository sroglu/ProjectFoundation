using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Parses / serializes the content file: one INI section per language (<c>[language-code]</c>) whose
    /// entries are <c>key=value</c> lines. Produces an <see cref="InMemoryLocalizationSource"/> ready to
    /// feed the resolver. Engine-free: the Unity layer only supplies the file bytes.
    /// </summary>
    public static class ContentTables
    {
        public static InMemoryLocalizationSource FromIni(IniDocument doc)
        {
            var source = new InMemoryLocalizationSource();
            if (doc == null) return source;

            foreach (var sectionName in doc.SectionNames)
            {
                if (!doc.TryGetSection(sectionName, out var map)) continue;
                var table = new Dictionary<string, string>(map.Count);
                foreach (var pair in map) table[pair.Key] = pair.Value;
                source.Add(new LanguageKey(sectionName), table);
            }
            return source;
        }

        public static InMemoryLocalizationSource FromText(string content)
            => FromIni(IniDocument.Parse(content));

        public static IniDocument ToIni(IEnumerable<KeyValuePair<LanguageKey, IReadOnlyDictionary<string, string>>> tables)
        {
            var doc = new IniDocument();
            foreach (var lang in tables)
            {
                var section = doc.Section(lang.Key.Code);
                foreach (var entry in lang.Value) section[entry.Key] = entry.Value;
            }
            return doc;
        }
    }
}
