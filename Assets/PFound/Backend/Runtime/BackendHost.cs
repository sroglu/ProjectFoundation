namespace PFound.Backend
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using PFound.NetworkLayer;

    /// <summary>
    /// Single-threaded game loop host for the backend server. Owns the ServerPeer
    /// and the deferred reply queue, ticking both in sync every frame.
    /// </summary>
    public sealed class BackendHost
    {
        readonly ServerPeer _serverPeer;
        readonly ConcurrentQueue<Action> _replyQueue;

        public BackendHost(ServerPeer serverPeer, ConcurrentQueue<Action> replyQueue)
        {
            _serverPeer = serverPeer;
            _replyQueue = replyQueue;
        }

        /// <summary>Start the server listening on the given port.</summary>
        public void Listen(int port)
        {
            _serverPeer.Listen(port);
        }

        /// <summary>
        /// Tick the server once per frame:
        /// 1. Pump incoming frames from the network link
        /// 2. Drain deferred replies queued by threadpool completions (RULE 2)
        ///
        /// All async handlers enqueue their replies here; draining on the pump
        /// thread ensures no race with the watchdog's expired request reclaim.
        /// </summary>
        public void Tick()
        {
            _serverPeer.Update();

            // Drain deferred replies queued by threadpool continuations.
            // Each action is a closure that calls exchange.Reply(reply) on the pump thread.
            while (_replyQueue.TryDequeue(out var act))
            {
                act();
            }
        }

        /// <summary>Halt the server and close all peer connections.</summary>
        public void Halt()
        {
            _serverPeer.Halt();
        }

        /// <summary>Read-only view of connected peer IDs.</summary>
        public IReadOnlyCollection<int> Peers => _serverPeer.Peers;
    }
}
