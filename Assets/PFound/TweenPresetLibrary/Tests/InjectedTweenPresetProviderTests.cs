using NUnit.Framework;
using UnityEngine;

namespace PFound.TweenPresetLibrary.Tests
{
    /// <summary>The injected (non-Resources) resolve path: keying by asset name or a custom selector, lookups, and edge cases.</summary>
    public sealed class InjectedTweenPresetProviderTests
    {
        private static TweenPreset Make(string name)
        {
            var p = ScriptableObject.CreateInstance<TweenPreset>();
            p.name = name;
            return p;
        }

        [Test]
        public void Resolves_ByAssetName_ByDefault()
        {
            var a = Make("fade_in");
            var b = Make("pop");
            try
            {
                var provider = new InjectedTweenPresetProvider(new[] { a, b });

                Assert.IsTrue(provider.TryGet("fade_in", out var got));
                Assert.AreSame(a, got, "resolves to the injected instance");
                Assert.IsTrue(provider.Contains("pop"));

                Assert.IsFalse(provider.TryGet("missing", out var miss));
                Assert.IsNull(miss, "a miss returns null");

                CollectionAssert.AreEquivalent(new[] { "fade_in", "pop" }, provider.Keys);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void CustomKeySelector_KeysBySelectorOutput()
        {
            var a = Make("x");
            try
            {
                var provider = new InjectedTweenPresetProvider(new[] { a }, p => "ui/" + p.name);
                Assert.IsTrue(provider.Contains("ui/x"));
                Assert.IsFalse(provider.Contains("x"), "the raw name is not a key under a custom selector");
            }
            finally { Object.DestroyImmediate(a); }
        }

        [Test]
        public void SkipsNullPresets_AndHandlesNullKeysSafely()
        {
            var a = Make("ok");
            try
            {
                var provider = new InjectedTweenPresetProvider(new TweenPreset[] { a, null });
                Assert.AreEqual(1, provider.Keys.Count, "null preset skipped");
                Assert.IsFalse(provider.TryGet(null, out _), "null key is a safe miss");
                Assert.IsFalse(provider.Contains(""), "empty key is a safe miss");
            }
            finally { Object.DestroyImmediate(a); }
        }

        [Test]
        public void DuplicateKey_LastWins()
        {
            var a = Make("dup");
            var b = Make("dup");
            try
            {
                var provider = new InjectedTweenPresetProvider(new[] { a, b });
                Assert.AreEqual(1, provider.Keys.Count);
                provider.TryGet("dup", out var got);
                Assert.AreSame(b, got, "the later duplicate overwrites the earlier");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
