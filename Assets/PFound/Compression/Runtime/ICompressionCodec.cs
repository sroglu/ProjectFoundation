namespace PFound.Compression
{
    /// <summary>
    /// A byte[] round-trip compression codec. The framework depends on this interface, not on any
    /// concrete algorithm, so implementations are swappable: the built-in engine-free
    /// <see cref="LzmaCodec"/> is the zero-dependency default, but a consumer can register a
    /// different one — e.g. a provider that wraps the reference public-domain LZMA SDK (updated
    /// independently as a drop-in) or another algorithm (LZ4, Zstd, Deflate). Resolve codecs
    /// through <see cref="CompressionCodecs"/>.
    /// </summary>
    public interface ICompressionCodec
    {
        /// <summary>Stable identifier used to register/resolve this codec (e.g. "lzma").</summary>
        string Id { get; }

        /// <summary>Compresses <paramref name="data"/> into a self-describing blob.</summary>
        byte[] Compress(byte[] data);

        /// <summary>Inverts <see cref="Compress"/> — returns the original bytes.</summary>
        byte[] Decompress(byte[] data);
    }
}
