using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiCategoryParserTests
    {
        [Test]
        public void Parse_WithPrefixAndSpace_StripsBoth()
        {
            KuiCategoryParser.Parse("[AI] hello world", out var cat, out var msg);
            Assert.AreEqual("AI", cat);
            Assert.AreEqual("hello world", msg);
        }

        [Test]
        public void Parse_WithPrefixNoSpace_OnlyStripsPrefix()
        {
            KuiCategoryParser.Parse("[Net]hello", out var cat, out var msg);
            Assert.AreEqual("Net", cat);
            Assert.AreEqual("hello", msg);
        }

        [Test]
        public void Parse_NoPrefix_ReturnsOriginal()
        {
            KuiCategoryParser.Parse("plain message", out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual("plain message", msg);
        }

        [Test]
        public void Parse_EmptyBrackets_NoMatch()
        {
            KuiCategoryParser.Parse("[] payload", out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual("[] payload", msg);
        }

        [Test]
        public void Parse_NestedOpenBracket_NoMatch()
        {
            KuiCategoryParser.Parse("[a[b]] payload", out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual("[a[b]] payload", msg);
        }

        [Test]
        public void Parse_TooLongCategory_NoMatch()
        {
            // 32-char category exceeds the 31-char cap.
            string cat32 = new string('a', 32);
            string input = "[" + cat32 + "] msg";
            KuiCategoryParser.Parse(input, out var cat, out var msg);
            Assert.IsNull(cat, "32-char category should be rejected");
            Assert.AreEqual(input, msg);
        }

        [Test]
        public void Parse_MaxLengthCategory_Accepted()
        {
            // 31-char category sits exactly on the cap.
            string cat31 = new string('a', 31);
            KuiCategoryParser.Parse("[" + cat31 + "] msg", out var cat, out var msg);
            Assert.AreEqual(cat31, cat);
            Assert.AreEqual("msg", msg);
        }

        [Test]
        public void Parse_MissingClosingBracket_NoMatch()
        {
            KuiCategoryParser.Parse("[no-end something", out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual("[no-end something", msg);
        }

        [Test]
        public void Parse_OnlyPrefixNoMessage_EmptyMessage()
        {
            KuiCategoryParser.Parse("[X]", out var cat, out var msg);
            Assert.AreEqual("X", cat);
            Assert.AreEqual(string.Empty, msg);
        }

        [Test]
        public void Parse_NullInput_EmptyMessage()
        {
            KuiCategoryParser.Parse(null, out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual(string.Empty, msg);
        }

        [Test]
        public void Parse_EmptyInput_EmptyMessage()
        {
            KuiCategoryParser.Parse(string.Empty, out var cat, out var msg);
            Assert.IsNull(cat);
            Assert.AreEqual(string.Empty, msg);
        }

        [Test]
        public void Parse_LeadingOpenBracketOnly_NoMatch()
        {
            KuiCategoryParser.Parse("[", out var cat, out var msg);
            Assert.IsNull(cat);
        }
    }
}
