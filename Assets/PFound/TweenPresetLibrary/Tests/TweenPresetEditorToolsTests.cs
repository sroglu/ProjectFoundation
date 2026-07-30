using NUnit.Framework;
using PFound.TweenPresetLibrary.Editor;

namespace PFound.TweenPresetLibrary.Tests
{
    /// <summary>The pure name-normalizer behind the "Normalize Selected Preset Name(s)" editor tool.</summary>
    public sealed class TweenPresetEditorToolsTests
    {
        [TestCase("Fade In", "fade_in")]
        [TestCase("  Pop-Up  ", "pop_up")]
        [TestCase("already_ok", "already_ok")]
        [TestCase("A--B  C", "a_b_c")]
        [TestCase("__x__", "x")]
        public void NormalizeName_AppliesConvention(string input, string expected)
        {
            Assert.AreEqual(expected, TweenPresetEditorTools.NormalizeName(input));
        }

        [Test]
        public void NormalizeName_NullAndEmptyPassThrough()
        {
            Assert.IsNull(TweenPresetEditorTools.NormalizeName(null));
            Assert.AreEqual("", TweenPresetEditorTools.NormalizeName(""));
        }
    }
}
