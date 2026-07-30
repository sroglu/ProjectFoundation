using System.Collections.Generic;
using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiCommandParserTests
    {
        // ---- Tokenizer ---------------------------------------------------------

        static List<string> Tok(string line)
        {
            var t = new List<string>();
            Assert.IsTrue(KuiCommandParser.TryTokenize(line, t, out _),
                "expected tokenize success for: " + line);
            return t;
        }

        [Test]
        public void Tokenize_Empty_NoTokens()
        {
            var t = Tok("");
            Assert.AreEqual(0, t.Count);
        }

        [Test]
        public void Tokenize_Whitespace_NoTokens()
        {
            var t = Tok("   ");
            Assert.AreEqual(0, t.Count);
        }

        [Test]
        public void Tokenize_PlainArgs_SplitsOnSpaces()
        {
            var t = Tok("set-volume 0.5 true");
            Assert.AreEqual(3, t.Count);
            Assert.AreEqual("set-volume", t[0]);
            Assert.AreEqual("0.5", t[1]);
            Assert.AreEqual("true", t[2]);
        }

        [Test]
        public void Tokenize_QuotedString_KeepsSpaces()
        {
            var t = Tok("say \"hello world\"");
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual("say", t[0]);
            Assert.AreEqual("hello world", t[1]);
        }

        [Test]
        public void Tokenize_EscapedQuoteInsideQuotes()
        {
            var t = Tok("say \"a\\\"b\"");
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual("a\"b", t[1]);
        }

        [Test]
        public void Tokenize_UnbalancedQuote_Fails()
        {
            var t = new List<string>();
            Assert.IsFalse(KuiCommandParser.TryTokenize("say \"oops", t, out string err));
            StringAssert.Contains("unbalanced", err);
        }

        [Test]
        public void Tokenize_TrailingBackslashInQuote_Fails()
        {
            var t = new List<string>();
            Assert.IsFalse(KuiCommandParser.TryTokenize("say \"abc\\", t, out string err));
            StringAssert.Contains("backslash", err);
        }

        // ---- Binder ------------------------------------------------------------

        enum SampleEnum { Alpha, Beta, Gamma }

        static System.Reflection.MethodInfo M(string name) =>
            typeof(KuiCommandParserTests).GetMethod(name,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        // Sample static methods covering each supported parameter type.
        // Body is irrelevant — only the signature matters for the binder.
        static void _Mix(int a, float b, double c, bool d, string e, SampleEnum f) { }
        static void _Single(int x) { }

        static KuiCommandEntry EntryFor(System.Reflection.MethodInfo mi)
        {
            return new KuiCommandEntry
            {
                Name       = mi.Name,
                Method     = mi,
                Parameters = mi.GetParameters(),
                Category   = "Test",
            };
        }

        [Test]
        public void Bind_AllSupportedTypes_Roundtrip()
        {
            var args = new List<string> { "_Mix", "42", "1.5", "2.5", "true", "hi", "Beta" };
            var entry = EntryFor(M(nameof(_Mix)));
            Assert.IsTrue(KuiCommandParser.TryBind(entry, args, firstArg: 1, out var boxed, out _));
            Assert.AreEqual(42,           boxed[0]);
            Assert.AreEqual(1.5f,         boxed[1]);
            Assert.AreEqual(2.5,          boxed[2]);
            Assert.AreEqual(true,         boxed[3]);
            Assert.AreEqual("hi",         boxed[4]);
            Assert.AreEqual(SampleEnum.Beta, boxed[5]);
        }

        [Test]
        public void Bind_BoolAcceptsCommonAliases()
        {
            var entry = EntryFor(M(nameof(_Single)));
            // Reuse _Mix to exercise bool slot. Easier: write a bool-only sample.
            var args = new List<string> { "_Mix", "0", "0", "0", "yes", "x", "Alpha" };
            entry = EntryFor(M(nameof(_Mix)));
            Assert.IsTrue(KuiCommandParser.TryBind(entry, args, firstArg: 1, out var b, out _));
            Assert.AreEqual(true, b[3]);
        }

        [Test]
        public void Bind_BoolRejectsGarbage()
        {
            var entry = EntryFor(M(nameof(_Mix)));
            var args = new List<string> { "_Mix", "0", "0", "0", "maybe", "x", "Alpha" };
            Assert.IsFalse(KuiCommandParser.TryBind(entry, args, firstArg: 1, out _, out string err));
            StringAssert.Contains("bool", err);
        }

        [Test]
        public void Bind_EnumCaseInsensitive()
        {
            var entry = EntryFor(M(nameof(_Mix)));
            var args = new List<string> { "_Mix", "0", "0", "0", "true", "x", "gamma" };
            Assert.IsTrue(KuiCommandParser.TryBind(entry, args, firstArg: 1, out var b, out _));
            Assert.AreEqual(SampleEnum.Gamma, b[5]);
        }

        [Test]
        public void Bind_ArityMismatch_Fails()
        {
            var entry = EntryFor(M(nameof(_Single)));
            var args  = new List<string> { "_Single" };           // missing arg
            Assert.IsFalse(KuiCommandParser.TryBind(entry, args, firstArg: 1, out _, out string err));
            StringAssert.Contains("expected 1", err);
        }

        [Test]
        public void Bind_UnparseableInt_Fails()
        {
            var entry = EntryFor(M(nameof(_Single)));
            var args  = new List<string> { "_Single", "not-an-int" };
            Assert.IsFalse(KuiCommandParser.TryBind(entry, args, firstArg: 1, out _, out string err));
            StringAssert.Contains("Int32", err);
        }
    }
}
