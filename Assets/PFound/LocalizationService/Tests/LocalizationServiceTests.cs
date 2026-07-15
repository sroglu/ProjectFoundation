using System;
using System.Collections.Generic;
using PFound.LocalizationService;

// Standalone mono/csc runner (pure C#) — parity oracle for the localization core.
internal static class LocalizationServiceTests
{
    private static int s_passed, s_failed;
    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    private sealed class MemorySource : ILocalizationSource
    {
        private readonly Dictionary<LanguageKey, IReadOnlyDictionary<string, string>> _data;
        public MemorySource(Dictionary<LanguageKey, IReadOnlyDictionary<string, string>> data) { _data = data; }
        public IReadOnlyList<LanguageKey> AvailableLanguages
        {
            get { var l = new List<LanguageKey>(_data.Keys); return l; }
        }
        public IReadOnlyDictionary<string, string> Load(LanguageKey language) => _data[language];
    }

    public static int Main()
    {
        var en = new LanguageKey("en");
        var tr = new LanguageKey("tr");
        var source = new MemorySource(new Dictionary<LanguageKey, IReadOnlyDictionary<string, string>>
        {
            [en] = new Dictionary<string, string> { ["hello"] = "Hello", ["greet"] = "Hi {0}", ["only_en"] = "OnlyEN" },
            [tr] = new Dictionary<string, string> { ["hello"] = "Merhaba", ["greet"] = "Selam {0}" }, // no "only_en"
        });

        var loc = new LocalizationService(source, fallbackLanguage: en);

        Check(loc.ActiveLanguage.Equals(en), "active language defaults to fallback");
        Check(loc.Get("hello") == "Hello", "Get returns active-language text");

        loc.SwitchLanguage(tr);
        Check(loc.ActiveLanguage.Equals(tr) && loc.Get("hello") == "Merhaba", "SwitchLanguage changes resolution");
        Check(loc.Get("greet", "Ali") == "Selam Ali", "Get formats with args");
        Check(loc.Get("only_en") == "OnlyEN", "missing key falls back to fallback language");
        Check(loc.Get("does_not_exist") == "does_not_exist", "missing everywhere returns the raw key");

        Check(loc.IsLanguageSupported(tr) && !loc.IsLanguageSupported(new LanguageKey("de")), "IsLanguageSupported");
        Check(loc.SupportedLanguages.Count == 2, "SupportedLanguages lists source languages");

        Check(loc.TryGetUnprocessed("greet", out var raw) && raw == "Selam {0}", "TryGetUnprocessed returns unformatted text");

        bool threw = false;
        try { loc.SwitchLanguage(new LanguageKey("de")); } catch (ArgumentException) { threw = true; }
        Check(threw, "SwitchLanguage throws for unsupported language");

        // Extended v2 featureset coverage (shares this harness via the Check delegate).
        LocalizationV2Tests.Run(Check);

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.LocalizationService: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }
}
