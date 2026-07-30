using System;
using System.Text;
using PFound.Utilities.StringTools;

// Standalone mono/csc runner (pure C#, no NUnit) — behavior oracle for StringTools core.
// Exit code 0 = all pass.
internal static class StringToolsTests
{
    private static int s_passed, s_failed;

    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    // Engine-free sink that appends literal text verbatim and wraps each tag body in angle
    // brackets, so a test can tell text apart from tags in the assembled output.
    private sealed class MarkerSink : StringTools.ITagSink
    {
        public readonly StringBuilder Output = new StringBuilder();
        public void AppendText(ReadOnlySpan<char> text) => Output.Append(text.ToString());
        public void AppendTag(ReadOnlySpan<char> tag) => Output.Append('<').Append(tag.ToString()).Append('>');
    }

    private static StringTools.TagProcessResult RunTags(string input, out string output)
    {
        var sink = new MarkerSink();
        var result = input.ProcessTags('{', '}', sink);
        output = sink.Output.ToString();
        return result;
    }

    public static int Main()
    {
        // Between
        Check("[a]hello[/a]".Between("[a]", "[/a]") == "hello", "Between basic");
        Check("no markers".Between("[", "]") == null, "Between missing -> null");
        Check("x<>y".Between("<", ">") == "", "Between empty content");

        // ReplaceFirst / ReplaceLast
        Check("a-a-a".ReplaceFirstOccurrence("a", "X") == "X-a-a", "ReplaceFirstOccurrence");
        Check("a-a-a".ReplaceLastOccurrence("a", "X") == "a-a-X", "ReplaceLastOccurrence");
        Check("abc".ReplaceFirstOccurrence("z", "X") == "abc", "ReplaceFirstOccurrence no match");

        // ReplaceInside / ReplaceAllInside
        Check("[a]hi[/a]".ReplaceInside("[a]", "[/a]", "YO") == "[a]YO[/a]", "ReplaceInside keepMarkers");
        Check("[a]hi[/a]".ReplaceInside("[a]", "[/a]", "YO", keepMarkers: false) == "YO", "ReplaceInside dropMarkers");
        Check("(1)(2)(3)".ReplaceAllInside("(", ")", "_") == "(_)(_)(_)", "ReplaceAllInside keepMarkers");
        Check("(1)(2)".ReplaceAllInside("(", ")", "_", keepMarkers: false) == "__", "ReplaceAllInside dropMarkers");

        // Repeat
        Check("ab".Repeat(3) == "ababab", "Repeat 3");
        Check("ab".Repeat(0) == "", "Repeat 0 -> empty");
        Check("ab".Repeat(1) == "ab", "Repeat 1");

        // TrimStart / TrimEnd (string)
        Check("xxxdata".TrimStart("x") == "data", "TrimStart single char run");
        Check("ababcore".TrimStart("ab") == "core", "TrimStart multichar run");
        Check("data...".TrimEnd(".") == "data", "TrimEnd single char run");
        Check("coreabab".TrimEnd("ab") == "core", "TrimEnd multichar run");
        Check("data".TrimStart("z") == "data", "TrimStart no match");

        // Counting
        Check("banana".CountCharacters('a') == 3, "CountCharacters");
        Check("aaaa".CountSubstrings("aa") == 2, "CountSubstrings non-overlapping");
        Check("hello".CountSubstrings("l") == 2, "CountSubstrings single");

        // Numeric classification
        Check("12345".IsNumeric(), "IsNumeric digits");
        Check("-42".IsNumeric(), "IsNumeric signed");
        Check(!"12.3".IsNumeric(), "IsNumeric rejects dot");
        Check(!"".IsNumeric(), "IsNumeric empty false");
        Check("12.34".IsNumericFloat(), "IsNumericFloat decimal");
        Check("-1.5e3".IsNumericFloat(), "IsNumericFloat exponent");
        Check(!"abc".IsNumericFloat(), "IsNumericFloat rejects letters");

        // Letters/digits
        Check("Abc123".IsAsciiLettersOrDigits(), "IsAsciiLettersOrDigits true");
        Check(!"Abc 123".IsAsciiLettersOrDigits(), "IsAsciiLettersOrDigits space false");
        Check("Abc123".IsLettersOrDigits(), "IsLettersOrDigits ascii");
        Check("Åß12".IsLettersOrDigits(), "IsLettersOrDigits letters");
        Check(!"a-b".IsLettersOrDigits(), "IsLettersOrDigits dash false");

        // Truncate
        Check("hello".Truncate(10) == "hello", "Truncate under limit");
        Check("hello world".Truncate(5) == "hello", "Truncate hard clip");
        Check("hello world".Truncate(8, "...") == "hello...", "Truncate with suffix in budget");
        Check("hello world".Truncate(8, "...").Length == 8, "Truncate respects budget");

        // Trailing number parsing
        Check("Item42".GetTrailingNumber() == 42, "GetTrailingNumber");
        Check("Item".GetTrailingNumber() == null, "GetTrailingNumber none -> null");
        Check("Clone (3)".GetTrailingParenthesizedNumber() == 3, "GetTrailingParenthesizedNumber");
        Check("Clone".GetTrailingParenthesizedNumber() == null, "GetTrailingParenthesizedNumber none");
        Check("Clone (3)".StripTrailingNumberedSuffix() == "Clone", "StripTrailingNumberedSuffix trims space");
        Check("Clone".StripTrailingNumberedSuffix() == "Clone", "StripTrailingNumberedSuffix noop");

        // Hex / binary
        Check(new byte[] { 0x00, 0xFF, 0x1A }.ToHexString() == "00ff1a", "ToHexString");
        Check(new byte[] { 0xA5 }.ToBinaryString() == "10100101", "ToBinaryString");

        // Line endings
        Check("a\nb\r\nc\rd".NormalizeToCRLF() == "a\r\nb\r\nc\r\nd", "NormalizeToCRLF");

        // Plural postfix
        Check(1.ToStringWithEnglishPluralPostfix("apple") == "1 apple", "Plural singular");
        Check(3.ToStringWithEnglishPluralPostfix("apple") == "3 apples", "Plural plural");
        Check(0.ToStringWithEnglishPluralPostfix("apple") == "0 apples", "Plural zero -> plural");

        // Base64 roundtrip
        Check("hello ünïcode".ToBase64().FromBase64() == "hello ünïcode", "Base64 roundtrip");

        // Wildcards
        Check("file.txt".MatchesWildcard("*.txt"), "Wildcard star match");
        Check(!"file.png".MatchesWildcard("*.txt"), "Wildcard star no match");
        Check("a1c".MatchesWildcard("a?c"), "Wildcard question match");
        Check("FILE.TXT".MatchesWildcard("*.txt"), "Wildcard case-insensitive default");
        Check(!"FILE.TXT".MatchesWildcard("*.txt", caseSensitive: true), "Wildcard case-sensitive");
        Check("a.b".MatchesWildcard("a.b"), "Wildcard escapes literal dot");
        Check(!"axb".MatchesWildcard("a.b"), "Wildcard literal dot not wildcard");

        // Tag processing ------------------------------------------------------
        // Successful passes: verify both the assembled output and the reported result.
        Check(RunTags("Hi {name}!", out var t1) == StringTools.TagProcessResult.Succeeded &&
              t1 == "Hi <name>!", "Tags single tag");
        Check(RunTags("{a}{b}", out var t2) == StringTools.TagProcessResult.Succeeded &&
              t2 == "<a><b>", "Tags adjacent tags");
        Check(RunTags("x {a} y {bb} z", out var t3) == StringTools.TagProcessResult.Succeeded &&
              t3 == "x <a> y <bb> z", "Tags text around tags");
        Check(RunTags("plain text", out var t4) == StringTools.TagProcessResult.SucceededButFoundNoTags &&
              t4 == "plain text", "Tags no tags -> literal");
        Check(RunTags("", out var t5) == StringTools.TagProcessResult.SucceededButEmptyInputText &&
              t5 == "", "Tags empty input");
        Check(RunTags(null, out var t5b) == StringTools.TagProcessResult.SucceededButEmptyInputText &&
              t5b == "", "Tags null input");
        Check(RunTags("{only}", out var t6) == StringTools.TagProcessResult.Succeeded &&
              t6 == "<only>", "Tags whole-string tag");

        // Failure passes: brace-structure errors.
        Check(RunTags("{a{b}}", out _) == StringTools.TagProcessResult.FailedDueToNestedTagBraces,
              "Tags nested braces");
        Check(RunTags("{{", out _) == StringTools.TagProcessResult.FailedDueToNestedTagBraces,
              "Tags immediate reopen is nested");
        Check(RunTags("{unclosed", out _) == StringTools.TagProcessResult.FailedDueToMismatchingTagBraces,
              "Tags unclosed opener");
        Check(RunTags("stray}", out _) == StringTools.TagProcessResult.FailedDueToMismatchingTagBraces,
              "Tags stray closer");
        Check(RunTags("ok {then} bad}", out _) == StringTools.TagProcessResult.FailedDueToMismatchingTagBraces,
              "Tags trailing stray closer");
        Check(RunTags("{}", out _) == StringTools.TagProcessResult.FailedDueToEmptyTagBraces,
              "Tags empty braces");
        Check(RunTags("a{}b", out _) == StringTools.TagProcessResult.FailedDueToEmptyTagBraces,
              "Tags empty braces mid-text");

        Console.WriteLine($"StringTools: {s_passed} passed, {s_failed} failed (total {s_passed + s_failed})");
        return s_failed == 0 ? 0 : 1;
    }
}
