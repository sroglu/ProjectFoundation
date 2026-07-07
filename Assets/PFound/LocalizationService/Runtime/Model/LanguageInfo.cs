namespace PFound.LocalizationService
{
    /// <summary>A supported language: its <see cref="LanguageKey"/> code plus a human-readable name.</summary>
    public readonly struct LanguageInfo
    {
        public readonly LanguageKey Language;
        public readonly string DisplayName;

        public LanguageInfo(LanguageKey language, string displayName)
        {
            Language = language;
            DisplayName = displayName;
        }

        public LanguageInfo(string code, string displayName)
            : this(new LanguageKey(code), displayName) { }

        public override string ToString() => DisplayName + " (" + Language + ")";
    }
}
