namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The bridge between an in-memory <see cref="PlayerSave"/> and a stored <see cref="RemotePlayerRecord"/>.
    /// Packing serializes the save to the blob and derives the extracted fields from that same save in one
    /// step, which is what structurally guarantees the extracted fields match the blob (they are computed
    /// together, from one source). Unpacking parses the blob and migrates it to the current schema, so a
    /// record loaded from any backend is handed on already in the shape the game expects.
    /// </summary>
    public sealed class PlayerSavePacker
    {
        readonly IPlayerFieldExtractor _extractor;
        readonly PlayerSaveMigrator _migrator;

        public PlayerSavePacker(IPlayerFieldExtractor extractor, PlayerSaveMigrator migrator)
        {
            _extractor = extractor;
            _migrator = migrator;
        }

        /// <summary>Serialize the save to a blob and derive the extracted fields from it, keyed by install id.</summary>
        public RemotePlayerRecord Pack(string installId, PlayerSave save)
        {
            string blob = PlayerSaveCodec.Serialize(save);
            ExtractedFields fields = ExtractedFields.Derive(installId, save, _extractor);
            return new RemotePlayerRecord(blob, fields);
        }

        /// <summary>Parse a stored blob and migrate it to the current schema before the game sees it (used for both the backend load and the durable local copy).</summary>
        public PlayerSave UnpackBlob(string blob)
        {
            PlayerSave save = PlayerSaveCodec.Deserialize(blob);
            return _migrator.MigrateToCurrent(save);
        }
    }
}
