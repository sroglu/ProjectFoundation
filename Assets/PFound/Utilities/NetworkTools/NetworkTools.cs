using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PFound.Utilities.NetworkTools
{
    /// <summary>
    /// Host-level network discovery helpers. Engine-free; relies only on
    /// <see cref="System.Net"/>.
    /// </summary>
    public static class NetworkResolver
    {
        /// <summary>
        /// Picks the most likely "real" local IPv4 address of this machine by
        /// walking the operational network interfaces and preferring a routable
        /// private address over loopback / link-local ones.
        /// Returns <c>null</c> when no suitable address is found.
        /// </summary>
        public static IPAddress GetPreferredLocalAddress(
            AddressFamily family = AddressFamily.InterNetwork)
        {
            IPAddress fallback = null;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                {
                    IPAddress candidate = info.Address;
                    if (candidate.AddressFamily != family)
                        continue;
                    if (candidate.IsNullOrZero() || IPAddress.IsLoopback(candidate))
                        continue;

                    // Skip 169.254.x.x auto-configuration addresses unless nothing
                    // better exists.
                    byte[] bytes = candidate.GetAddressBytes();
                    bool isAutoConfig = family == AddressFamily.InterNetwork
                        && bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;

                    if (isAutoConfig)
                    {
                        fallback = fallback ?? candidate;
                        continue;
                    }

                    return candidate;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Queries a public "what is my IP" HTTP endpoint and parses the response
        /// body into an <see cref="IPAddress"/>. This performs a blocking network
        /// request; call it off the main thread.
        /// </summary>
        /// <param name="serviceUrl">
        /// An endpoint that returns the caller's public address as plain text.
        /// </param>
        /// <param name="timeoutMilliseconds">Request timeout.</param>
        public static IPAddress QueryExternalAddress(
            string serviceUrl = "https://api.ipify.org",
            int timeoutMilliseconds = 5000)
        {
            if (string.IsNullOrEmpty(serviceUrl))
                throw new ArgumentException("Service URL must be provided.", nameof(serviceUrl));

            var request = (HttpWebRequest)WebRequest.Create(serviceUrl);
            request.Method = "GET";
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            {
                string body = reader.ReadToEnd().Trim();
                return IPAddress.Parse(body);
            }
        }
    }
}
