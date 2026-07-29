namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The backend-neutral player save the game owns and mutates. It is an envelope around the game's data
    /// tree (<see cref="Data"/>) plus two engine-managed fields: <see cref="SchemaVersion"/> (the shape the
    /// data is currently in, used by the migration pipeline) and <see cref="SaveCounter"/> (a per-save
    /// monotonic counter that is the tie-breaker for conflict resolution — the higher counter is the more
    /// recent write). The game reads and writes its own domain data through <see cref="Data"/>; it never
    /// sets the counter directly (the sync engine bumps it on each committed save), which is what keeps
    /// last-write-wins meaningful across devices and reinstalls.
    /// </summary>
    public sealed class PlayerSave
    {
        public int SchemaVersion { get; set; }

        /// <summary>Monotonic per-save counter. The engine increments it on each commit; conflict resolution compares it.</summary>
        public long SaveCounter { get; set; }

        /// <summary>The game's own domain data as a backend-neutral tree. This is the bulk of the blob.</summary>
        public PlayerSaveNode Data { get; }

        public PlayerSave(int schemaVersion, long saveCounter, PlayerSaveNode data)
        {
            SchemaVersion = schemaVersion;
            SaveCounter = saveCounter;
            Data = data;
        }

        /// <summary>A brand-new save at the current schema with an empty data tree and a zero counter. This is the "no save exists yet" fallback — the one allowed default, not a bug-hiding guard.</summary>
        public static PlayerSave CreateDefault(int schemaVersion) => new PlayerSave(schemaVersion, 0, PlayerSaveNode.NewObject());
    }
}
