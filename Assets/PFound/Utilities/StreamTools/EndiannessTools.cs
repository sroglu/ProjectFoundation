using System;

namespace PFound.Utilities.StreamTools
{
    /// <summary>
    /// Byte-order utilities: swap the endianness of the primitive integer and
    /// floating-point types, reverse raw byte spans, and produce a
    /// reversed-endian byte layout for a <see cref="Guid"/>.
    /// </summary>
    public static class EndiannessTools
    {
        public static short ReverseEndianness(short value)
        {
            return unchecked((short)ReverseEndianness((ushort)value));
        }

        public static ushort ReverseEndianness(ushort value)
        {
            return (ushort)((value >> 8) | (value << 8));
        }

        public static int ReverseEndianness(int value)
        {
            return unchecked((int)ReverseEndianness((uint)value));
        }

        public static uint ReverseEndianness(uint value)
        {
            return (value >> 24)
                 | ((value & 0x00FF0000u) >> 8)
                 | ((value & 0x0000FF00u) << 8)
                 | (value << 24);
        }

        public static long ReverseEndianness(long value)
        {
            return unchecked((long)ReverseEndianness((ulong)value));
        }

        public static ulong ReverseEndianness(ulong value)
        {
            ulong lo = ReverseEndianness((uint)(value & 0xFFFFFFFFul));
            ulong hi = ReverseEndianness((uint)(value >> 32));
            return (lo << 32) | hi;
        }

        /// <summary>Reverses the byte order of a 32-bit float.</summary>
        public static float ReverseEndianness(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>Reverses the byte order of a 64-bit float.</summary>
        public static double ReverseEndianness(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        /// <summary>
        /// Returns a new array with the bytes of <paramref name="bytes"/> in
        /// reverse order. The input is not modified.
        /// </summary>
        public static byte[] ReverseBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            byte[] copy = new byte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                copy[i] = bytes[bytes.Length - 1 - i];
            return copy;
        }

        /// <summary>
        /// Reverses a byte array in place and returns the same reference for
        /// convenient chaining.
        /// </summary>
        public static byte[] ReverseBytesInPlace(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Produces the byte layout of a <see cref="Guid"/> with the endianness
        /// of its three leading, endian-sensitive components flipped. This
        /// converts between the mixed-endian layout <see cref="Guid.ToByteArray"/>
        /// emits and a fully big-endian ("network order") representation.
        /// </summary>
        public static byte[] GuidToReversedEndianBytes(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();

            // First component: 4-byte integer.
            SwapRange(bytes, 0, 4);
            // Second component: 2-byte short.
            SwapRange(bytes, 4, 2);
            // Third component: 2-byte short.
            SwapRange(bytes, 6, 2);
            // Remaining 8 bytes are stored in order and are left untouched.

            return bytes;
        }

        private static void SwapRange(byte[] buffer, int offset, int length)
        {
            int i = offset;
            int j = offset + length - 1;
            while (i < j)
            {
                byte tmp = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = tmp;
                i++;
                j--;
            }
        }
    }
}
