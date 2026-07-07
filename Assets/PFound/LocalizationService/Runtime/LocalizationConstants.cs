namespace PFound.LocalizationService
{
    /// <summary>
    /// Behavioral constants shared by the resolver, the INI table format, and the editor converter.
    /// These are the fixed data-format conventions (delimiters, section headers, file naming) plus the
    /// default/fallback language. Kept in one place so the core, the Unity IO layer, and the editor
    /// importer agree on the on-disk shape without coupling.
    /// </summary>
    public static class LocalizationConstants
    {
        /// <summary>Fallback and default language when nothing else resolves.</summary>
        public const string DefaultLanguageCode = "en-US";

        // --- delimiters ---
        /// <summary>Separates one parameter spec from the next inside a definition cell.</summary>
        public const char ParameterListDelimiter = ';';
        /// <summary>Separates the fields of a single parameter spec: type - name [ - format ].</summary>
        public const char ParameterFieldDelimiter = '-';

        // --- tag / dynamic-key brackets ---
        public const char TagOpen = '{';
        public const char TagClose = '}';
        /// <summary>A key is "dynamic" (a template) if it contains this marker.</summary>
        public const char DynamicKeyMarker = '{';

        // --- INI line-break escape (stored form vs runtime form) ---
        public const string StoredNewline = "\\";   // the two chars backslash-backslash on disk
        public const string RuntimeNewline = "\n";

        // --- INI section headers ---
        public const string SectionParameterDefinitions = "ParameterDefinitions";
        public const string SectionDynamicKeyDefinitions = "DynamicKeyDefinitions";
        public const string SectionLanguageDefinitions = "LanguageDefinitions";
        public const string SectionRedirectionDefinitions = "RedirectionDefinitions";
        public const string SectionEnd = "End";

        // --- file naming ---
        public const string ContentFilePrefix = "LocalizationText";
        public const string DefinitionsFilePrefix = "LocalizationDefinitions";
        public const char FileNameDelimiter = '-';
        public const string PlainExtension = ".txt";
        public const string CompressedExtension = ".lzma";
        public const string TablesFolderName = "Localizables";

        // --- CSV header columns ---
        public const string CsvKeyColumn = "Key";
        public const string CsvParameterColumn = "ParameterDefinition";

        /// <summary>Content value written for an auto-generated enum key that has no translation yet.</summary>
        public const string MissingEnumText = "No_Text";

        /// <summary>Localization key whose value is the abbreviation for "hour" (used by hourly currency).</summary>
        public const string HourAbbreviationKey = "unit_hour_abbreviation";
    }
}
