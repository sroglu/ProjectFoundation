namespace PFound.Compression
{
    /// <summary>
    /// Typed selector for a built-in <see cref="ICompressionCodec"/>, a convenience alternative to the
    /// raw <see cref="ICompressionCodec.Id"/> string. Each value maps to a registered codec id via the
    /// enum overloads on <see cref="CompressionCodecs"/>. Grows one value per built-in codec; a codec
    /// registered only at runtime (with no enum value) is still reachable through the string overloads.
    /// </summary>
    public enum CompressionMethod
    {
        /// <summary>The built-in <see cref="LzmaCodec"/> (id "lzma").</summary>
        Lzma,

        /// <summary>The Deflate codec (id "deflate"), shipped by the <c>PFound.Compression.Deflate</c>
        /// provider assembly. Resolving it through <see cref="CompressionCodecs"/> throws until that
        /// provider has registered — which it does automatically at play.</summary>
        Deflate,
    }
}
