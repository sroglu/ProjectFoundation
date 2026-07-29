using System;
using System.Collections.Generic;

namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// One ordered step that upgrades a save's data tree from <see cref="FromVersion"/> to the next version.
    /// A migration is a pure function over the document: it rewrites <see cref="PlayerSaveNode"/> in place
    /// (rename a key, add a default, restructure a sub-object) and nothing else — no IO, no engine calls —
    /// so the whole upgrade path is deterministic and unit-testable. The pipeline runs the steps in order,
    /// so each step may assume the data is already in <see cref="FromVersion"/> shape.
    /// </summary>
    public interface IPlayerSaveMigration
    {
        int FromVersion { get; }
        void Apply(PlayerSaveNode data);
    }

    /// <summary>
    /// Brings a loaded save up to the current schema before the game reads it. On load, if the stored version
    /// is older than current, it applies each migration in ascending version order until the save reaches the
    /// current shape, so the game never receives a stale-shaped document. A stored version NEWER than current
    /// (a downgrade — an older client opened a save written by a newer build) is refused with
    /// <see cref="PlayerSaveVersionException"/> rather than silently mangled, and a missing intermediate step
    /// is a configuration bug that also throws — both are surfaced, not swallowed.
    /// </summary>
    public sealed class PlayerSaveMigrator
    {
        readonly Dictionary<int, IPlayerSaveMigration> _byFromVersion;

        public int CurrentVersion { get; }

        public PlayerSaveMigrator(int currentVersion, IEnumerable<IPlayerSaveMigration> migrations)
        {
            CurrentVersion = currentVersion;
            _byFromVersion = new Dictionary<int, IPlayerSaveMigration>();
            foreach (IPlayerSaveMigration migration in migrations)
            {
                _byFromVersion.Add(migration.FromVersion, migration);
            }
        }

        public static PlayerSaveMigrator None(int currentVersion) =>
            new PlayerSaveMigrator(currentVersion, Array.Empty<IPlayerSaveMigration>());

        /// <summary>
        /// Upgrade <paramref name="save"/> in place to <see cref="CurrentVersion"/> and return it. Already-current
        /// saves pass straight through. The same instance is returned so callers can chain.
        /// </summary>
        public PlayerSave MigrateToCurrent(PlayerSave save)
        {
            if (save.SchemaVersion > CurrentVersion)
            {
                throw new PlayerSaveVersionException(
                    $"Stored save schema {save.SchemaVersion} is newer than supported schema {CurrentVersion}; refusing to downgrade.");
            }

            while (save.SchemaVersion < CurrentVersion)
            {
                if (!_byFromVersion.TryGetValue(save.SchemaVersion, out IPlayerSaveMigration migration))
                {
                    throw new PlayerSaveVersionException(
                        $"No migration registered from schema {save.SchemaVersion}; cannot reach {CurrentVersion}.");
                }
                migration.Apply(save.Data);
                save.SchemaVersion++;
            }

            return save;
        }
    }

    /// <summary>Raised when a save cannot be brought to the current schema — a downgrade attempt or a gap in the migration chain. Surfaced to the caller, never swallowed.</summary>
    public sealed class PlayerSaveVersionException : Exception
    {
        public PlayerSaveVersionException(string message) : base(message) { }
    }
}
