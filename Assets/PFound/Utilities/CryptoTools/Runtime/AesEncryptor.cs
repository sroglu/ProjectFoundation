using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PFound.Utilities.CryptoTools
{
    /// <summary>
    /// A small symmetric two-way encryptor built on AES in CBC mode with PKCS#7 padding.
    /// Constructed with a fixed key and initialisation vector, it exposes raw-byte
    /// encryption plus text convenience methods that carry the ciphertext as either a
    /// hexadecimal or a Base64 string. Plaintext strings are treated as UTF-8.
    /// </summary>
    public sealed class AesEncryptor
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        /// <summary>
        /// Creates an encryptor bound to <paramref name="key"/> (16, 24 or 32 bytes for
        /// AES-128/192/256) and a 16-byte <paramref name="iv"/>. Both buffers are copied.
        /// </summary>
        public AesEncryptor(byte[] key, byte[] iv)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (iv == null) throw new ArgumentNullException(nameof(iv));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 16, 24 or 32 bytes long.", nameof(key));
            if (iv.Length != 16)
                throw new ArgumentException("AES initialisation vector must be 16 bytes long.", nameof(iv));

            _key = (byte[])key.Clone();
            _iv = (byte[])iv.Clone();
        }

        // -- Raw bytes -------------------------------------------------------

        /// <summary>Encrypts <paramref name="plainBytes"/> and returns the raw ciphertext.</summary>
        public byte[] Encrypt(byte[] plainBytes)
        {
            if (plainBytes == null) throw new ArgumentNullException(nameof(plainBytes));
            using (var aes = CreateAlgorithm())
            using (ICryptoTransform transform = aes.CreateEncryptor())
                return transform.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        /// <summary>Decrypts raw <paramref name="cipherBytes"/> and returns the recovered plaintext bytes.</summary>
        public byte[] Decrypt(byte[] cipherBytes)
        {
            if (cipherBytes == null) throw new ArgumentNullException(nameof(cipherBytes));
            using (var aes = CreateAlgorithm())
            using (ICryptoTransform transform = aes.CreateDecryptor())
                return transform.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }

        // -- Hexadecimal text ------------------------------------------------

        /// <summary>Encrypts a UTF-8 string and returns the ciphertext as lowercase hexadecimal.</summary>
        public string EncryptToHex(string plainText)
        {
            return HexEncoding.ToHex(Encrypt(Encoding.UTF8.GetBytes(Require(plainText))));
        }

        /// <summary>Decrypts a hexadecimal ciphertext and returns the recovered UTF-8 string.</summary>
        public string DecryptFromHex(string cipherHex)
        {
            return Encoding.UTF8.GetString(Decrypt(HexEncoding.FromHex(Require(cipherHex))));
        }

        // -- Base64 text -----------------------------------------------------

        /// <summary>Encrypts a UTF-8 string and returns the ciphertext as Base64.</summary>
        public string EncryptToBase64(string plainText)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(Require(plainText))));
        }

        /// <summary>Decrypts a Base64 ciphertext and returns the recovered UTF-8 string.</summary>
        public string DecryptFromBase64(string cipherBase64)
        {
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(Require(cipherBase64))));
        }

        // -- Internals -------------------------------------------------------

        private Aes CreateAlgorithm()
        {
            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _key;
            aes.IV = _iv;
            return aes;
        }

        private static string Require(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value;
        }
    }
}
