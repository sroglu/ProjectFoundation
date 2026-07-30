using System;
using System.Globalization;
using System.Text;

namespace PFound.Utilities.CryptoTools
{
    /// <summary>
    /// Lower-level conversions between raw byte buffers and their lowercase hexadecimal
    /// string representation. Kept internal to the module so the crypto helpers can share
    /// one implementation.
    /// </summary>
    internal static class HexEncoding
    {
        private const string Digits = "0123456789abcdef";

        /// <summary>Renders <paramref name="bytes"/> as a contiguous lowercase hex string.</summary>
        public static string ToHex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                builder.Append(Digits[value >> 4]);
                builder.Append(Digits[value & 0x0F]);
            }
            return builder.ToString();
        }

        /// <summary>Parses a hex string (with an even number of digits) back into a byte buffer.</summary>
        public static byte[] FromHex(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            if ((hex.Length & 1) != 0)
                throw new FormatException("A hexadecimal string must contain an even number of digits.");

            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }
            return result;
        }
    }
}
