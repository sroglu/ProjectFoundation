namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The set of game-segmentation values pulled out of a save's data tree for server-side querying. These
    /// are the fields an admin/LiveOps query filters on (spenders above a threshold, players past a level,
    /// banned accounts, recently active). They are always read FROM the blob, never authored beside it, so
    /// they cannot drift from the save they describe.
    /// </summary>
    public readonly struct PlayerSegmentationFields
    {
        public readonly int Level;
        public readonly double TotalSpend;
        public readonly string BanStatus;
        public readonly long LastSeenUnixMs;

        public PlayerSegmentationFields(int level, double totalSpend, string banStatus, long lastSeenUnixMs)
        {
            Level = level;
            TotalSpend = totalSpend;
            BanStatus = banStatus;
            LastSeenUnixMs = lastSeenUnixMs;
        }
    }

    /// <summary>
    /// Game-supplied knowledge of WHERE the segmentation values live inside the save's data tree. The set of
    /// extracted fields is fixed (level, total spend, ban status, last seen); only their location in the
    /// game's own document shape varies, so the game provides this reader. Keeping it an interface — rather
    /// than hard-coding paths in the engine — lets a game evolve its save shape without touching the sync
    /// core, and lets the extraction stay a pure function of the blob.
    /// </summary>
    public interface IPlayerFieldExtractor
    {
        PlayerSegmentationFields Read(PlayerSave save);

        /// <summary>
        /// Write the last-seen timestamp INTO the save's data tree at the same location <see cref="Read"/>
        /// reads it from. The engine calls this on each commit so last-seen lives in the blob (single source
        /// of truth) and is then extracted from it — read and write stay symmetric and game-located.
        /// </summary>
        void Stamp(PlayerSave save, long lastSeenUnixMs);
    }

    /// <summary>
    /// The default extractor: reads the segmentation values from canonical top-level keys in the data tree
    /// (<c>level</c>, <c>totalSpend</c>, <c>banStatus</c>, <c>lastSeen</c>). A game whose save keeps those
    /// values elsewhere supplies its own <see cref="IPlayerFieldExtractor"/> instead; the canonical reader
    /// is a sensible default, not a requirement.
    /// </summary>
    public sealed class CanonicalPlayerFieldExtractor : IPlayerFieldExtractor
    {
        public const string LevelKey = "level";
        public const string TotalSpendKey = "totalSpend";
        public const string BanStatusKey = "banStatus";
        public const string LastSeenKey = "lastSeen";

        public const string NotBanned = "none";

        public PlayerSegmentationFields Read(PlayerSave save)
        {
            PlayerSaveNode data = save.Data;
            return new PlayerSegmentationFields(
                data.GetInt(LevelKey, 0),
                data.GetDouble(TotalSpendKey, 0),
                data.GetString(BanStatusKey, NotBanned),
                data.GetLong(LastSeenKey, 0));
        }

        public void Stamp(PlayerSave save, long lastSeenUnixMs) => save.Data.SetLong(LastSeenKey, lastSeenUnixMs);
    }
}
