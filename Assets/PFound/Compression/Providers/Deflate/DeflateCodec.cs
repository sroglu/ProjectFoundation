using System;
using System.IO;
using System.IO.Compression;
using PFound.Compression;

namespace PFound.Compression.Deflate
{
    /// <summary>
    /// Drop-in <see cref="ICompressionCodec"/> (id "deflate") backed by the BCL
    /// <see cref="DeflateStream"/> (RFC 1951). Zero external dependency — pure
    /// <c>System.IO.Compression</c> plus the core module. Ships in its own referencing assembly and
    /// self-registers (see <see cref="DeflateCodecInstaller"/>), demonstrating that a codec provider
    /// plugs in without touching <see cref="CompressionCodecs"/> or any consumer.
    /// <para><see cref="Decompress"/> inverts <see cref="Compress"/> from this same codec; that
    /// byte-exact round trip is the only guarantee.</para>
    /// </summary>
    public sealed class DeflateCodec : ICompressionCodec
    {
        public string Id => "deflate";

        /// <summary>Compresses <paramref name="data"/> into a raw DEFLATE blob.</summary>
        public byte[] Compress(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using (var output = new MemoryStream())
            {
                // Leave the underlying stream open so ToArray() is valid after the DeflateStream,
                // whose Dispose flushes the final block, has been closed.
                using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
                {
                    deflate.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>Inverts <see cref="Compress"/> — returns the original bytes.</summary>
        public byte[] Decompress(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using (var source = new MemoryStream(data, writable: false))
            using (var deflate = new DeflateStream(source, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
