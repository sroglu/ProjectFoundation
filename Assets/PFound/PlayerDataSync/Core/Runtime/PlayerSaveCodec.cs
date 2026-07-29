namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// Turns a <see cref="PlayerSave"/> into the single opaque blob string that a backend stores verbatim,
    /// and back. The blob is one self-contained JSON document — envelope fields (<c>schemaVersion</c>,
    /// <c>saveCounter</c>) plus the game's data under <c>data</c> — with no backend-specific types, so the
    /// exact same string is valid for any provider (that is what makes a backend swap a pure blob copy).
    /// Deserialization is deliberately structural only: it reconstructs the envelope + data tree but does
    /// NOT migrate — migration is a separate, explicit step so the caller controls when a stale-shaped save
    /// is upgraded before the game sees it.
    /// </summary>
    public static class PlayerSaveCodec
    {
        public const string SchemaVersionKey = "schemaVersion";
        public const string SaveCounterKey = "saveCounter";
        public const string DataKey = "data";

        public static string Serialize(PlayerSave save)
        {
            PlayerSaveNode envelope = PlayerSaveNode.NewObject();
            envelope.SetInt(SchemaVersionKey, save.SchemaVersion);
            envelope.SetLong(SaveCounterKey, save.SaveCounter);
            envelope.Set(DataKey, save.Data);
            return envelope.ToJson();
        }

        /// <summary>
        /// Parse a blob back into a <see cref="PlayerSave"/> without migrating. Throws
        /// <see cref="PlayerSaveFormatException"/> on malformed input; the persistence boundary translates
        /// that into keeping the last good local copy rather than crashing.
        /// </summary>
        public static PlayerSave Deserialize(string blob)
        {
            PlayerSaveNode envelope = PlayerSaveNode.Parse(blob);
            if (!envelope.IsObject) throw new PlayerSaveFormatException("Save blob root is not an object.");

            int schemaVersion = envelope.GetInt(SchemaVersionKey, 0);
            long saveCounter = envelope.GetLong(SaveCounterKey, 0);
            PlayerSaveNode data = envelope.TryGet(DataKey, out PlayerSaveNode dataNode) && dataNode.IsObject
                ? dataNode
                : PlayerSaveNode.NewObject();

            return new PlayerSave(schemaVersion, saveCounter, data);
        }
    }
}
