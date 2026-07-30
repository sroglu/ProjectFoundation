using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class PlayerPrefsBackendTests
    {
        private const string Prefix = "__userprefs_test_";
        private PlayerPrefsBackend _backend;

        private enum Color { Red = 0, Green = 1, Blue = 2 }

        [SetUp]
        public void SetUp()
        {
            _backend = new PlayerPrefsBackend(new NullLogger());
        }

        [TearDown]
        public void TearDown()
        {
            // Cleanup any keys with our prefix
            string[] keys = { Prefix + "bool", Prefix + "int", Prefix + "long", Prefix + "float",
                              Prefix + "double", Prefix + "string", Prefix + "enum" };
            foreach (var k in keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        [Test]
        public void Bool_RoundTrip()
        {
            _backend.Set(Prefix + "bool", true);
            Assert.IsTrue(_backend.TryGet<bool>(Prefix + "bool", out var v) && v);
        }

        [Test]
        public void Int_RoundTrip()
        {
            _backend.Set(Prefix + "int", 42);
            Assert.IsTrue(_backend.TryGet<int>(Prefix + "int", out var v) && v == 42);
        }

        [Test]
        public void Long_RoundTrip()
        {
            _backend.Set(Prefix + "long", 9_999_999_999L);
            Assert.IsTrue(_backend.TryGet<long>(Prefix + "long", out var v) && v == 9_999_999_999L);
        }

        [Test]
        public void Float_RoundTrip()
        {
            _backend.Set(Prefix + "float", 3.14f);
            Assert.IsTrue(_backend.TryGet<float>(Prefix + "float", out var v) && Mathf.Approximately(v, 3.14f));
        }

        [Test]
        public void Double_RoundTrip()
        {
            _backend.Set(Prefix + "double", 2.71828);
            Assert.IsTrue(_backend.TryGet<double>(Prefix + "double", out var v));
            Assert.AreEqual(2.71828, v, 1e-9);
        }

        [Test]
        public void String_RoundTrip()
        {
            _backend.Set(Prefix + "string", "hello world");
            Assert.IsTrue(_backend.TryGet<string>(Prefix + "string", out var v) && v == "hello world");
        }

        [Test]
        public void Enum_RoundTrip()
        {
            _backend.Set(Prefix + "enum", Color.Blue);
            Assert.IsTrue(_backend.TryGet<Color>(Prefix + "enum", out var v) && v == Color.Blue);
        }

        [Test]
        public void TryGet_MissingKey_ReturnsFalse()
        {
            Assert.IsFalse(_backend.TryGet<int>(Prefix + "nonexistent", out _));
        }

        [Test]
        public void Delete_RemovesKey()
        {
            _backend.Set(Prefix + "int", 5);
            Assert.IsTrue(_backend.HasKey(Prefix + "int"));
            _backend.Delete(Prefix + "int");
            Assert.IsFalse(_backend.HasKey(Prefix + "int"));
        }

        [Test]
        public void Flush_NoCrash()
        {
            _backend.Set(Prefix + "int", 1);
            Assert.DoesNotThrow(() => _backend.Flush());
        }
    }
}
