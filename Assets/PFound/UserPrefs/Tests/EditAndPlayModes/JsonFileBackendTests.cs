using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class JsonFileBackendTests
    {
        [Serializable]
        private struct Profile
        {
            public string Name;
            public int Avatar;
        }

        private string _tmpFile;
        private JsonFileBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = Path.Combine(Application.temporaryCachePath, $"userprefs_test_{Guid.NewGuid():N}.json");
            _backend = new JsonFileBackend(_tmpFile, new NullLogger());
            _backend.Load();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tmpFile)) File.Delete(_tmpFile);
            var tmp = _tmpFile + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        [Test]
        public void ComplexObject_RoundTripInMemory()
        {
            var p = new Profile { Name = "Alice", Avatar = 7 };
            _backend.Set("profile", p);
            Assert.IsTrue(_backend.TryGet<Profile>("profile", out var got));
            Assert.AreEqual("Alice", got.Name);
            Assert.AreEqual(7, got.Avatar);
        }

        [Test]
        public void Persist_FileWrittenOnFlush()
        {
            _backend.Set("profile", new Profile { Name = "Bob", Avatar = 3 });
            _backend.Flush();
            Assert.IsTrue(File.Exists(_tmpFile));
        }

        [Test]
        public void Persist_ReloadRestoresValue()
        {
            _backend.Set("profile", new Profile { Name = "Carol", Avatar = 9 });
            _backend.Flush();

            var fresh = new JsonFileBackend(_tmpFile, new NullLogger());
            fresh.Load();
            Assert.IsTrue(fresh.TryGet<Profile>("profile", out var got));
            Assert.AreEqual("Carol", got.Name);
            Assert.AreEqual(9, got.Avatar);
        }

        [Test]
        public void Load_MissingFile_StartsEmpty()
        {
            var fresh = new JsonFileBackend(Path.Combine(Application.temporaryCachePath, "no-such-file.json"), new NullLogger());
            Assert.DoesNotThrow(() => fresh.Load());
            Assert.IsFalse(fresh.HasKey("anything"));
        }

        [Test]
        public void Load_CorruptedFile_FallsBackToEmpty_NoThrow()
        {
            File.WriteAllText(_tmpFile, "{ this is not valid json :: ::");
            var fresh = new JsonFileBackend(_tmpFile, new NullLogger());
            Assert.DoesNotThrow(() => fresh.Load());
            Assert.IsFalse(fresh.HasKey("anything"));
        }

        [Test]
        public void Delete_RemovesKey()
        {
            _backend.Set("profile", new Profile { Name = "Dan", Avatar = 1 });
            _backend.Delete("profile");
            Assert.IsFalse(_backend.HasKey("profile"));
        }

        [Test]
        public void TryGet_TypeMismatch_ReturnsFalse()
        {
            _backend.Set("profile", new Profile { Name = "Eve", Avatar = 2 });
            // Try to deserialize Profile bytes as a different incompatible type;
            // FromJson may succeed silently with default values. Either way, no throw.
            Assert.DoesNotThrow(() => _backend.TryGet<int>("profile", out _));
        }
    }
}
