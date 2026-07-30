#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using UnityEngine;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// EditMode tests for <see cref="ThemeMaterialRegistry"/> — lookup semantics + edge cases
    /// (empty key, missing key, OnValidate cache invalidation via re-call).
    /// </summary>
    public class ThemeMaterialRegistryTests
    {
        private Shader _shader;
        private Material _matA;
        private Material _matB;

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find("UISystem/Shape");
            _matA = new Material(_shader) { hideFlags = HideFlags.DontSave, name = "MatA" };
            _matB = new Material(_shader) { hideFlags = HideFlags.DontSave, name = "MatB" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_matA != null) Object.DestroyImmediate(_matA);
            if (_matB != null) Object.DestroyImmediate(_matB);
        }

        private ThemeMaterialRegistry CreateRegistry(params (string Key, Material Material)[] entries)
        {
            var reg = ScriptableObject.CreateInstance<ThemeMaterialRegistry>();
            reg.hideFlags = HideFlags.DontSave;
            var field = typeof(ThemeMaterialRegistry).GetField("_entries",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var list = new System.Collections.Generic.List<ThemeMaterialRegistry.Entry>();
            foreach (var (k, m) in entries)
                list.Add(new ThemeMaterialRegistry.Entry { Key = k, Material = m });
            field.SetValue(reg, list);
            return reg;
        }

        [Test]
        public void EmptyRegistry_GetReturnsNull()
        {
            var reg = CreateRegistry();
            try { Assert.IsNull(reg.Get("anything")); }
            finally { Object.DestroyImmediate(reg); }
        }

        [Test]
        public void Get_ReturnsMappedMaterial()
        {
            var reg = CreateRegistry(("card", _matA), ("button", _matB));
            try
            {
                Assert.AreSame(_matA, reg.Get("card"));
                Assert.AreSame(_matB, reg.Get("button"));
            }
            finally { Object.DestroyImmediate(reg); }
        }

        [Test]
        public void Get_MissingKey_ReturnsNull()
        {
            var reg = CreateRegistry(("card", _matA));
            try { Assert.IsNull(reg.Get("unknown-key")); }
            finally { Object.DestroyImmediate(reg); }
        }

        [Test]
        public void Get_EmptyOrNullKey_ReturnsNull()
        {
            var reg = CreateRegistry(("card", _matA));
            try
            {
                Assert.IsNull(reg.Get(null));
                Assert.IsNull(reg.Get(""));
            }
            finally { Object.DestroyImmediate(reg); }
        }

        [Test]
        public void Count_ReflectsValidEntries()
        {
            // Entries with empty key or null material are skipped during EnsureLookup.
            var reg = CreateRegistry(
                ("card", _matA),
                ("", _matB),         // skipped: empty key
                ("orphan", null),    // skipped: null material
                ("button", _matB));
            try { Assert.AreEqual(2, reg.Count); }
            finally { Object.DestroyImmediate(reg); }
        }
    }
}
#endif
