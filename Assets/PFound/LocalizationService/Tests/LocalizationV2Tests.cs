using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PFound.LocalizationService;

// Extended mono/csc coverage for the v2 featureset (parameters, tags, formatters, number abbrev, INI,
// selection, CSV, table builder, and the rich catalog). Driven by the shared runner in
// LocalizationServiceTests.Main via the passed-in Check delegate.
internal static class LocalizationV2Tests
{
    [LocalizableEnum]
    private enum Status { Idle, Active }

    [LocalizableEnum("clr")]
    private enum Color { Red, Green }

    private sealed class RecordingResolver : IValueKeyResolver
    {
        public bool TryResolveKey(ILocalizationValue value, out LocalizationKey key)
        {
            // Route any number 42 to a special key, to prove the hook beats formatting.
            if (value.TryGetDouble(out double d) && d == 42) { key = new LocalizationKey("answer"); return true; }
            key = default; return false;
        }
    }

    public static void Run(Action<bool, string> check)
    {
        ParameterParsing(check);
        TagScannerOutcomes(check);
        NumberAbbrev(check);
        Formatters(check);
        Ini(check);
        Redirections(check);
        Selection(check);
        Csv(check);
        TableBuilder(check);
        EnumKeys(check);
        Catalog(check);
        DirectorySource(check);
    }

    private static void DirectorySource(Action<bool, string> c)
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pf_loc_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            string csv =
                "Key,ParameterDefinition,en-US,tr-TR\n" +
                "hello,,Hello,Merhaba\n" +
                "count,int-n-NumberAbbreviated,You have {n},\n";
            var built = LocalizationTableBuilder.Build(csv);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(dir, LocalizationConstants.DefinitionsFilePrefix + LocalizationConstants.PlainExtension),
                built.DefinitionsText);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(dir, LocalizationConstants.ContentFilePrefix + LocalizationConstants.PlainExtension),
                built.ContentText);

            var src = new DirectoryLocalizationSource(dir);
            c(src.AvailableLanguages.Count == 2, "directory source: languages discovered");
            c(src.Load(new LanguageKey("tr-TR"))["hello"] == "Merhaba", "directory source: content loaded per language");
            c(src.Definitions.ParametersFor("count").Count == 1, "directory source: definitions loaded");

            var catalog = new LocalizationCatalog(src, new LanguageKey("en-US"), src.Definitions);
            catalog.SetCulture(CultureInfo.InvariantCulture);
            c(catalog.Get("count", new NumberValue(2500)) == "You have 2.5K", "directory source: end-to-end catalog get");
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }

    private static void ParameterParsing(Action<bool, string> c)
    {
        var def = ParameterDefinition.Parse("int-count-NumberAbbreviated;Vector-pos-Coordinates");
        c(def.Count == 2, "param def parses two specs");
        c(def[0].Name == "count" && def[0].TypeName == "int" && def[0].Format == LocalizationFormat.NumberAbbreviated, "param spec fields + format");
        c(def[1].Format == LocalizationFormat.Coordinates, "second spec format");
        c(def.IndexOf("pos") == 1 && def.IndexOf("nope") == -1, "param def index lookup (ordinal)");
        c(ParameterDefinition.Parse("").Count == 0, "empty param cell -> empty definition");
        c(ParameterSpec.TryParse("Type-name", out var s2) && s2.Format == LocalizationFormat.None, "spec without format -> None");
        c(ParameterSpec.TryParse("Type-name-BogusFmt", out var s3) && s3.Format == LocalizationFormat.Unsupported, "unknown format token -> Unsupported");
        c(def.Serialize().Contains("count") && def.Serialize().Contains(";"), "param def serialize round-trips");
    }

    private static void TagScannerOutcomes(Action<bool, string> c)
    {
        TagResolver up = (name, o) => o.Append(name.ToUpperInvariant());
        c(TagScanner.Process("", '{', '}', up, out var r0) == TagScanOutcome.Accepted && r0 == "", "scanner: empty input accepted");
        c(TagScanner.Process("plain", '{', '}', up, out var r1) == TagScanOutcome.Accepted && r1 == "plain", "scanner: no tags accepted");
        c(TagScanner.Process("a{b}c", '{', '}', up, out var r2) == TagScanOutcome.Accepted && r2 == "aBc", "scanner: single tag substituted");
        c(TagScanner.Process("{}", '{', '}', up, out _) == TagScanOutcome.RejectedEmptyTag, "scanner: empty tag rejected");
        c(TagScanner.Process("{a{b}", '{', '}', up, out _) == TagScanOutcome.RejectedNested, "scanner: nested open rejected");
        c(TagScanner.Process("a}b", '{', '}', up, out _) == TagScanOutcome.RejectedMismatched, "scanner: stray close rejected");
        c(TagScanner.Process("{a", '{', '}', up, out _) == TagScanOutcome.RejectedMismatched, "scanner: unterminated open rejected");
    }

    private static void NumberAbbrev(Action<bool, string> c)
    {
        c(NumberAbbreviator.Abbreviate(999) == "999", "abbrev: <1e3 raw");
        c(NumberAbbreviator.Abbreviate(1000) == "1K", "abbrev: 1000 -> 1K (2nd digit 0)");
        c(NumberAbbreviator.Abbreviate(1500) == "1.5K", "abbrev: 1500 -> 1.5K");
        c(NumberAbbreviator.Abbreviate(1234567) == "1.2M", "abbrev: 1,234,567 -> 1.2M");
        c(NumberAbbreviator.Abbreviate(12345678) == "12M", "abbrev: 12.3M -> 12M (two integer sig digits)");
        c(NumberAbbreviator.Abbreviate(123456789) == "120M", "abbrev: 123M -> 120M");
        c(NumberAbbreviator.Abbreviate(1e9) == "1B", "abbrev: 1e9 -> 1B");
        c(NumberAbbreviator.Abbreviate(2.5e12) == "2.5T", "abbrev: 2.5e12 -> 2.5T");
        c(NumberAbbreviator.Abbreviate(5e15) == "5q", "abbrev: 5e15 -> 5q");
        c(NumberAbbreviator.Abbreviate(1e18) == "1Q", "abbrev: 1e18 -> 1Q");
        c(NumberAbbreviator.Abbreviate(-1500) == "-1.5K", "abbrev: negative keeps sign");
    }

    private sealed class FixedContext : ILocalizationContext
    {
        public CultureInfo Culture => CultureInfo.InvariantCulture;
        public string Localize(LocalizationKey key) => key.Value == LocalizationConstants.HourAbbreviationKey ? "hr" : key.Value;
    }

    private static void Formatters(Action<bool, string> c)
    {
        var reg = new FormatterRegistry();
        var ctx = new FixedContext();

        c(Format(reg, new NumberValue(1234), LocalizationFormat.CurrencyWhole, ctx) == "1,234", "fmt: currency whole N0");
        c(Format(reg, new NumberValue(1234.5), LocalizationFormat.CurrencyDecimal, ctx) == "1,234.50", "fmt: currency decimal N2");
        c(Format(reg, new NumberValue(10), LocalizationFormat.CurrencyHourly, ctx) == "+10/hr", "fmt: hourly currency + localized hour");
        c(Format(reg, new NumberValue(2000), LocalizationFormat.NumberAbbreviated, ctx) == "2K", "fmt: number abbreviated");
        c(Format(reg, new DurationValue(new TimeSpan(1, 2, 3, 4)), LocalizationFormat.Duration, ctx) == "1 02:03:04", "fmt: duration with days");
        c(Format(reg, new DurationValue(new TimeSpan(0, 5, 6, 7)), LocalizationFormat.Duration, ctx) == "05:06:07", "fmt: duration sub-day");
        c(Format(reg, new CoordinatesValue(3, 7), LocalizationFormat.Coordinates, ctx) == "X:3 Y:7", "fmt: coordinates");
        c(Format(reg, new CoordinatesValue(3, 7), LocalizationFormat.Size, ctx) == "3x7", "fmt: size");
        c(Format(reg, new CoordinatesValue(3, 7), LocalizationFormat.BracketedSize, ctx) == "[3x7]", "fmt: bracketed size");
        c(Format(reg, new TextValue("raw"), LocalizationFormat.None, ctx) == "raw", "fmt: None falls back to value text");

        // Custom formatter wins over built-in and is tried in insertion order.
        reg.Register(new UpperFormatter());
        c(Format(reg, new TextValue("hi"), LocalizationFormat.Unsupported, ctx) == "HI", "fmt: custom registry consulted first");
    }

    private sealed class UpperFormatter : IValueFormatter
    {
        public bool TryFormat(ILocalizationValue value, LocalizationFormat format, ILocalizationContext context, StringBuilder output)
        {
            if (format != LocalizationFormat.Unsupported) return false;
            var sb = new StringBuilder(); value.AppendTo(sb);
            output.Append(sb.ToString().ToUpperInvariant());
            return true;
        }
    }

    private static string Format(FormatterRegistry reg, ILocalizationValue v, LocalizationFormat f, ILocalizationContext ctx)
    {
        var sb = new StringBuilder(); reg.Format(v, f, ctx, sb); return sb.ToString();
    }

    private static void Ini(Action<bool, string> c)
    {
        var doc = new IniDocument();
        var s = doc.Section("en-US");
        s["greeting"] = "Hello";
        s["multi"] = "line1\nline2";
        string text = doc.Write();
        c(text.Contains("[en-US]") && text.Contains("multi=line1\\line2"), "ini: newline escaped on write");

        var back = IniDocument.Parse(text);
        c(back.TryGetSection("en-US", out var s2) && s2["greeting"] == "Hello", "ini: round-trip key");
        c(s2["multi"] == "line1\nline2", "ini: newline unescaped on read");

        var defs = new LocalizationDefinitions();
        defs.Parameters["k"] = ParameterDefinition.Parse("int-n-NumberAbbreviated");
        defs.DynamicKeys["item_{id}"] = ParameterDefinition.Parse("int-id");
        defs.Languages.Add(new LanguageInfo("en-US", "English"));
        var parsed = LocalizationDefinitions.FromIni(defs.ToIni());
        c(parsed.ParametersFor("k").Count == 1 && parsed.TryGetDynamicKey("item_{id}", out _), "ini: definitions round-trip");
        c(parsed.Languages.Count == 1 && parsed.Languages[0].DisplayName == "English", "ini: language definitions round-trip");
    }

    private static void Redirections(Action<bool, string> c)
    {
        var r = LanguageRedirections.CreateDefault();
        c(r.TryRedirect("en-UK", out var a) && a == "en-GB", "redirect: en-UK -> en-GB (exact)");
        c(r.TryRedirect("en-AU", out var b) && b == "en-US", "redirect: en-* -> en-US (prefix)");
        c(!r.TryRedirect("tr-TR", out _), "redirect: unrelated code not redirected");
    }

    private static void Selection(Action<bool, string> c)
    {
        var supported = new List<LanguageKey> { new LanguageKey("en-US"), new LanguageKey("tr-TR"), new LanguageKey("en-GB") };
        var red = LanguageRedirections.CreateDefault();

        var a = LanguageSelector.Resolve("tr-TR", "en-US", supported, red, out bool clearA);
        c(a.Code == "tr-TR" && !clearA, "select: supported preference wins");

        var b = LanguageSelector.Resolve(null, "en-US", supported, red, out _);
        c(b.Code == "en-US", "select: device culture when no preference");

        var d = LanguageSelector.Resolve("de-DE", "tr-TR", supported, red, out bool clearD);
        c(d.Code == "tr-TR" && clearD, "select: stale preference cleared, falls to device");

        var e = LanguageSelector.Resolve("en-UK", "tr-TR", supported, red, out _);
        c(e.Code == "en-GB", "select: preference redirected to supported");

        var f = LanguageSelector.Resolve(null, "en-AU", supported, red, out _);
        c(f.Code == "en-US", "select: device redirected via prefix");

        var g = LanguageSelector.Resolve(null, "fr-FR", supported, red, out _);
        c(g.Code == LocalizationConstants.DefaultLanguageCode, "select: default when nothing resolves");
    }

    private static void Csv(Action<bool, string> c)
    {
        var simple = CsvReader.Parse("a,b,c");
        c(simple.Count == 1 && simple[0].Count == 3 && simple[0][2] == "c", "csv: simple row");

        var quoted = CsvReader.Parse("a,\"b,c\",d");
        c(quoted[0].Count == 3 && quoted[0][1] == "b,c", "csv: quoted comma preserved");

        var esc = CsvReader.Parse("\"he said \"\"hi\"\"\"");
        c(esc[0][0] == "he said \"hi\"", "csv: escaped quotes");

        var nl = CsvReader.Parse("a,\"line1\nline2\"");
        c(nl[0][1] == "line1\nline2", "csv: newline inside quotes");

        var trailing = CsvReader.Parse("a,b\n");
        c(trailing.Count == 1, "csv: trailing newline no phantom record");

        var crlf = CsvReader.Parse("a,b\r\nc,d");
        c(crlf.Count == 2 && crlf[1][0] == "c", "csv: CRLF splits records");
    }

    private static void TableBuilder(Action<bool, string> c)
    {
        string csv =
            "Key,ParameterDefinition,en-US,tr-TR\n" +
            "greeting,,Hello,Merhaba\n" +
            "count,int-n-NumberAbbreviated,You have {n},\n" +
            "item_{id}_name,int-id,Sword,\n";

        var result = LocalizationTableBuilder.Build(csv, new[] { typeof(Status) });
        c(result.Languages.Count == 2, "builder: two languages from header");

        var defs = LocalizationDefinitions.FromIni(IniDocument.Parse(result.DefinitionsText));
        c(defs.ParametersFor("count").Count == 1, "builder: param key routed to ParameterDefinitions");
        c(defs.TryGetDynamicKey("item_{id}_name", out _), "builder: dynamic key routed to DynamicKeyDefinitions");
        c(!defs.Parameters.ContainsKey("greeting"), "builder: empty param cell not stored");

        var content = ContentTables.FromText(result.ContentText);
        var en = content.Load(new LanguageKey("en-US"));
        var tr = content.Load(new LanguageKey("tr-TR"));
        c(en["greeting"] == "Hello" && tr["greeting"] == "Merhaba", "builder: content per language");
        c(!tr.ContainsKey("count"), "builder: empty translation cell skipped");
        c(en.ContainsKey("Status_Idle") && en["Status_Active"] == LocalizationConstants.MissingEnumText, "builder: enum keys auto-added with placeholder");
        c(tr.ContainsKey("Status_Active"), "builder: enum keys added in every language");
    }

    private static void EnumKeys(Action<bool, string> c)
    {
        c(EnumKeyGenerator.KeyFor(Status.Active) == "Status_Active", "enum: default prefix is type name");
        c(EnumKeyGenerator.KeyFor(Color.Red) == "clr_Red", "enum: attribute prefix override");
        c(EnumKeyGenerator.IsLocalizable(typeof(Status)) && !EnumKeyGenerator.IsLocalizable(typeof(int)), "enum: IsLocalizable gate");

        ILocalizationValue v = new EnumValue(Color.Green);
        c(v.TryGetLocalizationKey(out var k) && k.Value == "clr_Green", "enum value: resolves to its key");
        c(v.TryGetDouble(out double d) && d == 1, "enum value: exposes numeric value");
    }

    private static void Catalog(Action<bool, string> c)
    {
        var en = new LanguageKey("en-US");
        var tr = new LanguageKey("tr-TR");
        var source = new InMemoryLocalizationSource()
            .Add(en, new Dictionary<string, string>
            {
                ["hello"] = "Hello",
                ["gold"] = "You own {amount}",
                ["pos"] = "At {loc}",
                ["state"] = "State: {s}",
                ["Status_Active"] = "ACTIVE",
                ["answer"] = "forty-two",
                ["pick"] = "Chosen: {v}",
                ["item_5_name"] = "Sword",
                ["only_en"] = "OnlyEN",
            })
            .Add(tr, new Dictionary<string, string>
            {
                ["hello"] = "Merhaba",
            });

        var defs = new LocalizationDefinitions();
        defs.Parameters["gold"] = ParameterDefinition.Parse("int-amount-NumberAbbreviated");
        defs.Parameters["pos"] = ParameterDefinition.Parse("Vec-loc-Coordinates");
        defs.Parameters["state"] = ParameterDefinition.Parse("Enum-s");
        defs.Parameters["pick"] = ParameterDefinition.Parse("Num-v-CurrencyWhole");
        defs.DynamicKeys["item_{id}_name"] = ParameterDefinition.Parse("int-id");

        var catalog = new LocalizationCatalog(source, en, defs);
        catalog.SetCulture(CultureInfo.InvariantCulture);

        c(catalog.Get("hello") == "Hello", "catalog: plain get");
        c(catalog.Get("gold", new NumberValue(1500000)) == "You own 1.5M", "catalog: number-abbreviated tag");
        c(catalog.Get("pos", new CoordinatesValue(3, 7)) == "At X:3 Y:7", "catalog: coordinates tag");
        c(catalog.Get("state", new EnumValue(Status.Active)) == "State: ACTIVE", "catalog: value-key (enum) beats formatting");
        c(catalog.Get("item_{id}_name", new TextValue("5")) == "Sword", "catalog: dynamic key substitution");

        catalog.SetValueKeyResolver(new RecordingResolver());
        c(catalog.Get("pick", new NumberValue(42)) == "Chosen: forty-two", "catalog: value-key resolver hook wins");
        c(catalog.Get("pick", new NumberValue(7)) == "Chosen: 7", "catalog: falls to format when hook declines");

        c(!catalog.TryGet("nope", out _), "catalog: TryGet false when missing");
        catalog.SwitchLanguage(tr);
        c(catalog.TryGet("only_en", out var t, out bool fromFb) && fromFb && t == "OnlyEN", "catalog: fallback-aware reports fallback");

        bool threw = false;
        try { catalog.GetEnsured("nope"); } catch (KeyNotFoundException) { threw = true; }
        c(threw, "catalog: GetEnsured throws when missing");
        c(catalog.GetOrErrorText("nope") == "# [nope] #", "catalog: GetOrErrorText placeholder");
        c(catalog.Get("nope") == "nope", "catalog: Get returns raw key when missing");
    }
}
