using System;
using System.Text;

namespace PFound.Utilities.CryptoTools
{
    /// <summary>
    /// MurmurHash2, the fast non-cryptographic 32-bit hash by Austin Appleby (public domain).
    /// Suitable for hash tables, bucketing and checksums — <em>not</em> for security. String
    /// overloads hash the UTF-8 bytes of the text.
    /// </summary>
    public static class MurmurHash2
    {
        // Mixing constants specified by the algorithm.
        private const uint Multiplier = 0x5bd1e995;
        private const int Rotation = 24;

        public static uint Hash32(string text, uint seed = 0)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Hash32(Encoding.UTF8.GetBytes(text), seed);
        }

        public static uint Hash32(byte[] data, uint seed = 0)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int length = data.Length;
            unchecked
            {
                uint hash = seed ^ (uint)length;
                int index = 0;

                // Consume the input four bytes at a time.
                while (length >= 4)
                {
                    uint block = (uint)(data[index]
                        | (data[index + 1] << 8)
                        | (data[index + 2] << 16)
                        | (data[index + 3] << 24));

                    block *= Multiplier;
                    block ^= block >> Rotation;
                    block *= Multiplier;

                    hash *= Multiplier;
                    hash ^= block;

                    index += 4;
                    length -= 4;
                }

                // Fold in the trailing 1-3 bytes.
                switch (length)
                {
                    case 3:
                        hash ^= (uint)(data[index + 2] << 16);
                        goto case 2;
                    case 2:
                        hash ^= (uint)(data[index + 1] << 8);
                        goto case 1;
                    case 1:
                        hash ^= data[index];
                        hash *= Multiplier;
                        break;
                }

                // Final avalanche so every input bit affects the output.
                hash ^= hash >> 13;
                hash *= Multiplier;
                hash ^= hash >> 15;
                return hash;
            }
        }
    }
}
