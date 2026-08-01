namespace PFound.Backend
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;

    /// <summary>
    /// Chains operation handlers to a ServerPeer via deferred request exchanges.
    /// Uses closures to capture generic types at registration time, marshaling
    /// async completions back to the pump thread via a thread-safe reply queue.
    /// </summary>
    public sealed class OperationHandlerRegistry
    {
        readonly ConcurrentQueue<Action> _replyQueue;
        readonly List<Action<ServerPeer>> _wiring = new();

        public OperationHandlerRegistry(ConcurrentQueue<Action> replyQueue)
            => _replyQueue = replyQueue;

        /// <summary>
        /// Register an operation handler. The handler is stored as a typed closure
        /// that captures the request type for later generic method dispatch.
        /// </summary>
        public OperationHandlerRegistry Register<TReq, TReply>(
            IOperationHandler<TReq, TReply> handler)
            where TReq : RequestMessage, new()
            where TReply : ReplyMessage, new()
        {
            _wiring.Add(serverPeer =>
            {
                serverPeer.HandleDeferred<TReq>(exchange =>
                {
                    // Fire the handler async. When it completes, marshal the reply
                    // back to the pump thread via the concurrent queue.
                    _ = handler.HandleAsync(exchange.Peer, exchange.Request, CancellationToken.None)
                        .ContinueWith(task =>
                        {
                            if (task.IsCompletedSuccessfully)
                            {
                                // Capture typed exchange and handler result in closure.
                                // Queue the reply action to run on pump thread (RULE 2).
                                var reply = (TReply)task.Result;
                                _replyQueue.Enqueue(() => exchange.Reply(reply));
                            }
                        });
                });
            });
            return this;
        }

        /// <summary>
        /// Attach all registered handlers to a ServerPeer. Invokes each typed closure,
        /// which calls HandleDeferred with the captured request type.
        /// </summary>
        public void AttachTo(ServerPeer serverPeer)
        {
            foreach (var wire in _wiring)
                wire(serverPeer);
        }
    }
}
