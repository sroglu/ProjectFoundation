using System.Threading;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Hands out the per-call correlation tokens that pair a reply with its
    /// request. Monotonic and wrap-safe; token 0 is skipped so a zeroed field is
    /// never mistaken for a live call. Thread-safe because a transport may mint
    /// tokens off the send path.
    /// </summary>
    public sealed class CallTokenSource
    {
        int _cursor;

        public uint Next()
        {
            uint token;
            do
            {
                token = unchecked((uint)Interlocked.Increment(ref _cursor));
            }
            while (token == 0);
            return token;
        }
    }
}
