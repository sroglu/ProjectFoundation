using System;
using System.Collections.Generic;

namespace PFound.LocalizationService
{
    /// <summary>
    /// The non-content metadata of a localization table set: per-key parameter definitions, dynamic-key
    /// (template) definitions, the supported languages, and the redirection rules. Read from / written to
    /// the four-section definitions file. Content text lives separately (per-language sections) and is
    /// served through <see cref="ILocalizationSource"/>.
    /// </summary>
    public sealed class LocalizationDefinitions
    {
        public readonly Dictionary<string, ParameterDefinition> Parameters =
            new Dictionary<string, ParameterDefinition>(StringComparer.Ordinal);

        public readonly Dictionary<string, ParameterDefinition> DynamicKeys =
            new Dictionary<string, ParameterDefinition>(StringComparer.Ordinal);

        public readonly List<LanguageInfo> Languages = new List<LanguageInfo>();

        public LanguageRedirections Redirections = LanguageRedirections.CreateDefault();

        public ParameterDefinition ParametersFor(string key)
            => Parameters.TryGetValue(key, out var def) ? def : ParameterDefinition.Empty;

        public bool TryGetDynamicKey(string templateKey, out ParameterDefinition def)
            => DynamicKeys.TryGetValue(templateKey, out def);

        /// <summary>Reads the four-section definitions document.</summary>
        public static LocalizationDefinitions FromIni(IniDocument doc)
        {
            var defs = new LocalizationDefinitions();
            if (doc == null) return defs;

            if (doc.TryGetSection(LocalizationConstants.SectionParameterDefinitions, out var pd))
                foreach (var e in pd) defs.Parameters[e.Key] = ParameterDefinition.Parse(e.Value);

            if (doc.TryGetSection(LocalizationConstants.SectionDynamicKeyDefinitions, out var dk))
                foreach (var e in dk) defs.DynamicKeys[e.Key] = ParameterDefinition.Parse(e.Value);

            if (doc.TryGetSection(LocalizationConstants.SectionLanguageDefinitions, out var ld))
                foreach (var e in ld) defs.Languages.Add(new LanguageInfo(e.Key, e.Value));

            if (doc.TryGetSection(LocalizationConstants.SectionRedirectionDefinitions, out var rd))
            {
                defs.Redirections = LanguageRedirections.CreateDefault();
                foreach (var e in rd) defs.Redirections.Add(e.Key, e.Value);
            }

            return defs;
        }

        public IniDocument ToIni()
        {
            var doc = new IniDocument();
            var pd = doc.Section(LocalizationConstants.SectionParameterDefinitions);
            foreach (var kv in Parameters) pd[kv.Key] = kv.Value.Serialize();

            var dk = doc.Section(LocalizationConstants.SectionDynamicKeyDefinitions);
            foreach (var kv in DynamicKeys) dk[kv.Key] = kv.Value.Serialize();

            var ld = doc.Section(LocalizationConstants.SectionLanguageDefinitions);
            foreach (var lang in Languages) ld[lang.Language.Code] = lang.DisplayName;

            doc.Section(LocalizationConstants.SectionEnd); // terminator section
            return doc;
        }

        public IReadOnlyList<LanguageKey> LanguageKeys()
        {
            var list = new List<LanguageKey>(Languages.Count);
            foreach (var l in Languages) list.Add(l.Language);
            return list;
        }
    }
}
