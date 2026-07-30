namespace PFound.NetworkLayer
{
    /// <summary>
    /// The ambient client the generated <c>&lt;Op&gt;.CallAsync(args)</c> / <c>&lt;Notify&gt;.Send(args)</c>
    /// entry points send through. Configure it ONCE at startup, right after the peer is built:
    /// <code>NetworkClient.Current = clientPeer;</code>
    /// so game code can call an operation uniformly (<c>await GetPlayerData.CallAsync(playerId)</c>) without
    /// threading a <see cref="ClientPeer"/> through every call site. It is left unset by default and is a
    /// plain settable field on purpose — an unconfigured call faults fast with a
    /// <see cref="System.NullReferenceException"/> (nothing here should be null at runtime; no defensive guard).
    /// </summary>
    public static class NetworkClient
    {
        /// <summary>The peer every generated <c>CallAsync</c>/<c>Send</c> uses. Assign once at boot.</summary>
        public static ClientPeer Current;
    }
}
