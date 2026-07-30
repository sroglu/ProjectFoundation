using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PFound.PlayerDataSync;
using PFound.PlayerDataSync.Core;

namespace PFound.PlayerDataSync.Tests
{
    /// <summary>
    /// EditMode coverage for the Unity glue: the durable file-backed local store plus the sync engine end to
    /// end. These exercise the paths that only exist off the pure Core — real atomic file writes under a temp
    /// directory, load-with-no-remote yielding a fresh default, save/reload durability, the offline queue
    /// surviving to a reconnect, and sync points writing without any per-frame churn.
    /// </summary>
    public sealed class PlayerDataSyncEditModeTests
    {
        const string Install = "test-install";
        const int Schema = 1;

        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pf_pds_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        PlayerSaveSyncEngine BuildEngine(StubRemotePlayerStore remote, ILocalPlayerSaveStore local)
        {
            return new PlayerSaveSyncEngine(
                Install, remote, local, new CanonicalPlayerFieldExtractor(),
                PlayerSaveMigrator.None(Schema), new LastWriteWinsConflictPolicy(),
                new SystemSyncClock(), new ImmediateDelay(), RetryPolicy.Default);
        }

        [Test]
        public void FileStore_WritesAtomically_AndRoundTrips()
        {
            var store = new FilePlayerSaveStore(_dir);
            Assert.IsFalse(store.TryLoad(Install, out _), "no file yet");

            store.Save(Install, new LocalSaveEntry("{\"schemaVersion\":1,\"saveCounter\":2,\"data\":{}}", true));
            Assert.IsTrue(store.TryLoad(Install, out LocalSaveEntry entry), "file present after save");
            Assert.IsTrue(entry.PendingUpload, "pending flag persisted");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "playersave_" + Install + ".json.tmp")), "temp file cleaned up after atomic replace");
        }

        [Test]
        public void Load_WithNoRemote_YieldsFreshDefault()
        {
            var remote = new StubRemotePlayerStore();
            var engine = BuildEngine(remote, new FilePlayerSaveStore(_dir));

            PlayerSave loaded = engine.LoadAsync().GetAwaiter().GetResult();
            Assert.AreEqual(Schema, loaded.SchemaVersion);
            Assert.AreEqual(0, loaded.SaveCounter, "fresh default, never saved");
        }

        [Test]
        public void SaveThenReload_RestoresSameData()
        {
            var remote = new StubRemotePlayerStore();
            var local = new FilePlayerSaveStore(_dir);
            var engine = BuildEngine(remote, local);

            engine.LoadAsync().GetAwaiter().GetResult();
            engine.Current.Data.SetInt("level", 42);
            engine.Save();
            SyncOutcome outcome = engine.FlushAsync().GetAwaiter().GetResult();
            Assert.AreEqual(SyncStatus.Synced, outcome.Status);

            // A brand-new engine + fresh local store reloads from the backend.
            var engine2 = BuildEngine(remote, new FilePlayerSaveStore(Path.Combine(_dir, "second")));
            PlayerSave reloaded = engine2.LoadAsync().GetAwaiter().GetResult();
            Assert.AreEqual(42, reloaded.Data.GetInt("level", 0));
        }

        [Test]
        public void OfflineSave_IsQueuedDurably_AndSyncsOnReconnect()
        {
            var remote = new StubRemotePlayerStore { Online = false };
            var local = new FilePlayerSaveStore(_dir);
            var engine = BuildEngine(remote, local);

            engine.LoadAsync().GetAwaiter().GetResult();
            engine.Current.Data.SetInt("level", 7);
            engine.Save();
            Assert.AreEqual(SyncStatus.Deferred, engine.FlushAsync().GetAwaiter().GetResult().Status);

            // The queued write is durable: a fresh store reading the same directory still sees it pending.
            Assert.IsTrue(new FilePlayerSaveStore(_dir).TryLoad(Install, out LocalSaveEntry queued) && queued.PendingUpload,
                "pending write survives on disk");

            remote.Online = true;
            Assert.AreEqual(SyncStatus.Synced, engine.FlushAsync().GetAwaiter().GetResult().Status);
            Assert.IsTrue(remote.Stored.ContainsKey(Install), "reached backend after reconnect");
        }

        [Test]
        public void SyncPoints_WriteWithoutPerFrameChurn()
        {
            var remote = new StubRemotePlayerStore();
            var local = new CountingLocalStore();
            var engine = BuildEngine(remote, local);

            engine.LoadAsync().GetAwaiter().GetResult();
            engine.Current.Data.SetInt("level", 1);
            engine.Save();
            engine.Current.Data.SetInt("level", 2);
            engine.Save();

            Assert.AreEqual(0, remote.SaveCalls, "event-driven Save never hits the network");
            Assert.AreEqual(2, local.Writes, "exactly one durable write per Save, no per-frame writes");

            engine.FlushAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, remote.SaveCalls, "a single flush performs one backend write");
        }

        sealed class StubRemotePlayerStore : IRemotePlayerStore
        {
            public readonly Dictionary<string, RemotePlayerRecord> Stored = new Dictionary<string, RemotePlayerRecord>();
            public bool Online = true;
            public int SaveCalls;

            public Task<RemoteLoadOutcome> LoadAsync(string installId, CancellationToken cancellationToken = default)
            {
                if (!Online) throw new Exception("offline");
                return Task.FromResult(Stored.TryGetValue(installId, out RemotePlayerRecord r)
                    ? RemoteLoadOutcome.Of(r.Blob)
                    : RemoteLoadOutcome.Missing);
            }

            public Task SaveAsync(string installId, RemotePlayerRecord record, CancellationToken cancellationToken = default)
            {
                if (!Online) throw new Exception("offline");
                SaveCalls++;
                Stored[installId] = record;
                return Task.CompletedTask;
            }
        }

        sealed class CountingLocalStore : ILocalPlayerSaveStore
        {
            readonly Dictionary<string, LocalSaveEntry> _map = new Dictionary<string, LocalSaveEntry>();
            public int Writes;

            public bool TryLoad(string installId, out LocalSaveEntry entry) => _map.TryGetValue(installId, out entry);
            public void Save(string installId, LocalSaveEntry entry) { Writes++; _map[installId] = entry; }
        }

        sealed class ImmediateDelay : ISyncDelay
        {
            public Task Wait(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
