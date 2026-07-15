using System;
using System.Text;

namespace PFound.Compression
{
    /// <summary>
    /// Codec-agnostic UTF-8 string convenience over any <see cref="ICompressionCodec"/>. Lets a
    /// "compressed byte[] &lt;-&gt; UTF-8 string" flow (e.g. a compressed text field decompressed to a
    /// string) run through whichever codec a consumer resolved from <see cref="CompressionCodecs"/>,
    /// not only the static <see cref="Lzma"/> helper.
    /// </summary>
    public static class CompressionCodecExtensions
    {
        /// <summary>UTF-8 encodes <paramref name="text"/>, then compresses the bytes with <paramref name="codec"/>.</summary>
        public static byte[] CompressString(this ICompressionCodec codec, string text)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            if (text == null) throw new ArgumentNullException(nameof(text));
            return codec.Compress(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Decompresses <paramref name="data"/> with <paramref name="codec"/>, then decodes the result as a UTF-8 string.</summary>
        public static string DecompressString(this ICompressionCodec codec, byte[] data)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            if (data == null) throw new ArgumentNullException(nameof(data));
            return Encoding.UTF8.GetString(codec.Decompress(data));
        }
    }
}
