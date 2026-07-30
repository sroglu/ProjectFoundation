namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The unit that crosses the backend seam: the opaque <see cref="Blob"/> (stored as one document field /
    /// one <c>jsonb</c> column) plus the <see cref="Fields"/> extracted from it (stored as sibling
    /// first-class columns for querying). The two always travel together and the fields are always derived
    /// from this blob, so a stored record is internally consistent by construction.
    /// </summary>
    public readonly struct RemotePlayerRecord
    {
        public readonly string Blob;
        public readonly ExtractedFields Fields;

        public RemotePlayerRecord(string blob, ExtractedFields fields)
        {
            Blob = blob;
            Fields = fields;
        }
    }

    /// <summary>
    /// The result of a backend load: whether a stored save exists for the install, and if so its opaque
    /// blob. Only the blob comes back — the extracted fields are a WRITE-side projection that exists for the
    /// backend's own querying, not something the client reads (and a backend that does not parse the blob
    /// could not reconstruct them anyway). The blob alone is enough for the client, which re-derives the
    /// extracted fields on its next write. A first-ever launch yields <see cref="Missing"/> (no throw, no
    /// null) so the engine can fall back to a fresh default save — the one sanctioned "absence is normal" path.
    /// </summary>
    public readonly struct RemoteLoadOutcome
    {
        public readonly bool Found;
        public readonly string Blob;

        RemoteLoadOutcome(bool found, string blob)
        {
            Found = found;
            Blob = blob;
        }

        public static RemoteLoadOutcome Missing => new RemoteLoadOutcome(false, null);
        public static RemoteLoadOutcome Of(string blob) => new RemoteLoadOutcome(true, blob);
    }
}
