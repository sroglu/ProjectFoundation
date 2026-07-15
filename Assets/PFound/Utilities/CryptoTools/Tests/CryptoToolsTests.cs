using System;
using System.IO;
using System.Text;
using PFound.Utilities.CryptoTools;

namespace PFound.Utilities.CryptoTools.Tests
{
    /// <summary>
    /// Standalone Core test runner for the CryptoTools module. Following the project convention
    /// (engine-free modules verified without Unity), this compiles and runs under <c>csc</c>+<c>mono</c>
    /// via its <see cref="Main"/> entry point and reports pass/total. Unity ignores the entry point.
    /// </summary>
    internal static class CryptoToolsTests
    {
        private static int s_passed;
        private static int s_failed;

        private static void Check(bool condition, string name)
        {
            if (condition) s_passed++;
            else { s_failed++; Console.WriteLine("  FAIL: " + name); }
        }

        private static void CheckEqual(string expected, string actual, string name)
        {
            Check(string.Equals(expected, actual, StringComparison.Ordinal),
                name + " (expected '" + expected + "', got '" + actual + "')");
        }

        public static int Main()
        {
            HashKnownVectors();
            HashSourceParity();
            HashEqualityHelper();
            MurmurVectors();
            AesRoundTrips();
            AesValidation();

            Console.WriteLine("CryptoTools: " + s_passed + "/" + (s_passed + s_failed) + " passed.");
            return s_failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------

        private static void HashKnownVectors()
        {
            CheckEqual("d41d8cd98f00b204e9800998ecf8427e", Hashing.ToHexString(Hashing.Md5("")), "MD5 empty");
            CheckEqual("900150983cd24fb0d6963f7d28e17f72", Hashing.ToHexString(Hashing.Md5("abc")), "MD5 abc");
            CheckEqual("da39a3ee5e6b4b0d3255bfef95601890afd80709", Hashing.ToHexString(Hashing.Sha1("")), "SHA1 empty");
            CheckEqual("a9993e364706816aba3e25717850c26c9cd0d89d", Hashing.ToHexString(Hashing.Sha1("abc")), "SHA1 abc");
            CheckEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Hashing.ToHexString(Hashing.Sha256("")), "SHA256 empty");
            CheckEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hashing.ToHexString(Hashing.Sha256("abc")), "SHA256 abc");
        }

        private static void HashSourceParity()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("the quick brown fox");
            byte[] fromString = Hashing.Sha256("the quick brown fox");
            byte[] fromBytes = Hashing.Sha256(bytes);
            Check(Hashing.AreEqual(fromString, fromBytes), "SHA256 string/bytes parity");

            using (var stream = new MemoryStream(bytes))
            {
                byte[] fromStream = Hashing.Sha256(stream);
                Check(Hashing.AreEqual(fromString, fromStream), "SHA256 stream parity");
            }

            string path = Path.Combine(Path.GetTempPath(), "pf_cryptotools_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                File.WriteAllBytes(path, bytes);
                byte[] fromFile = Hashing.Sha256File(path);
                Check(Hashing.AreEqual(fromString, fromFile), "SHA256 file parity");

                byte[] md5File = Hashing.Md5File(path);
                Check(Hashing.AreEqual(Hashing.Md5(bytes), md5File), "MD5 file parity");
                byte[] sha1File = Hashing.Sha1File(path);
                Check(Hashing.AreEqual(Hashing.Sha1(bytes), sha1File), "SHA1 file parity");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void HashEqualityHelper()
        {
            Check(Hashing.AreEqual(Hashing.Md5("x"), Hashing.Md5("x")), "AreEqual identical");
            Check(!Hashing.AreEqual(Hashing.Md5("x"), Hashing.Md5("y")), "AreEqual different content");
            Check(!Hashing.AreEqual(Hashing.Md5("x"), Hashing.Sha1("x")), "AreEqual different length");
            Check(!Hashing.AreEqual(null, Hashing.Md5("x")), "AreEqual null lhs");
            Check(!Hashing.AreEqual(Hashing.Md5("x"), null), "AreEqual null rhs");
        }

        private static void MurmurVectors()
        {
            // Derivable vector: empty input with seed 0 avalanches 0 to 0.
            Check(MurmurHash2.Hash32(Array.Empty<byte>(), 0) == 0u, "Murmur empty/seed0 == 0");

            uint a = MurmurHash2.Hash32("hello");
            uint b = MurmurHash2.Hash32("hello");
            Check(a == b, "Murmur deterministic");

            Check(MurmurHash2.Hash32("hello") != MurmurHash2.Hash32("hellp"), "Murmur input sensitivity");
            Check(MurmurHash2.Hash32("hello", 0) != MurmurHash2.Hash32("hello", 1), "Murmur seed sensitivity");

            // Length-boundary cases (tail of 0..3 bytes) should all be well-defined.
            Check(MurmurHash2.Hash32("a") != MurmurHash2.Hash32("ab"), "Murmur tail 1 vs 2");
            Check(MurmurHash2.Hash32("abc") != MurmurHash2.Hash32("abcd"), "Murmur tail 3 vs 0");

            Check(MurmurHash2.Hash32(Encoding.UTF8.GetBytes("hello")) == MurmurHash2.Hash32("hello"), "Murmur string/bytes parity");
        }

        private static void AesRoundTrips()
        {
            byte[] key = new byte[32];
            byte[] iv = new byte[16];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(i * 7 + 1);
            for (int i = 0; i < iv.Length; i++) iv[i] = (byte)(i * 3 + 5);

            var encryptor = new AesEncryptor(key, iv);
            const string message = "Attack at dawn — 12:00. Rendezvous çöğ.";

            CheckEqual(message, encryptor.DecryptFromHex(encryptor.EncryptToHex(message)), "AES hex round-trip");
            CheckEqual(message, encryptor.DecryptFromBase64(encryptor.EncryptToBase64(message)), "AES base64 round-trip");

            byte[] plain = Encoding.UTF8.GetBytes(message);
            byte[] cipher = encryptor.Encrypt(plain);
            Check(!Hashing.AreEqual(plain, cipher), "AES ciphertext differs from plaintext");
            Check(Hashing.AreEqual(plain, encryptor.Decrypt(cipher)), "AES raw round-trip");

            // A second encryptor with the same key/iv can decrypt the first one's output.
            var twin = new AesEncryptor((byte[])key.Clone(), (byte[])iv.Clone());
            CheckEqual(message, twin.DecryptFromBase64(encryptor.EncryptToBase64(message)), "AES cross-instance parity");

            // AES-128 with a 16-byte key also works.
            byte[] key128 = new byte[16];
            for (int i = 0; i < key128.Length; i++) key128[i] = (byte)(255 - i);
            var aes128 = new AesEncryptor(key128, iv);
            CheckEqual("short", aes128.DecryptFromHex(aes128.EncryptToHex("short")), "AES-128 round-trip");
        }

        private static void AesValidation()
        {
            byte[] iv = new byte[16];
            Check(Throws(() => new AesEncryptor(new byte[8], iv)), "AES rejects bad key length");
            Check(Throws(() => new AesEncryptor(new byte[32], new byte[8])), "AES rejects bad iv length");
            Check(Throws(() => new AesEncryptor(null, iv)), "AES rejects null key");
        }

        private static bool Throws(Action action)
        {
            try { action(); return false; }
            catch { return true; }
        }
    }
}
