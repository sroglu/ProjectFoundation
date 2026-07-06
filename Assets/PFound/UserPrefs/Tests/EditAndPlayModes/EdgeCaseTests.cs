using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class EdgeCaseTests
    {
        private static readonly PrefKey<int> Existing = new PrefKey<int>("edge.existing", 7);
        private static readonly PrefKey<string> NewlyAdded = new PrefKey<string>("edge.newlyAdded", "default-tag");

        [Serializable]
        private struct Profile { public string Name; public int Avatar; }
        private static readonly PrefKey<Profile> Profile1 = new PrefKey<Profile>("edge.profile",
            new Profile { Name = "default", Avatar = 0 });

        private string _tmpFile;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = $"edge_test_{Guid.NewGuid():N}.json";
            PlayerPrefs.DeleteKey(Existing.Key);
            PlayerPrefs.DeleteKey(NewlyAdded.Key);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Existing.Key);
            PlayerPrefs.DeleteKey(NewlyAdded.Key);
            var path = Path.Combine(Application.persistentDataPath, _tmpFile);
            if (File.Exists(path)) File.Delete(path);
            var tmp = path + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        [Test]
        public void CorruptedJsonFile_LogsAndFallsBackToDefaults_NoThrow()
        {
            var path = Path.Combine(Application.persistentDataPath, _tmpFile);
            File.WriteAllText(path, "{not json at all :::");

            using var store = new PrefsBuilder()
                .Add(Profile1)
                .WithJsonStorage(_tmpFile)
                .WithLogger(new NullLogger())
                .Build();

            var p = store.Get(Profile1);
            Assert.AreEqual("default", p.Name);
            Assert.AreEqual(0, p.Avatar);
        }

        [Test]
        public void NewFieldAddedAfterSave_ReturnsDefault_ExistingPreserved()
        {
            // First boot: only "existing" registered, set to 42, save.
            using (var first = new PrefsBuilder()
                .Add(Existing)
                .WithJsonStorage(_tmpFile)
                .WithLogger(new NullLogger())
                .Build())
            {
                first.Set(Existing, 42);
                first.Flush();
            }

            // Second boot: "newlyAdded" key added to schema. "existing" should still be 42; new key returns default.
            using (var second = new PrefsBuilder()
                .Add(Existing)
                .Add(NewlyAdded)
                .WithJsonStorage(_tmpFile)
                .WithLogger(new NullLogger())
                .Build())
            {
                Assert.AreEqual(42, second.Get(Existing));
                Assert.AreEqual("default-tag", second.Get(NewlyAdded));
            }
        }

        [Test]
        public void RemovedFieldFromSchema_StoredValueIgnoredOnRead_NoError()
        {
            // First boot: register both, write Existing.
            using (var first = new PrefsBuilder()
                .Add(Existing)
                .Add(NewlyAdded)
                .WithJsonStorage(_tmpFile)
                .WithLogger(new NullLogger())
                .Build())
            {
                first.Set(Existing, 99);
                first.Set(NewlyAdded, "stored-value");
                first.Flush();
            }

            // Second boot: only Existing registered. Stored "newlyAdded" is ignored.
            Assert.DoesNotThrow(() =>
            {
                using var second = new PrefsBuilder()
                    .Add(Existing)
                    .WithJsonStorage(_tmpFile)
                    .WithLogger(new NullLogger())
                    .Build();

                Assert.AreEqual(99, second.Get(Existing));
            });
        }
    }
}
