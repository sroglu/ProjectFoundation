namespace PFound.LocalizationService.Unity
{
    /// <summary>
    /// The pair of content-address hashes that name a deployed localization table set: one for the
    /// content (text) table and one for the definitions table. Both are lifted from the hash-suffixed
    /// file names (e.g. <c>LocalizationText-&lt;hash&gt;.lzma</c>). <see cref="IsValid"/> is set only when a
    /// full pair was resolved; a <c>default</c> value (see <see cref="Invalid"/>) reports
    /// <see cref="IsValid"/> == <c>false</c>.
    /// </summary>
    public readonly struct LocalizationHashData
    {
        /// <summary>Hash segment taken from the content (text) table file name.</summary>
        public readonly string ContentHash;

        /// <summary>Hash segment taken from the definitions table file name.</summary>
        public readonly string DefinitionsHash;

        /// <summary>
        /// True when this value was produced from a resolved content+definitions pair. Stored explicitly
        /// at construction — it is not inferred from the hash fields.
        /// </summary>
        public readonly bool IsValid;

        public LocalizationHashData(string contentHash, string definitionsHash)
        {
            ContentHash = contentHash;
            DefinitionsHash = definitionsHash;
            IsValid = true;
        }

        /// <summary>The "not resolved" value: <see cref="IsValid"/> is <c>false</c> and both hashes are null.</summary>
        public static readonly LocalizationHashData Invalid = default;
    }
}
