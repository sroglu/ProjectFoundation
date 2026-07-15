using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PFound.Utilities.CryptoTools
{
    /// <summary>
    /// Cryptographic digest helpers (MD5, SHA-1, SHA-256) over byte buffers, strings,
    /// streams and files, plus a length-safe helper for comparing two digests.
    /// String overloads encode the text as UTF-8 before hashing.
    /// </summary>
    public static class Hashing
    {
        // -- MD5 -------------------------------------------------------------

        public static byte[] Md5(byte[] data) => Compute(MD5.Create(), data);
        public static byte[] Md5(string text) => Compute(MD5.Create(), text);
        public static byte[] Md5(Stream stream) => Compute(MD5.Create(), stream);
        public static byte[] Md5File(string path) => ComputeFile(MD5.Create(), path);

        // -- SHA-1 -----------------------------------------------------------

        public static byte[] Sha1(byte[] data) => Compute(SHA1.Create(), data);
        public static byte[] Sha1(string text) => Compute(SHA1.Create(), text);
        public static byte[] Sha1(Stream stream) => Compute(SHA1.Create(), stream);
        public static byte[] Sha1File(string path) => ComputeFile(SHA1.Create(), path);

        // -- SHA-256 ---------------------------------------------------------

        public static byte[] Sha256(byte[] data) => Compute(SHA256.Create(), data);
        public static byte[] Sha256(string text) => Compute(SHA256.Create(), text);
        public static byte[] Sha256(Stream stream) => Compute(SHA256.Create(), stream);
        public static byte[] Sha256File(string path) => ComputeFile(SHA256.Create(), path);

        // -- Formatting & comparison ----------------------------------------

        /// <summary>Renders a digest as a lowercase hexadecimal string.</summary>
        public static string ToHexString(byte[] hash) => HexEncoding.ToHex(hash);

        /// <summary>
        /// Compares two digests for equality in constant time relative to their length,
        /// so the comparison does not leak how many leading bytes matched. A <c>null</c>
        /// argument or a length mismatch yields <see langword="false"/>.
        /// </summary>
        public static bool AreEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < a.Length; i++)
                difference |= a[i] ^ b[i];
            return difference == 0;
        }

        // -- Shared plumbing -------------------------------------------------

        private static byte[] Compute(HashAlgorithm algorithm, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (algorithm)
                return algorithm.ComputeHash(data);
        }

        private static byte[] Compute(HashAlgorithm algorithm, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Compute(algorithm, Encoding.UTF8.GetBytes(text));
        }

        private static byte[] Compute(HashAlgorithm algorithm, Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (algorithm)
                return algorithm.ComputeHash(stream);
        }

        private static byte[] ComputeFile(HashAlgorithm algorithm, string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using (var stream = File.OpenRead(path))
                return Compute(algorithm, stream);
        }
    }
}
