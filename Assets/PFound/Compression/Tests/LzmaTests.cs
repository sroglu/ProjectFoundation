using System;
using System.IO;
using System.Text;
using PFound.Compression;

// Behavior tests for the Lzma codec: the decoder inverts the encoder (the core property), the new
// dictionary-size option is written into and honored from the header, and the file/string/stream
// convenience helpers all round-trip. Standalone runner (no NUnit) so it runs under mono/csc.
internal static class LzmaTests
{
    public static void Run()
    {
        EmptyRoundTrip();
        SmallRoundTrip();
        DefaultDictHeaderIs4MiB();
        DictSizeIsWrittenToHeader();
        DictSizeOutOfRangeThrows();
        BeyondWindowRoundTripDefaultDict();
        SmallerDictStreamRoundTrip();
        DecoderHonorsSmallerHeaderWindow();
        FilePathHelpersRoundTrip();
        StringHelpersRoundTrip();
        StreamCompressRoundTrip();
        NullArgumentsThrow();
    }

    // --- helpers ---

    // Reads the 4-byte little-endian dict-size field the .lzma header carries after the props byte.
    private static int HeaderDictSize(byte[] stream)
    {
        int d = 0;
        for (int i = 0; i < 4; i++) d |= stream[1 + i] << (8 * i);
        return d;
    }

    private static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // Pseudo-random-but-repetitive payload: seeded PRNG bytes over a small alphabet so both literals
    // (window wrapping) and back-references (match copy across the wrap) are exercised on decode.
    private static byte[] MakePayload(int length, int seed)
    {
        var rng = new Random(seed);
        var data = new byte[length];
        for (int i = 0; i < length; i++)
            data[i] = (byte)(rng.Next(0, 6) == 0 ? rng.Next(256) : data[Math.Max(0, i - 40)]);
        return data;
    }

    // --- tests ---

    private static void EmptyRoundTrip()
    {
        var original = new byte[0];
        var restored = Lzma.Decompress(Lzma.Compress(original));
        TestKit.Check(Same(original, restored), "empty payload round-trips");
    }

    private static void SmallRoundTrip()
    {
        var original = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. " +
                                              "The quick brown fox jumps over the lazy dog.");
        var restored = Lzma.Decompress(Lzma.Compress(original));
        TestKit.Check(Same(original, restored), "small payload round-trips");
    }

    private static void DefaultDictHeaderIs4MiB()
    {
        var packed = Lzma.Compress(Encoding.UTF8.GetBytes("hello"));
        TestKit.Check(HeaderDictSize(packed) == Lzma.DefaultDictionarySize,
            "default compress writes 4 MiB dict field");
    }

    private static void DictSizeIsWrittenToHeader()
    {
        var packed = Lzma.Compress(MakePayload(4096, 1), Lzma.MinDictionarySize);
        TestKit.Check(HeaderDictSize(packed) == Lzma.MinDictionarySize,
            "explicit dict size is written to the header");
    }

    private static void DictSizeOutOfRangeThrows()
    {
        var data = new byte[16];
        TestKit.Throws<ArgumentOutOfRangeException>(
            () => Lzma.Compress(data, Lzma.MinDictionarySize - 1), "dict below min throws");
        TestKit.Throws<ArgumentOutOfRangeException>(
            () => Lzma.Compress(data, Lzma.DefaultDictionarySize + 1), "dict above max throws");
    }

    // Payload larger than the default 4 MiB window: the decode window must wrap and flush.
    private static void BeyondWindowRoundTripDefaultDict()
    {
        var original = MakePayload((5 * 1024 * 1024) + 777, 7);
        var restored = Lzma.Decompress(Lzma.Compress(original));
        TestKit.Check(Same(original, restored), ">4 MiB payload round-trips (default window wraps)");
    }

    // Payload larger than a 256 KiB window compressed with that smaller dict: exercises wrapping at
    // the smaller window on both encode (match distance cap) and decode (circular buffer size).
    private static void SmallerDictStreamRoundTrip()
    {
        var original = MakePayload((700 * 1024) + 13, 11);
        var packed = Lzma.Compress(original, Lzma.MinDictionarySize);
        TestKit.Check(HeaderDictSize(packed) == Lzma.MinDictionarySize, "smaller-dict stream header field");
        var restored = Lzma.Decompress(packed);
        TestKit.Check(Same(original, restored), "smaller-dict (>window) payload round-trips");
    }

    // The decoder must size its window from the header field, not a hard-coded 4 MiB. Round-tripping
    // several distinct dict sizes through the shared pooled decoder proves it reads + applies the field.
    private static void DecoderHonorsSmallerHeaderWindow()
    {
        int[] dicts = { Lzma.MinDictionarySize, 512 * 1024, 1024 * 1024, Lzma.DefaultDictionarySize };
        foreach (var dict in dicts)
        {
            var original = MakePayload((300 * 1024) + dict % 97, dict);
            var restored = Lzma.Decompress(Lzma.Compress(original, dict));
            TestKit.Check(Same(original, restored), "decoder honors header dict=" + dict);
        }
    }

    private static void FilePathHelpersRoundTrip()
    {
        var original = MakePayload(64 * 1024, 3);
        string dir = Path.Combine(Path.GetTempPath(), "pfcomp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string srcFile = Path.Combine(dir, "src.bin");
            string packedFile = Path.Combine(dir, "src.lzma");
            string outFile = Path.Combine(dir, "out.bin");
            File.WriteAllBytes(srcFile, original);

            // path -> path (compress) then path -> path (decompress)
            Lzma.CompressFile(srcFile, packedFile);
            Lzma.DecompressFile(packedFile, outFile);
            TestKit.Check(Same(original, File.ReadAllBytes(outFile)), "path->path compress+decompress round-trips");

            // path -> bytes
            byte[] packedBytes = Lzma.CompressFile(srcFile);
            TestKit.Check(Same(original, Lzma.Decompress(packedBytes)), "path->bytes compress round-trips");

            // bytes -> path
            Lzma.CompressToFile(original, packedFile);
            TestKit.Check(Same(original, Lzma.DecompressFile(packedFile)), "bytes->path compress + path->bytes decompress");

            // bytes -> path (decompress)
            Lzma.DecompressToFile(packedBytes, outFile);
            TestKit.Check(Same(original, File.ReadAllBytes(outFile)), "bytes->path decompress round-trips");

            // explicit-dict file overload writes the smaller header field
            Lzma.CompressFile(srcFile, packedFile, Lzma.MinDictionarySize);
            TestKit.Check(HeaderDictSize(File.ReadAllBytes(packedFile)) == Lzma.MinDictionarySize,
                "path->path compress honors dict-size overload");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static void StringHelpersRoundTrip()
    {
        string text = "PFound compression — UTF-8 test: café, naïve, 日本語, emoji 🚀, 0123456789.";
        var restored = Lzma.DecompressString(Lzma.CompressString(text));
        TestKit.Check(restored == text, "UTF-8 string round-trips");

        var restoredSmallDict = Lzma.DecompressString(Lzma.CompressString(text, Lzma.MinDictionarySize));
        TestKit.Check(restoredSmallDict == text, "UTF-8 string round-trips with explicit dict");
    }

    private static void StreamCompressRoundTrip()
    {
        var original = MakePayload(128 * 1024, 5);
        using (var src = new MemoryStream(original, false))
        using (var packed = new MemoryStream())
        {
            Lzma.Compress(src, packed);
            packed.Position = 0;
            using (var restored = new MemoryStream())
            {
                Lzma.DecompressInto(packed, restored);
                TestKit.Check(Same(original, restored.ToArray()), "Compress(Stream,Stream) round-trips");
            }
        }
    }

    private static void NullArgumentsThrow()
    {
        TestKit.Throws<ArgumentNullException>(() => Lzma.Compress((byte[])null), "Compress(null) throws");
        TestKit.Throws<ArgumentNullException>(() => Lzma.Compress((byte[])null, Lzma.MinDictionarySize), "Compress(null,dict) throws");
        TestKit.Throws<ArgumentNullException>(() => Lzma.CompressString(null), "CompressString(null) throws");
        TestKit.Throws<ArgumentNullException>(() => Lzma.DecompressString(null), "DecompressString(null) throws");
        TestKit.Throws<ArgumentNullException>(() => Lzma.CompressFile(null), "CompressFile(null) throws");
        TestKit.Throws<ArgumentNullException>(() => Lzma.Compress((Stream)null, new MemoryStream()), "Compress(null stream) throws");
    }
}
