using System;
using System.IO;

namespace PFound.Utilities.StreamTools
{
    /// <summary>
    /// Helpers for working with arbitrary <see cref="Stream"/> instances.
    /// </summary>
    public static class StreamComparison
    {
        private const int DefaultChunkSize = 8192;

        /// <summary>
        /// Reads both streams to their end and reports whether their remaining
        /// contents are byte-for-byte identical. Reads in fixed-size chunks so
        /// arbitrarily large streams can be compared without buffering them fully.
        /// Both streams are consumed from their current position.
        /// </summary>
        public static bool ContentsEqual(Stream first, Stream second, int chunkSize = DefaultChunkSize)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));

            if (ReferenceEquals(first, second))
                return true;

            // Fast path: if both expose a known length and they differ, bail out.
            if (first.CanSeek && second.CanSeek && first.Length - first.Position != second.Length - second.Position)
                return false;

            byte[] bufferA = new byte[chunkSize];
            byte[] bufferB = new byte[chunkSize];

            while (true)
            {
                int filledA = ReadBlock(first, bufferA);
                int filledB = ReadBlock(second, bufferB);

                if (filledA != filledB)
                    return false;

                if (filledA == 0)
                    return true; // both hit end at the same offset

                for (int i = 0; i < filledA; i++)
                {
                    if (bufferA[i] != bufferB[i])
                        return false;
                }
            }
        }

        /// <summary>
        /// Attempts to configure the read timeout of a stream. Streams that do not
        /// support timeouts (<see cref="Stream.CanTimeout"/> is <c>false</c>)
        /// return <c>false</c> instead of throwing.
        /// </summary>
        public static bool TrySetReadTimeout(Stream stream, TimeSpan timeout)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanTimeout)
                return false;

            try
            {
                stream.ReadTimeout = (int)timeout.TotalMilliseconds;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        // Fills as much of the buffer as possible, honouring partial reads.
        private static int ReadBlock(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            return total;
        }
    }
}
