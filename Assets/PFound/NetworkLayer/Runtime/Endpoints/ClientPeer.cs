using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// The client-side messaging engine: turns typed messages into frames over an
    /// <see cref="IClientLink"/> and back. It owns RPC correlation (await a reply
    /// matched by call token), fire-and-forget notifies, and inbound notify routing.
    /// It is fully transport-agnostic — hand it a loopback link for tests or a
    /// Telepathy TCP link in production. No server internals live here, so it ships
    /// in a client build untouched.
    /// </summary>
    public sealed class ClientPeer
    {
        readonly IClientLink _link;
        readonly MessageCatalog _catalog;
        readonly CallTokenSource _tokens = new CallTokenSource();
        readonly OutstandingCalls _outstanding = new OutstandingCalls();
        readonly IClock _clock;
        readonly ClientLinkOptions _options;
        readonly Dictionary<ushort, Action<Message>> _notifyRoutes = new Dictionary<ushort, Action<Message>>();

        // Per-call round-trip timing state, keyed by call token: the opcode that was
        // sent and the time it left, so we can measure how long the caller waited when
        // the call finally leaves the outstanding table (any outcome).
        struct CallTiming
        {
            public ushort Opcode;
            public long SentMs;
        }
        readonly Dictionary<uint, CallTiming> _timings = new Dictionary<uint, CallTiming>();

        // Stored so a reconnect can re-dial the same endpoint without the caller
        // having to remember it (O2).
        string _dialHost;
        int _dialPort;

        // Awaitable-connect state: settled when the link opens or its deadline trips (O1).
        TaskCompletionSource<bool> _pendingConnect;
        long _connectDeadlineMs;

        public event Action Connected;
        public event Action Disconnected;

        public bool IsConnected => _link.IsOpen;
        public int OutstandingCallCount => _outstanding.Count;

        /// <summary>Per-opcode round-trip latency (count / min / max / average) plus a
        /// slow-call event when a call outlives its threshold. Round-trip is measured
        /// here on the client, reusing the request-reply token correlation, so it is
        /// the time the caller actually waited (network hop + server service time).</summary>
        public CallLatencyStats Latency { get; }

        /// <summary>The endpoint most recently dialed; what a reconnect re-uses.</summary>
        public string RemoteHost => _dialHost;
        public int RemotePort => _dialPort;

        public ClientPeer(IClientLink link, MessageCatalog catalog, ClientLinkOptions options, IClock clock = null)
        {
            _link = link;
            _catalog = catalog;
            _options = options;
            _clock = clock ?? new MonotonicClock();
            Latency = new CallLatencyStats(options.SlowCallThresholdMs);

            _outstanding.Closed += OnCallClosed;
            _link.Opened += OnLinkOpened;
            _link.Closed += OnLinkClosed;
            _link.Received += OnFrame;
        }

        // A call left the outstanding table (settled, faulted, expired, or dropped):
        // close its round-trip measurement and fold it into the per-opcode latency.
        void OnCallClosed(uint token)
        {
            if (!_timings.TryGetValue(token, out var timing))
                return;
            _timings.Remove(token);
            Latency.RecordRoundTrip(timing.Opcode, _clock.NowMs - timing.SentMs);
        }

        public void Connect(string host, int port)
        {
            _dialHost = host;
            _dialPort = port;
            _link.Open(host, port);
        }

        /// <summary>Open the link and await the connected state, faulting with a
        /// <see cref="ConnectTimeoutFault"/> if it is not reached before the deadline.
        /// The deadline is evaluated from <see cref="Update"/>, so keep pumping.</summary>
        public Task ConnectAsync(string host, int port, int timeoutMs = -1)
        {
            _dialHost = host;
            _dialPort = port;
            var promise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingConnect = promise;
            _connectDeadlineMs = _clock.NowMs + (timeoutMs < 0 ? _options.ConnectDeadlineMs : timeoutMs);
            _link.Open(host, port); // may raise Opened synchronously (in-memory) or later (TCP)
            return promise.Task;
        }

        public void Disconnect() => _link.Close();

        /// <summary>Drop the current link (if any) and re-open the stored endpoint.</summary>
        public void Reconnect()
        {
            _link.Close();
            _link.Open(_dialHost, _dialPort);
        }

        /// <summary>Awaitable variant of <see cref="Reconnect"/> reusing the stored endpoint.</summary>
        public Task ReconnectAsync(int timeoutMs = -1)
        {
            _link.Close();
            return ConnectAsync(_dialHost, _dialPort, timeoutMs);
        }

        /// <summary>Send a request and await the correlated reply. Faults on expiry,
        /// link drop, or a non-success control reply from the peer.</summary>
        public Task<TReply> CallAsync<TReply>(RequestMessage request, int deadlineMs = -1)
            where TReply : ReplyMessage
        {
            ushort opcode = _catalog.OpcodeFor(request.GetType());
            uint token = _tokens.Next();
            byte[] frame = FrameCodec.WriteRequest(opcode, token, _catalog.PackBody(request));

            var promise = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
            long now = _clock.NowMs;
            long deadline = now + (deadlineMs < 0 ? _options.CallDeadlineMs : deadlineMs);
            _outstanding.Open(token, promise, deadline, typeof(TReply));
            _timings[token] = new CallTiming { Opcode = opcode, SentMs = now };

            _catalog.Recycle(request);
            _link.Deliver(new ArraySegment<byte>(frame));
            return Await<TReply>(promise.Task);
        }

        static async Task<TReply> Await<TReply>(Task<Message> task) where TReply : ReplyMessage
            => (TReply)await task.ConfigureAwait(false);

        /// <summary>Fire a one-way notify; no reply is correlated.</summary>
        public void Post(NotifyMessage notify)
        {
            ushort opcode = _catalog.OpcodeFor(notify.GetType());
            byte[] frame = FrameCodec.WriteNotify(opcode, _catalog.PackBody(notify));
            _catalog.Recycle(notify);
            _link.Deliver(new ArraySegment<byte>(frame));
        }

        /// <summary>Register a handler for a server-pushed notify type.</summary>
        public void OnNotify<T>(Action<T> handler) where T : NotifyMessage, new()
        {
            ushort opcode = _catalog.OpcodeFor(typeof(T));
            _notifyRoutes[opcode] = msg => handler((T)msg);
        }

        /// <summary>Drive the transport and evaluate call + connect deadlines. Call once per frame/tick.</summary>
        public void Update(int budget = -1)
        {
            _link.Pump(budget < 0 ? _options.PumpBudget : budget);
            _outstanding.ExpireDue(_clock.NowMs);
            EvaluateConnectDeadline();
        }

        void EvaluateConnectDeadline()
        {
            if (_pendingConnect == null || _clock.NowMs < _connectDeadlineMs)
                return;
            var promise = _pendingConnect;
            _pendingConnect = null;
            promise.TrySetException(new ConnectTimeoutFault());
            _link.Close(); // abandon the dangling dial
        }

        void OnLinkOpened()
        {
            var promise = _pendingConnect;
            _pendingConnect = null;
            promise?.TrySetResult(true);
            Connected?.Invoke();
        }

        void OnLinkClosed()
        {
            _outstanding.BreakAll(new LinkDroppedFault());
            Disconnected?.Invoke();
        }

        // Decode isolation (R1): the receive pump must never throw. A frame whose
        // header cannot even be read is logged and skipped; a frame whose body fails
        // to deserialize faults only the single waiting call it belongs to (if any),
        // never the whole connection.
        void OnFrame(ArraySegment<byte> raw)
        {
            Envelope env;
            try
            {
                env = FrameCodec.Read(raw);
            }
            catch (Exception e)
            {
                NetLog.OnAlarm("ClientPeer dropped an undecodable frame header: " + e.Message);
                return;
            }

            try
            {
                switch (env.Kind)
                {
                    case MessageKind.Reply:
                        RouteReply(env);
                        break;
                    case MessageKind.Notify:
                        RouteNotify(env);
                        break;
                    default:
                        NetLog.OnNotice("ClientPeer ignored unexpected inbound kind: " + env.Kind);
                        break;
                }
            }
            catch (Exception e)
            {
                // Isolate the bad frame. If it was a reply, the caller waiting on that
                // token gets a MalformedFrameFault; other calls are untouched.
                if (env.Kind == MessageKind.Reply)
                    _outstanding.Break(env.CallToken, new MalformedFrameFault(e.Message));
                NetLog.OnAlarm("ClientPeer isolated a bad inbound frame (opcode " + env.Opcode + "): " + e.Message);
            }
        }

        void RouteReply(Envelope env)
        {
            if (env.Opcode == FrameCodec.ControlOpcode)
            {
                // Payload-less signal from the peer's watchdog: the call will never
                // be answered normally. Fault the waiter (if it hasn't already expired).
                if (env.Status == ReplyStatus.Expired)
                    _outstanding.Break(env.CallToken, new CallExpiredFault());
                else
                    _outstanding.Break(env.CallToken, new RemoteRejectedFault(env.Status));
                return;
            }

            // Correlation decode: the reply frame carries no opcode of its own (the server
            // echoes the request's), so decode the payload as the type this call already
            // awaits. An unknown token means the caller already gave up — drop the frame.
            if (!_outstanding.TryGetReplyType(env.CallToken, out var replyType))
                return;

            var reply = (ReplyMessage)_catalog.Unpack(replyType, env.Payload);
            reply.Status = env.Status;
            if (!_outstanding.Settle(env.CallToken, reply))
                _catalog.Recycle(reply); // arrived after the caller gave up
        }

        void RouteNotify(Envelope env)
        {
            var msg = _catalog.UnpackByOpcode(env.Opcode, env.Payload);
            if (_notifyRoutes.TryGetValue(env.Opcode, out var route))
                route(msg);
            _catalog.Recycle(msg);
        }
    }
}
