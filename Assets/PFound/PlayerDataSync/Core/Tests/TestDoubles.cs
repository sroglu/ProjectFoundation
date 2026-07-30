using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.PlayerDataSync.Core.Tests
{
    /// <summary>An in-memory backend that can be toggled offline (load/save throw, mimicking a network fault) and pre-seeded to mimic another device's write.</summary>
    internal sealed class InMemoryRemotePlayerStore : IRemotePlayerStore
    {
        readonly Dictionary<string, RemotePlayerRecord> _store = new Dictionary<string, RemotePlayerRecord>();

        public bool Online = true;
        public int SaveCalls;
        public int LoadCalls;

        public void Seed(string installId, RemotePlayerRecord record) => _store[installId] = record;
        public bool Has(string installId) => _store.ContainsKey(installId);
        public RemotePlayerRecord Get(string installId) => _store[installId];

        public Task<RemoteLoadOutcome> LoadAsync(string installId, CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            if (!Online) throw new Exception("offline");
            RemoteLoadOutcome outcome = _store.TryGetValue(installId, out RemotePlayerRecord record)
                ? RemoteLoadOutcome.Of(record.Blob)
                : RemoteLoadOutcome.Missing;
            return Task.FromResult(outcome);
        }

        public Task SaveAsync(string installId, RemotePlayerRecord record, CancellationToken cancellationToken = default)
        {
            if (!Online) throw new Exception("offline");
            SaveCalls++;
            _store[installId] = record;
            return Task.CompletedTask;
        }
    }

    /// <summary>An in-memory stand-in for the durable local copy; counts writes so tests can assert no per-frame churn.</summary>
    internal sealed class InMemoryLocalPlayerSaveStore : ILocalPlayerSaveStore
    {
        readonly Dictionary<string, LocalSaveEntry> _map = new Dictionary<string, LocalSaveEntry>();
        public int WriteCount;

        public bool TryLoad(string installId, out LocalSaveEntry entry) => _map.TryGetValue(installId, out entry);

        public void Save(string installId, LocalSaveEntry entry)
        {
            WriteCount++;
            _map[installId] = entry;
        }
    }

    internal sealed class FixedClock : ISyncClock
    {
        public long NowUnixMs { get; set; }
    }

    /// <summary>Resolves backoff waits instantly so retry tests do not spend real time.</summary>
    internal sealed class ImmediateSyncDelay : ISyncDelay
    {
        public int Waits;
        public Task Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            Waits++;
            return Task.CompletedTask;
        }
    }

    /// <summary>A conflict policy that merges by summing an additive counter, to prove the hook overrides last-write-wins.</summary>
    internal sealed class AdditiveMergeConflictPolicy : IPlayerSaveConflictPolicy
    {
        public PlayerSave Resolve(PlayerSave local, PlayerSave remote)
        {
            long merged = local.Data.GetLong("coins", 0) + remote.Data.GetLong("coins", 0);
            PlayerSaveNode data = PlayerSaveNode.NewObject();
            data.SetLong("coins", merged);
            long counter = Math.Max(local.SaveCounter, remote.SaveCounter) + 1;
            return new PlayerSave(Math.Max(local.SchemaVersion, remote.SchemaVersion), counter, data);
        }
    }

    /// <summary>Renames the data key <c>hp</c> to <c>health</c> — a representative v1→v2 shape change.</summary>
    internal sealed class RenameHpToHealthMigration : IPlayerSaveMigration
    {
        public int FromVersion => 1;

        public void Apply(PlayerSaveNode data)
        {
            long hp = data.GetLong("hp", 0);
            data.Remove("hp");
            data.SetLong("health", hp);
        }
    }
}
