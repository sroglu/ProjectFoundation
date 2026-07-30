using System;
using System.Net;
using System.Net.Sockets;

namespace PFound.Utilities.NetworkTools
{
    /// <summary>
    /// Address-arithmetic and classification helpers for <see cref="IPAddress"/>.
    /// All members operate on the raw address bytes so they work for both
    /// IPv4 and (where meaningful) IPv6 without touching the network.
    /// </summary>
    public static class IPAddressExtensions
    {
        /// <summary>
        /// Returns <c>true</c> when the address is <c>null</c> or every byte of it
        /// is zero (for example <c>0.0.0.0</c> / <c>IPAddress.Any</c> / <c>::</c>).
        /// </summary>
        public static bool IsNullOrZero(this IPAddress address)
        {
            if (address == null)
                return true;

            byte[] bytes = address.GetAddressBytes();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determines whether the address belongs to a private / local scope:
        /// loopback, IPv4 RFC 1918 blocks (10/8, 172.16/12, 192.168/16),
        /// the 169.254/16 link-local block, or IPv6 unique-local (fc00::/7) /
        /// link-local (fe80::/10) ranges.
        /// </summary>
        public static bool IsPrivate(this IPAddress address)
        {
            if (address == null)
                return false;

            if (IPAddress.IsLoopback(address))
                return true;

            byte[] b = address.GetAddressBytes();

            if (address.AddressFamily == AddressFamily.InterNetwork && b.Length == 4)
            {
                // 10.0.0.0/8
                if (b[0] == 10)
                    return true;
                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168)
                    return true;
                // 169.254.0.0/16 link-local (APIPA)
                if (b[0] == 169 && b[1] == 254)
                    return true;
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6 && b.Length == 16)
            {
                // fe80::/10 link-local
                if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80)
                    return true;
                // fc00::/7 unique-local
                if ((b[0] & 0xfe) == 0xfc)
                    return true;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Computes the network (subnet) address by AND-ing the address with the mask.
        /// </summary>
        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] a = address.GetAddressBytes();
            byte[] m = subnetMask.GetAddressBytes();
            RequireSameWidth(a, m);

            byte[] network = new byte[a.Length];
            for (int i = 0; i < a.Length; i++)
                network[i] = (byte)(a[i] & m[i]);

            return new IPAddress(network);
        }

        /// <summary>
        /// Computes the directed broadcast address by OR-ing the address with the
        /// inverted mask. Only meaningful for IPv4.
        /// </summary>
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] a = address.GetAddressBytes();
            byte[] m = subnetMask.GetAddressBytes();
            RequireSameWidth(a, m);

            byte[] broadcast = new byte[a.Length];
            for (int i = 0; i < a.Length; i++)
                broadcast[i] = (byte)(a[i] | (byte)~m[i]);

            return new IPAddress(broadcast);
        }

        /// <summary>
        /// Returns <c>true</c> when both addresses resolve to the same network
        /// address under the supplied mask.
        /// </summary>
        public static bool IsSameSubnet(this IPAddress address, IPAddress other, IPAddress subnetMask)
        {
            byte[] left = address.GetNetworkAddress(subnetMask).GetAddressBytes();
            byte[] right = other.GetNetworkAddress(subnetMask).GetAddressBytes();

            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }

        private static void RequireSameWidth(byte[] a, byte[] m)
        {
            if (a.Length != m.Length)
                throw new ArgumentException(
                    "Address and subnet mask must belong to the same address family.");
        }
    }
}
