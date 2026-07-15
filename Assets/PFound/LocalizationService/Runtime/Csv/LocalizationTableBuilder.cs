using System;
using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// Turns a localization spreadsheet (RFC-4180 CSV) into the definitions + content table texts. The
    /// header is <c>Key, ParameterDefinition, &lt;lang1&gt;, &lt;lang2&gt;, …</c>. Each data row routes by
    /// key: keys containing the dynamic marker go to the dynamic-key section, the rest to the parameter
    /// section. Empty translation cells are skipped (they fall back at runtime). Values from
    /// <c>[LocalizableEnum]</c> types are appended as parameterless keys with a placeholder in every
    /// language when not already present. Engine-free so the routing is mono-testable; the editor wrapper
    /// only adds file writing + compression.
    /// </summary>
    public static class LocalizationTableBuilder
    {
        public struct Result
        {
            public string DefinitionsText;
            public string ContentText;
            public List<LanguageKey> Languages;
        }

        public static Result Build(string csvText, IEnumerable<Type> localizableEnums = null)
        {
            var rows = CsvReader.Parse(csvText);
            if (rows.Count == 0)
                throw new FormatException("Localization CSV is empty.");

            var header = rows[0];
            if (header.Count < 2 ||
                !string.Equals(header[0].Trim(), LocalizationConstants.CsvKeyColumn, StringComparison.Ordinal) ||
                !string.Equals(header[1].Trim(), LocalizationConstants.CsvParameterColumn, StringComparison.Ordinal))
            {
                throw new FormatException("Localization CSV header must start with '" +
                    LocalizationConstants.CsvKeyColumn + "," + LocalizationConstants.CsvParameterColumn + "'.");
            }

            int langCount = header.Count - 2;
            var languages = new List<LanguageKey>(langCount);
            for (int l = 0; l < langCount; l++)
                languages.Add(new LanguageKey(header[2 + l].Trim()));

            var definitions = new LocalizationDefinitions();
            foreach (var lang in languages)
                definitions.Languages.Add(new LanguageInfo(lang, lang.Code));

            // Per-language ordered content maps.
            var content = new Dictionary<LanguageKey, Dictionary<string, string>>();
            foreach (var lang in languages) content[lang] = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0) continue;
                string key = row[0].Trim();
                if (key.Length == 0) continue;

                string paramCell = row.Count > 1 ? row[1] : string.Empty;
                var def = ParameterDefinition.Parse(paramCell);

                if (key.IndexOf(LocalizationConstants.DynamicKeyMarker) >= 0)
                    definitions.DynamicKeys[key] = def;                 // template registry: keep even when empty
                else if (def.Count > 0)
                    definitions.Parameters[key] = def;                  // only store non-trivial param lists

                for (int l = 0; l < langCount; l++)
                {
                    int col = 2 + l;
                    if (col >= row.Count) continue;
                    string text = row[col];
                    if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) continue; // skip empty -> fallback
                    content[languages[l]][key] = text;
                }
            }

            AppendEnumKeys(localizableEnums, languages, content);

            return new Result
            {
                DefinitionsText = definitions.ToIni().Write(),
                ContentText = BuildContentIni(languages, content).Write(),
                Languages = languages
            };
        }

        private static void AppendEnumKeys(IEnumerable<Type> enums, List<LanguageKey> languages,
            Dictionary<LanguageKey, Dictionary<string, string>> content)
        {
            if (enums == null) return;
            foreach (var enumType in enums)
            {
                if (enumType == null || !enumType.IsEnum) continue;
                foreach (var enumKey in EnumKeyGenerator.KeysFor(enumType))
                {
                    foreach (var lang in languages)
                    {
                        var table = content[lang];
                        if (!table.ContainsKey(enumKey))
                            table[enumKey] = LocalizationConstants.MissingEnumText;
                    }
                }
            }
        }

        private static IniDocument BuildContentIni(List<LanguageKey> languages,
            Dictionary<LanguageKey, Dictionary<string, string>> content)
        {
            var doc = new IniDocument();
            foreach (var lang in languages)
            {
                var section = doc.Section(lang.Code);
                foreach (var pair in content[lang]) section[pair.Key] = pair.Value;
            }
            return doc;
        }
    }
}
