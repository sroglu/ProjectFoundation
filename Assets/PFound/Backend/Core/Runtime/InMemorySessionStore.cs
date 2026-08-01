namespace PFound.Backend.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>Faz 1 default: in-memory peer ↔ session map.</summary>
    public sealed class InMemorySessionStore : ISessionStore
    {
        readonly Dictionary<int, PlayerSession> _sessions = new();

        public void Bind(int peerId, PlayerSession session)
        {
            if (_sessions.ContainsKey(peerId))
                throw new InvalidOperationException($"Peer {peerId} already bound");
            _sessions[peerId] = session;
        }

        public bool TryGet(int peerId, out PlayerSession session)
            => _sessions.TryGetValue(peerId, out session);

        public void Unbind(int peerId)
            => _sessions.Remove(peerId);
    }
}
