using System;
using System.Text;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The connection-origin facts recovered from a HAProxy PROXY-protocol preamble:
    /// who the load-balancer says really dialed us, and how many leading bytes the
    /// preamble occupied (so the caller can skip past it to the application stream).
    /// <see cref="HasSource"/> is false for LOCAL/UNKNOWN preambles that carry no
    /// address yet still consume header bytes.
    /// </summary>
    public readonly struct ProxyOrigin
    {
        public readonly bool HasSource;
        public readonly string SourceAddress;
        public readonly int SourcePort;
        public readonly string DestinationAddress;
        public readonly int DestinationPort;
        /// <summary>Bytes the preamble occupied; the app stream begins at this offset.</summary>
        public readonly int HeaderBytes;

        public ProxyOrigin(bool hasSource, string src, int srcPort, string dst, int dstPort, int headerBytes)
        {
            HasSource = hasSource;
            SourceAddress = src;
            SourcePort = srcPort;
            DestinationAddress = dst;
            DestinationPort = dstPort;
            HeaderBytes = headerBytes;
        }
    }

    /// <summary>
    /// Reads the HAProxy PROXY protocol preamble (versions 1 and 2) to recover the
    /// address of the client that actually connected when a proxy or load-balancer
    /// sits in front of the listener. Implemented from the public protocol
    /// description; both encodings are handled:
    ///
    ///   v1 — a single CRLF-terminated ASCII line, e.g.
    ///        "PROXY TCP4 1.2.3.4 5.6.7.8 4000 443\r\n"  (also TCP6 / UNKNOWN),
    ///        capped at 107 bytes including the CRLF.
    ///   v2 — a 12-byte signature (0D 0A 0D 0A 00 0D 0A 51 55 49 54 0A) then a
    ///        version+command byte, an address-family+protocol byte, a big-endian
    ///        u16 address-block length, and the address block itself.
    ///
    /// Opt-in per deployment: a bare listener never sees these bytes, so parsing runs
    /// only when the server config asks for it. Pure and engine-agnostic so it is
    /// verifiable without any socket.
    /// </summary>
    public static class ProxyProtocolReader
    {
        // v2 fixed signature; a v1 line always begins with the ASCII "PROXY ".
        static readonly byte[] V2Signature =
            { 0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A };

        const int V1MaxLine = 107; // per spec: worst-case "PROXY TCP6 ...\r\n"

        /// <summary>
        /// Attempt to read a PROXY preamble from the front of <paramref name="data"/>.
        /// Returns false (and does not throw) when the bytes are not a PROXY preamble,
        /// so a mis-flagged stream degrades to "no origin recovered" rather than a
        /// dropped connection.
        /// </summary>
        public static bool TryRead(ArraySegment<byte> data, out ProxyOrigin origin)
        {
            origin = default;
            byte[] buf = data.Array;
            int start = data.Offset;
            int len = data.Count;

            if (StartsWith(buf, start, len, V2Signature))
                return TryReadV2(buf, start, len, out origin);
            if (len >= 6 && buf[start] == (byte)'P' && buf[start + 1] == (byte)'R' &&
                buf[start + 2] == (byte)'O' && buf[start + 3] == (byte)'X' &&
                buf[start + 4] == (byte)'Y' && buf[start + 5] == (byte)' ')
                return TryReadV1(buf, start, len, out origin);

            return false;
        }

        // --- v1: CRLF-terminated ASCII line ---------------------------------

        static bool TryReadV1(byte[] buf, int start, int len, out ProxyOrigin origin)
        {
            origin = default;
            int scan = Math.Min(len, V1MaxLine);
            int crlf = -1;
            for (int i = 1; i < scan; i++)
            {
                if (buf[start + i - 1] == (byte)'\r' && buf[start + i] == (byte)'\n')
                {
                    crlf = i - 1;
                    break;
                }
            }
            if (crlf < 0)
                return false; // no terminator inside the byte budget

            int headerBytes = crlf + 2; // include the CRLF
            string line = Encoding.ASCII.GetString(buf, start, crlf);
            string[] parts = line.Split(' ');
            // "PROXY UNKNOWN..." carries no usable address but still occupies bytes.
            if (parts.Length < 2 || parts[1] == "UNKNOWN")
            {
                origin = new ProxyOrigin(false, null, 0, null, 0, headerBytes);
                return true;
            }
            if ((parts[1] != "TCP4" && parts[1] != "TCP6") || parts.Length < 6)
                return false;

            origin = new ProxyOrigin(
                true, parts[2], ParsePort(parts[4]), parts[3], ParsePort(parts[5]), headerBytes);
            return true;
        }

        static int ParsePort(string s)
        {
            int p;
            return int.TryParse(s, out p) ? p : 0;
        }

        // --- v2: binary ------------------------------------------------------

        static bool TryReadV2(byte[] buf, int start, int len, out ProxyOrigin origin)
        {
            origin = default;
            const int fixedHeader = 16; // 12 sig + verCmd + famProto + u16 length
            if (len < fixedHeader)
                return false;

            byte verCmd = buf[start + 12];
            byte famProto = buf[start + 13];
            int addrLen = (buf[start + 14] << 8) | buf[start + 15];
            int headerBytes = fixedHeader + addrLen;
            if (len < headerBytes)
                return false;

            int command = verCmd & 0x0F; // 0 = LOCAL (health check), 1 = PROXY
            int family = (famProto & 0xF0) >> 4; // 1 = AF_INET, 2 = AF_INET6

            if (command == 0)
            {
                // LOCAL: the proxy's own health check, no forwarded address.
                origin = new ProxyOrigin(false, null, 0, null, 0, headerBytes);
                return true;
            }

            int at = start + fixedHeader;
            if (family == 1 && addrLen >= 12) // IPv4: 4 + 4 + 2 + 2
            {
                string src = Ipv4(buf, at);
                string dst = Ipv4(buf, at + 4);
                int srcPort = (buf[at + 8] << 8) | buf[at + 9];
                int dstPort = (buf[at + 10] << 8) | buf[at + 11];
                origin = new ProxyOrigin(true, src, srcPort, dst, dstPort, headerBytes);
                return true;
            }
            if (family == 2 && addrLen >= 36) // IPv6: 16 + 16 + 2 + 2
            {
                string src = Ipv6(buf, at);
                string dst = Ipv6(buf, at + 16);
                int srcPort = (buf[at + 32] << 8) | buf[at + 33];
                int dstPort = (buf[at + 34] << 8) | buf[at + 35];
                origin = new ProxyOrigin(true, src, srcPort, dst, dstPort, headerBytes);
                return true;
            }

            // Unsupported family (e.g. AF_UNIX): consume the bytes, no address.
            origin = new ProxyOrigin(false, null, 0, null, 0, headerBytes);
            return true;
        }

        static string Ipv4(byte[] b, int at)
            => b[at] + "." + b[at + 1] + "." + b[at + 2] + "." + b[at + 3];

        static string Ipv6(byte[] b, int at)
        {
            var sb = new StringBuilder(39);
            for (int g = 0; g < 8; g++)
            {
                if (g > 0) sb.Append(':');
                int word = (b[at + g * 2] << 8) | b[at + g * 2 + 1];
                sb.Append(word.ToString("x"));
            }
            return sb.ToString();
        }

        static bool StartsWith(byte[] buf, int start, int len, byte[] sig)
        {
            if (len < sig.Length) return false;
            for (int i = 0; i < sig.Length; i++)
                if (buf[start + i] != sig[i]) return false;
            return true;
        }
    }
}
