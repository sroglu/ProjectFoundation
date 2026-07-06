using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class PrefsStoreTests
    {
        private static readonly PrefKey<int> Level = new PrefKey<int>("test.level", 1);
        private static readonly PrefKey<float> Volume = new PrefKey<float>("test.volume", 0.5f);

        private string _tmpFile;
        private IPrefsStore _store;

        [Serializable]
        private struct Profile { public string Name; public int Avatar; }

        private static readonly PrefKey<Profile> ProfileKey = new PrefKey<Profile>("test.profile",
            new Profile { Name = "default", Avatar = 0 });

        [SetUp]
        public void SetUp()
        {
            _tmpFile = $"userprefs_test_{Guid.NewGuid():N}.json";
            // Ensure clean PlayerPrefs
            PlayerPrefs.DeleteKey("test.level");
            PlayerPrefs.DeleteKey("test.volume");
            _store = new PrefsBuilder()
                .Add(Level)
                .Add(Volume)
                .Add(ProfileKey)
                .WithJsonStorage(_tmpFile)
                .WithLogger(new NullLogger())
                .Build();
        }

        [TearDown]
        public void TearDown()
        {
            _store?.Dispose();
            PlayerPrefs.DeleteKey("test.level");
            PlayerPrefs.DeleteKey("test.volume");
            var path = Path.Combine(Application.persistentDataPath, _tmpFile);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Get_NoStoredValue_ReturnsDefault()
        {
            Assert.AreEqual(1, _store.Get(Level));
            Assert.AreEqual(0.5f, _store.Get(Volume));
        }

        [Test]
        public void Set_Then_Get_ReturnsLastSet()
        {
            _store.Set(Level, 7);
            Assert.AreEqual(7, _store.Get(Level));
        }

        [Test]
        public void Clear_RestoresDefault()
        {
            _store.Set(Level, 12);
            _store.Clear(Level);
            Assert.AreEqual(1, _store.Get(Level));
        }

        [Test]
        public void Builder_BuildTwice_Throws()
        {
            var b = new PrefsBuilder().Add(new PrefKey<int>("dup.test", 0));
            b.Build();
            Assert.Throws<InvalidOperationException>(() => b.Build());
        }

        [Test]
        public void Builder_DuplicateKey_Throws()
        {
            var b = new PrefsBuilder().Add(new PrefKey<int>("dup.test2", 0));
            Assert.Throws<ArgumentException>(() => b.Add(new PrefKey<int>("dup.test2", 1)));
        }

        [Test]
        public void Disposed_AccessThrows()
        {
            _store.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _store.Get(Level));
            _store = null;
        }

        [Test]
        public void Set_ComplexObject_PersistsViaJson()
        {
            _store.Set(ProfileKey, new Profile { Name = "Alice", Avatar = 5 });
            _store.Flush();

            var got = _store.Get(ProfileKey);
            Assert.AreEqual("Alice", got.Name);
            Assert.AreEqual(5, got.Avatar);
        }
    }
}
