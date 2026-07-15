using System;
using System.Collections.Generic;

namespace PFound.Compression
{
    /// <summary>
    /// Registry of available <see cref="ICompressionCodec"/>s, keyed by <see cref="ICompressionCodec.Id"/>.
    /// Consumers (ContentDelivery, LocalizationService, …) resolve a codec here instead of calling a
    /// concrete class, so the implementation is swappable at runtime.
    ///
    /// <para>The built-in <see cref="LzmaCodec"/> is registered as the <see cref="Default"/>. A
    /// drop-in provider — for example a separate, define-gated asmdef that wraps the reference
    /// public-domain LZMA SDK, or an LZ4/Zstd implementation — simply calls
    /// <see cref="Register"/> at startup (and optionally <see cref="SetDefault"/>). Nothing else changes.</para>
    /// </summary>
    public static class CompressionCodecs
    {
        static readonly Dictionary<string, ICompressionCodec> s_byId =
            new Dictionary<string, ICompressionCodec>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The codec used when a consumer does not request a specific <see cref="ICompressionCodec.Id"/>.</summary>
        public static ICompressionCodec Default { get; private set; }

        static CompressionCodecs()
        {
            var lzma = new LzmaCodec();
            Register(lzma);
            Default = lzma;
        }

        /// <summary>Registers (or replaces) a codec under its <see cref="ICompressionCodec.Id"/>.</summary>
        public static void Register(ICompressionCodec codec)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            s_byId[codec.Id] = codec;
        }

        public static bool TryGet(string id, out ICompressionCodec codec) => s_byId.TryGetValue(id, out codec);

        /// <summary>Resolves a codec by id, or throws if none is registered under it.</summary>
        public static ICompressionCodec Get(string id)
        {
            if (s_byId.TryGetValue(id, out var codec)) return codec;
            throw new KeyNotFoundException("No compression codec registered with id '" + id + "'.");
        }

        /// <summary>Makes the codec registered under <paramref name="id"/> the new <see cref="Default"/>.</summary>
        public static void SetDefault(string id) => Default = Get(id);

        public static IEnumerable<string> RegisteredIds => s_byId.Keys;

        // ===================== TYPED (ENUM) OVERLOADS =====================
        // Convenience overloads that map a CompressionMethod to its codec id and reuse the string path,
        // so the typed and string APIs always resolve the same instance.

        /// <summary>Resolves the codec for <paramref name="method"/>, or throws if none is registered under its id.</summary>
        public static ICompressionCodec Get(CompressionMethod method) => Get(MethodToId(method));

        /// <summary>Resolves the codec for <paramref name="method"/>; false if none is registered under its id.</summary>
        public static bool TryGet(CompressionMethod method, out ICompressionCodec codec) => TryGet(MethodToId(method), out codec);

        /// <summary>Makes the codec for <paramref name="method"/> the new <see cref="Default"/>.</summary>
        public static void SetDefault(CompressionMethod method) => SetDefault(MethodToId(method));

        // Explicit method -> id map (a switch, not ToString(): no allocation, and renaming an enum
        // value can't silently break the id). Throws on an unmapped value so a new enum member without
        // a mapping fails loudly rather than resolving nothing.
        static string MethodToId(CompressionMethod method) => method switch
        {
            CompressionMethod.Lzma => "lzma",
            CompressionMethod.Deflate => "deflate",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "No codec id mapped for this CompressionMethod."),
        };
    }
}
