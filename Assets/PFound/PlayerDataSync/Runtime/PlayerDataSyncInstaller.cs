using System.Collections.Generic;
using PFound.PlayerDataSync.Core;
using PFound.RemoteGameConfig.Core;
using UnityEngine;

namespace PFound.PlayerDataSync
{
    /// <summary>
    /// Unity-side wiring that assembles a <see cref="PlayerSaveSyncEngine"/> from a backend store and the
    /// app's environment: the durable local copy under persistentDataPath, the system clock and a real delay
    /// for retry backoff, and the stable install id. The install id comes from the SAME source the rest of
    /// the framework uses — <see cref="IInstallIdentity"/> from RemoteGameConfig — so there is exactly one
    /// per-install GUID, never a second one minted here. The game supplies its backend
    /// <see cref="IRemotePlayerStore"/> (Firestore, self-host, or a test double) plus its migration steps;
    /// the extractor and conflict policy default to the canonical field reader and last-write-wins but can
    /// be overridden. <see cref="PlayerSaveSyncEngine.Current"/> is valid immediately after Build; the caller
    /// awaits <see cref="PlayerSaveSyncEngine.LoadAsync"/> to reconcile with the backend.
    /// </summary>
    public static class PlayerDataSyncInstaller
    {
        public static PlayerSaveSyncEngine Build(
            IInstallIdentity identity,
            IRemotePlayerStore remoteStore,
            int currentSchemaVersion,
            IEnumerable<IPlayerSaveMigration> migrations = null,
            IPlayerFieldExtractor extractor = null,
            IPlayerSaveConflictPolicy conflictPolicy = null)
        {
            var local = new FilePlayerSaveStore(Application.persistentDataPath);
            var migrator = new PlayerSaveMigrator(currentSchemaVersion, migrations ?? System.Array.Empty<IPlayerSaveMigration>());

            return new PlayerSaveSyncEngine(
                identity.InstallId,
                remoteStore,
                local,
                extractor ?? new CanonicalPlayerFieldExtractor(),
                migrator,
                conflictPolicy ?? new LastWriteWinsConflictPolicy(),
                new SystemSyncClock(),
                new TaskSyncDelay(),
                RetryPolicy.Default);
        }
    }
}
