namespace PFound.Backend.Core
{
    /// <summary>Seam: peer ID ↔ player session binding (connection lifecycle).</summary>
    public interface ISessionStore
    {
        void Bind(int peerId, PlayerSession session);
        bool TryGet(int peerId, out PlayerSession session);
        void Unbind(int peerId);
    }

    /// <summary>Minimal session data: peer → player.</summary>
    public readonly struct PlayerSession
    {
        public long PlayerId { get; init; }
        public string AuthToken { get; init; }
    }
}
