namespace PFound.Compression
{
    /// <summary>
    /// Built-in, zero-dependency <see cref="ICompressionCodec"/> backed by the engine-free
    /// <see cref="Lzma"/> implementation (pooled zero-alloc streaming decoder, classic
    /// <c>.lzma</c> "alone" stream). This is the default codec registered in
    /// <see cref="CompressionCodecs"/>. To use a different LZMA (e.g. the reference SDK) or
    /// another algorithm, register a codec with a different <see cref="Id"/> — no consumer changes.
    /// </summary>
    public sealed class LzmaCodec : ICompressionCodec
    {
        public string Id => "lzma";

        public byte[] Compress(byte[] data) => Lzma.Compress(data);

        public byte[] Decompress(byte[] data) => Lzma.Decompress(data);
    }
}
