namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The small, FIXED set of first-class fields written alongside the blob so the backend can query player
    /// state without reading (or understanding) the blob itself: the install id (the row key), the schema
    /// version, and the game-segmentation fields. Every field here is DERIVED from the blob on write via
    /// <see cref="Derive"/> — there is deliberately no public constructor and no setter, so extracted fields
    /// can never be authored independently of the save and can never drift from it. The single source of
    /// truth is always the blob; these are a projection of it. Adding a future extracted field means reading
    /// one more value here and recomputing on the next write — existing blobs are untouched.
    /// </summary>
    public readonly struct ExtractedFields
    {
        public readonly string InstallId;
        public readonly int SchemaVersion;
        public readonly int Level;
        public readonly double TotalSpend;
        public readonly string BanStatus;
        public readonly long LastSeenUnixMs;

        ExtractedFields(string installId, int schemaVersion, PlayerSegmentationFields segmentation)
        {
            InstallId = installId;
            SchemaVersion = schemaVersion;
            Level = segmentation.Level;
            TotalSpend = segmentation.TotalSpend;
            BanStatus = segmentation.BanStatus;
            LastSeenUnixMs = segmentation.LastSeenUnixMs;
        }

        /// <summary>
        /// Recompute the extracted fields from a save. InstallId is the row key (supplied by the store call),
        /// SchemaVersion comes from the envelope, and the segmentation values come from the blob via the
        /// game's <paramref name="extractor"/>. This is the ONLY way to produce an <see cref="ExtractedFields"/>.
        /// </summary>
        public static ExtractedFields Derive(string installId, PlayerSave save, IPlayerFieldExtractor extractor)
        {
            return new ExtractedFields(installId, save.SchemaVersion, extractor.Read(save));
        }
    }
}
