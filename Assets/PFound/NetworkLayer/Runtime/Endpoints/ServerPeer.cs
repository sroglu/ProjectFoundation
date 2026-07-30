#if BACKEND
using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The authoritative server-side messaging engine. Accepts requests and
    /// notifies over an <see cref="IServerLink"/>, dispatches them to typed
    /// handlers by opcode, correlates replies back by call token, and runs a
    /// watchdog that reclaims deferred requests whose serve-deadline elapsed. All
    /// server internals — request authority, metrics, the watchdog — sit behind the
    /// BACKEND symbol so a client build never contains them.
    ///
    /// Two handler flavors:
    ///  - <see cref="Handle{TReq,TReply}"/>  answers synchronously in-dispatch.
    ///  - <see cref="HandleDeferred{TReq}"/>  answers later via a <see cref="RequestExchange{T}"/>,
    ///    which is what the watchdog and the timeout metric exist for.
    /// </summary>
    public sealed class ServerPeer
    {
        readonly IServerLink _link;
        readonly MessageCatalog _catalog;
        readonly ServerLinkOptions _options;
        readonly IClock _clock;

        readonly Dictionary<ushort, Action<int, uint, ArraySegment<byte>>> _requestRoutes
            = new Dictionary<ushort, Action<int, uint, ArraySegment<byte>>>();
        readonly Dictionary<ushort, Action<int, Message>> _notifyRoutes
            = new Dictionary<ushort, Action<int, Message>>();

        readonly List<RequestExchangeBase> _deferred = new List<RequestExchangeBase>();
        readonly HashSet<int> _peers = new HashSet<int>();

        // A1: frames whose opcode had no handler yet, awaiting a late registration.
        readonly EarlyArrivalBuffer _early;

        // A3: real client address recovered from a PROXY preamble, and the set of
        // peers whose next frame is still expected to be that preamble.
        readonly Dictionary<int, string> _resolvedAddress = new Dictionary<int, string>();
        readonly HashSet<int> _awaitingProxy = new HashSet<int>();

        public ServerMetrics Metrics { get; } = new ServerMetrics();

        /// <summary>Richer per-opcode timing, throughput, and deadline diagnostics (A2).</summary>
        public ServerDiagnostics Diagnostics { get; }

        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;

        public bool IsListening => _link.IsListening;
        public IReadOnlyCollection<int> Peers => _peers;

        public ServerPeer(IServerLink link, MessageCatalog catalog, ServerLinkOptions options, IClock clock = null)
        {
            _link = link;
            _catalog = catalog;
            _options = options;
            _clock = clock ?? new MonotonicClock();
            _early = new EarlyArrivalBuffer(options.EarlyArrivalCapacity);
            Diagnostics = new ServerDiagnostics(options.ThroughputWindowMs);

            _link.PeerArrived += OnPeerArrived;
            _link.PeerDeparted += OnPeerDeparted;
            _link.Received += OnFrame;
        }

        public void Listen(int port) => _link.Listen(port);
        public void Halt() => _link.Halt();

        /// <summary>Disconnect (kick) a specific peer from the server side (R3).</summary>
        public void Kick(int peer) => _link.Evict(peer);

        /// <summary>The recovered client address for a peer: the PROXY-reported origin
        /// when proxy parsing is on and a preamble was read, otherwise the transport's
        /// raw socket address (A3).</summary>
        public string RemoteAddressOf(int peer)
        {
            string resolved;
            if (_resolvedAddress.TryGetValue(peer, out resolved))
                return resolved;
            return _link.AddressOf(peer);
        }

        // --- handler registration -------------------------------------------

        public void Handle<TReq, TReply>(Func<int, TReq, TReply> handler)
            where TReq : RequestMessage
            where TReply : ReplyMessage
        {
            ushort opcode = _catalog.OpcodeFor(typeof(TReq));
            _requestRoutes[opcode] = (peer, token, body) =>
            {
                var request = (TReq)_catalog.UnpackByOpcode(opcode, body);
                Metrics.CountRequest();
                long started = _clock.NowMs;

                TReply reply = handler(peer, request);
                _catalog.Recycle(request);

                EmitReply(peer, token, opcode, reply);
                long serviceMs = _clock.NowMs - started;
                Metrics.CountReply(serviceMs);
                Diagnostics.RecordServed(opcode, serviceMs, _clock.NowMs);
                _catalog.Recycle(reply);
            };
            FlushEarly(opcode);
        }

        public void HandleDeferred<TReq>(Action<RequestExchange<TReq>> handler)
            where TReq : RequestMessage
        {
            ushort opcode = _catalog.OpcodeFor(typeof(TReq));
            _requestRoutes[opcode] = (peer, token, body) =>
            {
                var request = (TReq)_catalog.UnpackByOpcode(opcode, body);
                Metrics.CountRequest();

                var exchange = new RequestExchange<TReq>
                {
                    Peer = peer,
                    CallToken = token,
                    Opcode = opcode,
                    Request = request,
                    RawRequest = request,
                    StartedMs = _clock.NowMs,
                    DeadlineMs = _clock.NowMs + _options.ServeDeadlineMs,
                };
                exchange.Bind(this);
                _deferred.Add(exchange);
                handler(exchange);
            };
            FlushEarly(opcode);
        }

        public void OnNotify<T>(Action<int, T> handler) where T : NotifyMessage, new()
        {
            ushort opcode = _catalog.OpcodeFor(typeof(T));
            _notifyRoutes[opcode] = (peer, msg) => handler(peer, (T)msg);
            FlushEarly(opcode);
        }

        // Replay any frames parked before this opcode had a handler (A1). Each replay
        // is decode-isolated exactly like a live frame (R1): a parked frame was only
        // header-validated, so a bad body must fault at most that one call.
        void FlushEarly(ushort opcode)
        {
            _early.DrainTo(opcode, p =>
            {
                var body = new ArraySegment<byte>(p.Body);
                try
                {
                    if (p.Kind == MessageKind.Request)
                        DispatchRequest(p.Peer, p.Opcode, p.CallToken, body);
                    else if (p.Kind == MessageKind.Notify)
                        DispatchNotify(p.Peer, p.Opcode, body);
                }
                catch (Exception e)
                {
                    if (p.Kind == MessageKind.Request)
                        _link.Deliver(p.Peer, new ArraySegment<byte>(
                            FrameCodec.WriteControlReply(p.CallToken, ReplyStatus.Faulted)));
                    NetLog.OnAlarm("ServerPeer isolated a bad replayed frame (opcode " + p.Opcode + "): " + e.Message);
                }
            });
        }

        // --- outbound --------------------------------------------------------

        public void SendTo(int peer, NotifyMessage notify)
        {
            byte[] frame = FrameOf(notify);
            _link.Deliver(peer, new ArraySegment<byte>(frame));
        }

        public void Broadcast(NotifyMessage notify)
        {
            byte[] frame = FrameOf(notify);
            foreach (int peer in _peers)
                _link.Deliver(peer, new ArraySegment<byte>(frame));
        }

        /// <summary>Serialize the notify ONCE and send the same frame to a named subset
        /// of peers (O3).</summary>
        public void SendToMany(IEnumerable<int> peers, NotifyMessage notify)
        {
            byte[] frame = FrameOf(notify);
            foreach (int peer in peers)
                _link.Deliver(peer, new ArraySegment<byte>(frame));
        }

        // Build a notify frame once (serialize + recycle) for one or many recipients.
        byte[] FrameOf(NotifyMessage notify)
        {
            ushort opcode = _catalog.OpcodeFor(notify.GetType());
            byte[] frame = FrameCodec.WriteNotify(opcode, _catalog.PackBody(notify));
            _catalog.Recycle(notify);
            return frame;
        }

        /// <summary>Answer a deferred request. Called by <see cref="RequestExchange{T}.Reply"/>.
        /// Idempotent: a reply after the watchdog reclaimed the exchange is dropped.</summary>
        public void FulfillDeferred(RequestExchangeBase exchange, ReplyMessage reply)
        {
            if (exchange.Answered)
            {
                _catalog.Recycle(reply);
                return;
            }
            exchange.Answered = true;
            _deferred.Remove(exchange);

            EmitReply(exchange.Peer, exchange.CallToken, exchange.Opcode, reply);
            long serviceMs = _clock.NowMs - exchange.StartedMs;
            Metrics.CountReply(serviceMs);
            Diagnostics.RecordServed(exchange.Opcode, serviceMs, _clock.NowMs);
            _catalog.Recycle(exchange.RawRequest);
            _catalog.Recycle(reply);
        }

        // --- pump ------------------------------------------------------------

        public void Update(int budget = -1)
        {
            _link.Pump(budget < 0 ? _options.PumpBudget : budget);
            ReclaimExpired();
            Diagnostics.Tick(_clock.NowMs);
        }

        void ReclaimExpired()
        {
            long now = _clock.NowMs;
            for (int i = _deferred.Count - 1; i >= 0; i--)
            {
                var exchange = _deferred[i];
                if (exchange.Answered || now < exchange.DeadlineMs)
                    continue;

                exchange.Answered = true;
                _deferred.RemoveAt(i);
                Metrics.CountReclaim();
                Diagnostics.RecordDeadlineMiss(exchange.Opcode, exchange.Peer, now - exchange.StartedMs);

                // Best-effort: nudge the client so it can fail fast. It has usually
                // already tripped its own (shorter) deadline, in which case this is
                // dropped as an unknown token on the far side.
                _link.Deliver(exchange.Peer,
                    new ArraySegment<byte>(FrameCodec.WriteControlReply(exchange.CallToken, ReplyStatus.Expired)));
                _catalog.Recycle(exchange.RawRequest);
            }
        }

        // --- inbound ---------------------------------------------------------

        void OnPeerArrived(int peer)
        {
            _peers.Add(peer);
            if (_options.ExpectProxyHeader)
                _awaitingProxy.Add(peer); // next frame is expected to be the PROXY preamble
            PeerConnected?.Invoke(peer);
        }

        void OnPeerDeparted(int peer)
        {
            _peers.Remove(peer);
            _awaitingProxy.Remove(peer);
            _resolvedAddress.Remove(peer);
            PeerDisconnected?.Invoke(peer);
        }

        // Decode isolation (R1): a malformed/oversized/schema-mismatched frame is
        // caught, logged, and skipped — it never throws out through the pump. A bad
        // request frame is answered Faulted so its one caller fails fast.
        void OnFrame(int peer, ArraySegment<byte> raw)
        {
            // A3: the first frame from a proxied peer carries the origin, not a message.
            if (_awaitingProxy.Contains(peer))
            {
                ConsumeProxyPreamble(peer, raw);
                return;
            }

            Envelope env;
            try
            {
                env = FrameCodec.Read(raw);
            }
            catch (Exception e)
            {
                NetLog.OnAlarm("ServerPeer dropped an undecodable frame header from peer " + peer + ": " + e.Message);
                return;
            }

            try
            {
                switch (env.Kind)
                {
                    case MessageKind.Request:
                        if (_requestRoutes.ContainsKey(env.Opcode))
                            DispatchRequest(peer, env.Opcode, env.CallToken, env.Payload);
                        else if (_options.BufferEarlyArrivals)
                            _early.Park(peer, MessageKind.Request, env.Opcode, env.CallToken, env.Payload, _clock.NowMs);
                        else
                            _link.Deliver(peer, new ArraySegment<byte>(
                                FrameCodec.WriteControlReply(env.CallToken, ReplyStatus.Unroutable)));
                        break;

                    case MessageKind.Notify:
                        if (_notifyRoutes.ContainsKey(env.Opcode))
                            DispatchNotify(peer, env.Opcode, env.Payload);
                        else if (_options.BufferEarlyArrivals)
                            _early.Park(peer, MessageKind.Notify, env.Opcode, 0, env.Payload, _clock.NowMs);
                        else
                            NetLog.OnNotice("ServerPeer dropped notify with no route, opcode " + env.Opcode);
                        break;

                    default:
                        NetLog.OnNotice("ServerPeer ignored unexpected inbound kind: " + env.Kind);
                        break;
                }
            }
            catch (Exception e)
            {
                if (env.Kind == MessageKind.Request)
                    _link.Deliver(peer, new ArraySegment<byte>(
                        FrameCodec.WriteControlReply(env.CallToken, ReplyStatus.Faulted)));
                NetLog.OnAlarm("ServerPeer isolated a bad inbound frame from peer " + peer +
                               " (opcode " + env.Opcode + "): " + e.Message);
            }
        }

        void DispatchRequest(int peer, ushort opcode, uint token, ArraySegment<byte> body)
            => _requestRoutes[opcode](peer, token, body);

        void DispatchNotify(int peer, ushort opcode, ArraySegment<byte> body)
        {
            var msg = _catalog.UnpackByOpcode(opcode, body);
            Metrics.CountNotify();
            _notifyRoutes[opcode](peer, msg);
            _catalog.Recycle(msg);
        }

        // A3: parse the PROXY preamble, record the real origin, and do not dispatch it.
        void ConsumeProxyPreamble(int peer, ArraySegment<byte> raw)
        {
            _awaitingProxy.Remove(peer);
            ProxyOrigin origin;
            if (ProxyProtocolReader.TryRead(raw, out origin) && origin.HasSource)
                _resolvedAddress[peer] = origin.SourceAddress;
            else
                NetLog.OnNotice("ServerPeer saw no usable PROXY origin from peer " + peer +
                                "; falling back to the raw socket address.");
        }

        // The reply is correlated by call token and decoded on the client against the type
        // that call awaits, so the reply type has no opcode of its own. The frame still needs
        // a NON-control opcode (0 is reserved for control), so echo the REQUEST's opcode —
        // the client ignores it for routing. This also means the reply type is never enrolled.
        void EmitReply(int peer, uint token, ushort requestOpcode, ReplyMessage reply)
        {
            byte[] frame = FrameCodec.WriteReply(requestOpcode, token, reply.Status, _catalog.PackBody(reply));
            _link.Deliver(peer, new ArraySegment<byte>(frame));
        }
    }
}
#endif
