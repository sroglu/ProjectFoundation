using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using PFound.NetworkLayer;

namespace PFound.NetworkLayer.Tests
{
    // --- sample message contracts (what a game project would author) ----------

    public sealed class EchoRequest : RequestMessage
    {
        public string Text;
        public int Nonce;
        public override void Clear() { Text = null; Nonce = 0; }
    }

    public sealed class EchoReply : ReplyMessage
    {
        public string Text;
        public int Nonce;
        public override void Clear() { base.Clear(); Text = null; Nonce = 0; }
    }

    public sealed class HeartbeatNotify : NotifyMessage
    {
        public int Beat;
        public override void Clear() { Beat = 0; }
    }

    static class Opcodes
    {
        public const ushort EchoRequest = 4101;
        public const ushort EchoReply = 4102;
        public const ushort Heartbeat = 4103;
    }

    /// <summary>
    /// Deterministic coverage over the in-memory transport: RPC round-trip, notify
    /// delivery, opcode dispatch, both timeout mechanisms, latency emulation, and
    /// message pooling/reset. No sockets, no threads — everything advances by
    /// pumping, so results are reproducible.
    /// </summary>
    [TestFixture]
    public sealed class NetworkLayerTests
    {
        static MessageCatalog NewCatalog()
        {
            var catalog = new MessageCatalog(new ReflectionBodyCodec());
            catalog.Enroll<EchoRequest>(Opcodes.EchoRequest);
            // A reply is decoded by correlation (the caller's known reply type), not by its
            // own opcode, so it is NOT enrolled — only registered for pooling by type.
            catalog.RegisterReplyType<EchoReply>();
            catalog.Enroll<HeartbeatNotify>(Opcodes.Heartbeat);
            return catalog;
        }

        // ---- pooling (no transport needed) ---------------------------------

        [Test]
        public void RecycledMessageIsReusedAndCleared()
        {
            var catalog = NewCatalog();

            var first = catalog.Take<EchoReply>();
            first.Text = "state";
            first.Nonce = 42;
            first.Status = ReplyStatus.Faulted;
            catalog.Recycle(first);

            var second = catalog.Take<EchoReply>();
            Assert.AreSame(first, second, "pool should hand back the recycled instance");
            Assert.IsNull(second.Text);
            Assert.AreEqual(0, second.Nonce);
            Assert.AreEqual(ReplyStatus.Ok, second.Status, "Clear must reset the reply status");
        }

        [Test]
        public void FrameRoundTripsHeaderFields()
        {
            byte[] frame = FrameCodec.WriteReply(Opcodes.EchoReply, 0xABCDu, ReplyStatus.Refused, default);
            Envelope env = FrameCodec.Read(new System.ArraySegment<byte>(frame));
            Assert.AreEqual(MessageKind.Reply, env.Kind);
            Assert.AreEqual(Opcodes.EchoReply, env.Opcode);
            Assert.AreEqual(0xABCDu, env.CallToken);
            Assert.AreEqual(ReplyStatus.Refused, env.Status);
        }

        // ---- A3: PROXY-protocol parser (pure, no transport) ----------------

        [Test]
        public void ProxyV1TextHeaderRecoversOrigin()
        {
            byte[] header = System.Text.Encoding.ASCII.GetBytes("PROXY TCP4 1.2.3.4 5.6.7.8 4000 443\r\n");
            ProxyOrigin origin;
            Assert.IsTrue(ProxyProtocolReader.TryRead(new System.ArraySegment<byte>(header), out origin));
            Assert.IsTrue(origin.HasSource);
            Assert.AreEqual("1.2.3.4", origin.SourceAddress);
            Assert.AreEqual(4000, origin.SourcePort);
            Assert.AreEqual("5.6.7.8", origin.DestinationAddress);
            Assert.AreEqual(443, origin.DestinationPort);
            Assert.AreEqual(header.Length, origin.HeaderBytes);
        }

        [Test]
        public void ProxyV1UnknownHeaderConsumesButYieldsNoOrigin()
        {
            byte[] header = System.Text.Encoding.ASCII.GetBytes("PROXY UNKNOWN\r\n");
            ProxyOrigin origin;
            Assert.IsTrue(ProxyProtocolReader.TryRead(new System.ArraySegment<byte>(header), out origin));
            Assert.IsFalse(origin.HasSource);
            Assert.AreEqual(header.Length, origin.HeaderBytes);
        }

        [Test]
        public void ProxyV2BinaryHeaderRecoversIpv4Origin()
        {
            // signature(12) + verCmd(0x21) + famProto(0x11) + len(12) + src(4)+dst(4)+sport(2)+dport(2)
            byte[] h = new byte[]
            {
                0x0D,0x0A,0x0D,0x0A,0x00,0x0D,0x0A,0x51,0x55,0x49,0x54,0x0A, // signature
                0x21,             // version 2, command PROXY
                0x11,             // AF_INET, STREAM
                0x00,0x0C,        // 12 address bytes follow
                11,22,33,44,      // source ip
                55,66,77,88,      // dest ip
                0x1F,0x40,        // source port 8000
                0x01,0xBB,        // dest port 443
            };
            ProxyOrigin origin;
            Assert.IsTrue(ProxyProtocolReader.TryRead(new System.ArraySegment<byte>(h), out origin));
            Assert.IsTrue(origin.HasSource);
            Assert.AreEqual("11.22.33.44", origin.SourceAddress);
            Assert.AreEqual(8000, origin.SourcePort);
            Assert.AreEqual(443, origin.DestinationPort);
            Assert.AreEqual(h.Length, origin.HeaderBytes);
        }

        [Test]
        public void NonProxyBytesAreRejectedCleanly()
        {
            byte[] junk = { 1, 2, 3, 4, 5, 6, 7, 8 };
            ProxyOrigin origin;
            Assert.IsFalse(ProxyProtocolReader.TryRead(new System.ArraySegment<byte>(junk), out origin));
        }

#if BACKEND
        sealed class Harness
        {
            public ClientPeer Client;
            public ServerPeer Server;
            public MessageCatalog ClientCatalog;
            public MessageCatalog ServerCatalog;
            public ManualClock ClientClock = new ManualClock();
            public ManualClock ServerClock = new ManualClock();
            public LoopbackClientLink ClientLink;
            public LoopbackServerLink ServerLink;
            public LoopbackHub Hub;

            public static Harness Build() => Build(ClientLinkOptions.Default, ServerLinkOptions.Default);

            public static Harness Build(ClientLinkOptions clientOpts, ServerLinkOptions serverOpts)
            {
                var hub = new LoopbackHub();
                var h = new Harness
                {
                    Hub = hub,
                    ClientCatalog = NewCatalog(),
                    ServerCatalog = NewCatalog(),
                };
                h.ClientLink = new LoopbackClientLink(hub);
                h.ServerLink = new LoopbackServerLink(hub);
                h.Client = new ClientPeer(h.ClientLink, h.ClientCatalog, clientOpts, h.ClientClock);
                h.Server = new ServerPeer(h.ServerLink, h.ServerCatalog, serverOpts, h.ServerClock);
                h.Server.Listen(0);
                h.Client.Connect("loopback", 0);
                return h;
            }
        }

        // ---- test-only link stubs for capabilities loopback can't express ---

        // Never opens: exercises the connect-timeout path (O1).
        sealed class SilentClientLink : IClientLink
        {
            public event System.Action Opened;
            public event System.Action Closed;
            public event System.Action<System.ArraySegment<byte>> Received;
            public bool IsOpen { get; private set; }
            public void Open(string host, int port) { /* deliberately never signals Opened */ }
            public void Close() { IsOpen = false; Closed?.Invoke(); }
            public bool Deliver(System.ArraySegment<byte> frame) => false;
            public void Pump(int budget) { }
            public void Touch() { Opened?.Invoke(); Received?.Invoke(default); } // silence unused warnings
        }

        // Records the endpoint it was last asked to open (O2 reuse check).
        sealed class RecordingClientLink : IClientLink
        {
            public string LastHost;
            public int LastPort;
            public int OpenCount;
            public event System.Action Opened;
            public event System.Action Closed;
            public event System.Action<System.ArraySegment<byte>> Received;
            public bool IsOpen { get; private set; }
            public void Open(string host, int port) { LastHost = host; LastPort = port; OpenCount++; IsOpen = true; Opened?.Invoke(); }
            public void Close() { IsOpen = false; Closed?.Invoke(); }
            public bool Deliver(System.ArraySegment<byte> frame) => true;
            public void Pump(int budget) { Received?.Invoke(default); }
        }

        // Captures every (peer, frame) delivery for the serialize-once fanout check (O3).
        sealed class CountingServerLink : IServerLink
        {
            public readonly System.Collections.Generic.List<int> DeliveredPeers = new System.Collections.Generic.List<int>();
            public readonly System.Collections.Generic.List<byte[]> DeliveredArrays = new System.Collections.Generic.List<byte[]>();
            public event System.Action<int> PeerArrived;
            public event System.Action<int> PeerDeparted;
            public event System.Action<int, System.ArraySegment<byte>> Received;
            public bool IsListening { get; private set; }
            public void Listen(int port) => IsListening = true;
            public void Halt() => IsListening = false;
            public bool Deliver(int peer, System.ArraySegment<byte> frame)
            {
                DeliveredPeers.Add(peer);
                DeliveredArrays.Add(frame.Array);
                return true;
            }
            public void Evict(int peer) => PeerDeparted?.Invoke(peer);
            public string AddressOf(int peer) => "0.0.0.0";
            public void Pump(int budget) { PeerArrived?.Invoke(0); Received?.Invoke(0, default); }
        }

        // A call completes via a RunContinuationsAsynchronously TaskCompletionSource
        // (see ClientPeer.CallAsync) so its result is published on a ThreadPool bounce,
        // not inline in Update(). Awaiting such a Task directly from a PlayMode
        // [Test] relies on the runner resuming the captured SynchronizationContext,
        // which can stall the runner indefinitely. Instead, drive the test as a
        // [UnityTest] and pump frames until the Task settles, with a hard frame cap so
        // a never-completing call fails the test rather than hanging the whole run.
        static IEnumerator SettleWithin(Task task, int maxFrames = 600)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (task.IsCompleted)
                    yield break;
                yield return null;
            }
            Assert.Fail("Task did not settle within " + maxFrames + " frames (timeout guard).");
        }

        // Inspect the fault of an ALREADY-settled Task without blocking the main thread.
        // Reading Task.Exception on a faulted task neither blocks nor rethrows (unlike
        // .Result/.Wait()/GetAwaiter().GetResult()), so this is safe to call right after
        // SettleWithin has confirmed completion. Never await the Task on the main thread:
        // CallAsync/ConnectAsync complete via RunContinuationsAsynchronously, so a
        // synchronous await (or NUnit's ThrowsAsync, which blocks the calling thread)
        // deadlocks against the captured PlayMode SynchronizationContext and hangs Unity.
        static TFault ExpectFault<TFault>(Task task) where TFault : System.Exception
        {
            Assert.IsTrue(task.IsCompleted, "task must be settled before inspecting its fault");
            Assert.IsTrue(task.IsFaulted, "expected the call to fault");
            System.Exception ex = task.Exception.InnerException; // non-blocking: already faulted
            Assert.IsInstanceOf<TFault>(ex, "unexpected fault type");
            return (TFault)ex;
        }

        [UnityTest]
        public IEnumerator RequestReplyRoundTrip()
        {
            var h = Harness.Build();
            h.Server.Handle<EchoRequest, EchoReply>((peer, req) =>
            {
                var reply = h.ServerCatalog.Take<EchoReply>();
                reply.Text = req.Text + "!";
                reply.Nonce = req.Nonce;
                reply.Status = ReplyStatus.Ok;
                return reply;
            });

            var request = h.ClientCatalog.Take<EchoRequest>();
            request.Text = "hi";
            request.Nonce = 7;

            var call = h.Client.CallAsync<EchoReply>(request);
            h.Server.Update();
            h.Client.Update();

            yield return SettleWithin(call);
            var reply = call.Result;
            Assert.AreEqual("hi!", reply.Text);
            Assert.AreEqual(7, reply.Nonce);
            Assert.AreEqual(ReplyStatus.Ok, reply.Status);
            Assert.AreEqual(1, h.Server.Metrics.RequestsAccepted);
            Assert.AreEqual(1, h.Server.Metrics.RepliesEmitted);
        }

        [Test]
        public void NotifyIsDeliveredToServerHandler()
        {
            var h = Harness.Build();
            int received = -1;
            h.Server.OnNotify<HeartbeatNotify>((peer, n) => received = n.Beat);

            var beat = h.ClientCatalog.Take<HeartbeatNotify>();
            beat.Beat = 99;
            h.Client.Post(beat);
            h.Server.Update();

            Assert.AreEqual(99, received);
            Assert.AreEqual(1, h.Server.Metrics.NotifiesAccepted);
        }

        [UnityTest]
        public IEnumerator UnroutableRequestFaultsCaller()
        {
            var h = Harness.Build(); // no handler registered for EchoRequest
            var request = h.ClientCatalog.Take<EchoRequest>();
            request.Text = "x";

            var call = h.Client.CallAsync<EchoReply>(request);
            h.Server.Update();
            h.Client.Update();

            yield return SettleWithin(call);
            var fault = ExpectFault<RemoteRejectedFault>(call);
            Assert.AreEqual(ReplyStatus.Unroutable, fault.Status);
        }

        [UnityTest]
        public IEnumerator ClientDeadlineExpiresCall()
        {
            var h = Harness.Build(); // server never processes the request
            var request = h.ClientCatalog.Take<EchoRequest>();

            var call = h.Client.CallAsync<EchoReply>(request, deadlineMs: 500);
            h.ClientClock.Advance(600);
            h.Client.Update();

            yield return SettleWithin(call);
            ExpectFault<CallExpiredFault>(call);
        }

        [UnityTest]
        public IEnumerator ServerWatchdogReclaimsDeferredRequest()
        {
            var h = Harness.Build();
            h.Server.HandleDeferred<EchoRequest>(_ => { /* deliberately never reply */ });

            var request = h.ClientCatalog.Take<EchoRequest>();
            var call = h.Client.CallAsync<EchoReply>(request, deadlineMs: 1000000);

            h.Server.Update(); // accepts + defers
            h.ServerClock.Advance(ServerLinkOptions.Default.ServeDeadlineMs + 25);
            h.Server.Update(); // watchdog reclaims

            Assert.AreEqual(1, h.Server.Metrics.CallsReclaimed);

            h.Client.Update(); // consumes the control-expiry frame
            yield return SettleWithin(call);
            ExpectFault<CallExpiredFault>(call);
        }

        [UnityTest]
        public IEnumerator DeferredReplyCompletesOnLaterTick()
        {
            var h = Harness.Build();
            RequestExchange<EchoRequest> stashed = null;
            h.Server.HandleDeferred<EchoRequest>(ex => stashed = ex);

            var request = h.ClientCatalog.Take<EchoRequest>();
            request.Nonce = 3;
            var call = h.Client.CallAsync<EchoReply>(request);

            h.Server.Update(); // handler stashes the exchange
            Assert.IsNotNull(stashed);

            var reply = h.ServerCatalog.Take<EchoReply>();
            reply.Nonce = stashed.Request.Nonce;
            reply.Status = ReplyStatus.Ok;
            stashed.Reply(reply);

            h.Client.Update();
            yield return SettleWithin(call);
            var result = call.Result;
            Assert.AreEqual(3, result.Nonce);
        }

        [UnityTest]
        public IEnumerator LatencyShimDefersDelivery()
        {
            var hub = new LoopbackHub();
            var clock = new ManualClock();
            var innerClient = new LoopbackClientLink(hub);
            var shim = new LatencyShapedLink(innerClient, clock, LatencyProfile.Constant(300));
            var clientCatalog = NewCatalog();
            var serverCatalog = NewCatalog();
            var client = new ClientPeer(shim, clientCatalog, ClientLinkOptions.Default, clock);
            var server = new ServerPeer(new LoopbackServerLink(hub), serverCatalog, ServerLinkOptions.Default, new ManualClock());

            server.Handle<EchoRequest, EchoReply>((peer, req) =>
            {
                var r = serverCatalog.Take<EchoReply>();
                r.Nonce = req.Nonce;
                r.Status = ReplyStatus.Ok;
                return r;
            });
            server.Listen(0);
            client.Connect("loopback", 0);

            var request = clientCatalog.Take<EchoRequest>();
            request.Nonce = 5;
            var call = client.CallAsync<EchoReply>(request);

            server.Update();  // reply queued toward client
            client.Update();  // shim absorbs it but delay has not elapsed

            // The frame is held by the latency window (not yet due), so the call is
            // genuinely un-settled here regardless of the async completion plumbing.
            Assert.IsFalse(call.IsCompleted, "reply must be held for the emulated latency");

            clock.Advance(350);
            client.Update();  // delay elapsed -> released

            yield return SettleWithin(call);
            var reply = call.Result;
            Assert.AreEqual(5, reply.Nonce);
        }

        // ==== Round 2 additions =============================================

        // R1: a reply whose body cannot be decoded faults ONLY that one call, and the
        // receive pump keeps running (a second outstanding call is untouched).
        [UnityTest]
        public IEnumerator MalformedReplyFaultsOnlyItsOwnCall()
        {
            var h = Harness.Build();
            h.Server.Update(); // let the peer arrive so the server link can deliver

            // First outstanding call gets token 1 (CallTokenSource starts at 1).
            var reqA = h.ClientCatalog.Take<EchoRequest>();
            var callA = h.Client.CallAsync<EchoReply>(reqA, deadlineMs: 1000000);
            var reqB = h.ClientCatalog.Take<EchoRequest>();
            var callB = h.Client.CallAsync<EchoReply>(reqB, deadlineMs: 1000000);

            // Inject a reply for token 1 whose header decodes but whose body is garbage
            // (too short for EchoReply's fields), forcing the codec to throw.
            byte[] bad = FrameCodec.WriteReply(Opcodes.EchoReply, 1u, ReplyStatus.Ok,
                new System.ArraySegment<byte>(new byte[] { 0x01, 0x02 }));
            h.ServerLink.Deliver(LoopbackHub.PeerKey, new System.ArraySegment<byte>(bad));

            h.Client.Update(); // pump must NOT throw

            yield return SettleWithin(callA);
            ExpectFault<MalformedFrameFault>(callA);
            Assert.IsFalse(callB.IsCompleted, "the unrelated call must remain outstanding");
        }

        // R1: a request with an undecodable body is answered Faulted, not propagated.
        [Test]
        public void MalformedRequestBodyIsIsolatedAndAnswered()
        {
            var h = Harness.Build();
            h.Server.Handle<EchoRequest, EchoReply>((peer, req) =>
            {
                var r = h.ServerCatalog.Take<EchoReply>();
                r.Status = ReplyStatus.Ok;
                return r;
            });
            h.Server.Update(); // peer arrives

            // Craft a request frame for a real opcode but with a body too short to decode.
            byte[] bad = FrameCodec.WriteRequest(Opcodes.EchoRequest, 7u,
                new System.ArraySegment<byte>(new byte[] { 0x01 }));
            h.ClientLink.Deliver(new System.ArraySegment<byte>(bad));
            Assert.DoesNotThrow(() => h.Server.Update(), "bad body must not throw through the pump");
        }

        // R2: the transport config exposes bounded send/receive queue limits.
        [Test]
        public void TransportConfigCarriesQueueLimits()
        {
            Assert.IsTrue(ClientLinkOptions.Default.SendQueueLimit > 0);
            Assert.IsTrue(ClientLinkOptions.Default.ReceiveQueueLimit > 0);
            Assert.IsTrue(ServerLinkOptions.Default.SendQueueLimit > 0);
            Assert.IsTrue(ServerLinkOptions.Default.ReceiveQueueLimit > 0);
        }

        // R3: the server can kick a specific peer.
        [Test]
        public void KickDisconnectsThePeer()
        {
            var h = Harness.Build();
            h.Server.Update(); // peer arrives
            Assert.AreEqual(1, h.Server.Peers.Count);

            int departed = -1;
            h.Server.PeerDisconnected += p => departed = p;
            h.Server.Kick(LoopbackHub.PeerKey);

            Assert.AreEqual(LoopbackHub.PeerKey, departed);
            Assert.AreEqual(0, h.Server.Peers.Count);
        }

        // O1: connect faults with a ConnectTimeoutFault if the link never opens.
        [UnityTest]
        public IEnumerator ConnectAsyncTimesOutWhenLinkNeverOpens()
        {
            var clock = new ManualClock();
            var link = new SilentClientLink();
            var client = new ClientPeer(link, NewCatalog(), ClientLinkOptions.Default, clock);

            var connect = client.ConnectAsync("nowhere", 1, timeoutMs: 500);
            Assert.IsFalse(connect.IsCompleted);

            clock.Advance(600);
            client.Update(); // deadline evaluated here

            yield return SettleWithin(connect);
            ExpectFault<ConnectTimeoutFault>(connect);
        }

        // O2: reconnect re-opens the stored endpoint without the caller re-supplying it.
        [Test]
        public void ReconnectReusesStoredEndpoint()
        {
            var link = new RecordingClientLink();
            var client = new ClientPeer(link, NewCatalog(), ClientLinkOptions.Default, new ManualClock());

            client.Connect("game.example", 42);
            Assert.AreEqual("game.example", client.RemoteHost);
            Assert.AreEqual(42, client.RemotePort);

            client.Reconnect();
            Assert.AreEqual("game.example", link.LastHost);
            Assert.AreEqual(42, link.LastPort);
            Assert.AreEqual(2, link.OpenCount, "reconnect should re-open the same endpoint");
        }

        // O3: notify-to-subset serializes the body once and reuses the SAME frame.
        [Test]
        public void SendToManySerializesOnce()
        {
            var link = new CountingServerLink();
            var server = new ServerPeer(link, NewCatalog(), ServerLinkOptions.Default, new ManualClock());

            var beat = new HeartbeatNotify { Beat = 5 };
            server.SendToMany(new[] { 10, 20, 30 }, beat);

            Assert.AreEqual(3, link.DeliveredPeers.Count);
            Assert.AreEqual(10, link.DeliveredPeers[0]);
            Assert.AreEqual(30, link.DeliveredPeers[2]);
            // Same underlying byte[] handed to every recipient -> serialized exactly once.
            Assert.AreSame(link.DeliveredArrays[0], link.DeliveredArrays[1]);
            Assert.AreSame(link.DeliveredArrays[1], link.DeliveredArrays[2]);
        }

        // A1 (unit): bounded FIFO with drop-oldest eviction and arrival-order replay.
        [Test]
        public void EarlyArrivalBufferEvictsOldestAndReplaysInOrder()
        {
            var buf = new EarlyArrivalBuffer(2);
            buf.Park(1, MessageKind.Notify, 4103, 0, new System.ArraySegment<byte>(new byte[] { 1 }), 0);
            buf.Park(1, MessageKind.Notify, 4103, 0, new System.ArraySegment<byte>(new byte[] { 2 }), 1);
            buf.Park(1, MessageKind.Notify, 4103, 0, new System.ArraySegment<byte>(new byte[] { 3 }), 2);

            Assert.AreEqual(2, buf.Count);
            Assert.AreEqual(1, buf.EvictionCount);

            var seen = new System.Collections.Generic.List<byte>();
            buf.DrainTo(4103, p => seen.Add(p.Body[0]));
            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(2, seen[0]); // oldest survivor first (the '1' was evicted)
            Assert.AreEqual(3, seen[1]);
            Assert.AreEqual(0, buf.Count);
        }

        // A1 (integration): a notify arriving before its handler is replayed on register.
        [Test]
        public void EarlyNotifyIsReplayedWhenHandlerRegisters()
        {
            var opts = ServerLinkOptions.Default;
            opts.BufferEarlyArrivals = true;
            var h = Harness.Build(ClientLinkOptions.Default, opts);

            var beat = h.ClientCatalog.Take<HeartbeatNotify>();
            beat.Beat = 77;
            h.Client.Post(beat);
            h.Server.Update(); // no handler yet -> parked

            int received = -1;
            h.Server.OnNotify<HeartbeatNotify>((peer, n) => received = n.Beat); // triggers replay
            Assert.AreEqual(77, received);
        }

        // A2 (unit): per-opcode timing, throughput window, and deadline event.
        [Test]
        public void DiagnosticsTracksTimingThroughputAndDeadlines()
        {
            var diag = new ServerDiagnostics(windowMs: 1000);
            diag.RecordServed(4101, serviceMs: 10, nowMs: 0);
            diag.RecordServed(4101, serviceMs: 30, nowMs: 100);

            ServerDiagnostics.OpcodeStat stat;
            Assert.IsTrue(diag.TryGet(4101, out stat));
            Assert.AreEqual(2, stat.Served);
            Assert.AreEqual(10, stat.MinMs);
            Assert.AreEqual(30, stat.MaxMs);
            Assert.AreEqual(20.0, stat.MeanMs);

            ServerDiagnostics.ThroughputSample sample = default;
            int samples = 0;
            diag.ThroughputReported += s => { sample = s; samples++; };
            diag.Tick(500);  // window not elapsed
            Assert.AreEqual(0, samples);
            diag.Tick(1100); // window elapsed
            Assert.AreEqual(1, samples);
            Assert.AreEqual(2, sample.Served);

            ServerDiagnostics.DeadlineMiss miss = default;
            diag.DeadlineExceeded += m => miss = m;
            diag.RecordDeadlineMiss(4101, peer: 9, elapsedMs: 4321);
            Assert.AreEqual(4101, miss.Opcode);
            Assert.AreEqual(9, miss.Peer);
            Assert.AreEqual(1, diag.DeadlineMissCount);
        }

        // A2 (integration): the watchdog reclaim fires the deadline-exceeded event.
        [UnityTest]
        public IEnumerator WatchdogReclaimRaisesDeadlineEvent()
        {
            var h = Harness.Build();
            h.Server.HandleDeferred<EchoRequest>(_ => { /* never reply */ });

            ServerDiagnostics.DeadlineMiss miss = default;
            int fired = 0;
            h.Server.Diagnostics.DeadlineExceeded += m => { miss = m; fired++; };

            var request = h.ClientCatalog.Take<EchoRequest>();
            var call = h.Client.CallAsync<EchoReply>(request, deadlineMs: 1000000);
            h.Server.Update(); // accept + defer
            h.ServerClock.Advance(ServerLinkOptions.Default.ServeDeadlineMs + 25);
            h.Server.Update(); // reclaim -> event

            Assert.AreEqual(1, fired);
            Assert.AreEqual(Opcodes.EchoRequest, miss.Opcode);
            h.Client.Update();
            yield return SettleWithin(call);
            ExpectFault<CallExpiredFault>(call);
        }

        // A3 (integration): with proxy parsing on, the first frame is the PROXY origin,
        // and RemoteAddressOf returns the recovered client IP; a later frame dispatches.
        [Test]
        public void ProxyPreambleRecoversClientAddressThenDispatches()
        {
            var opts = ServerLinkOptions.Default;
            opts.ExpectProxyHeader = true;
            var h = Harness.Build(ClientLinkOptions.Default, opts);

            int beat = -1;
            h.Server.OnNotify<HeartbeatNotify>((peer, n) => beat = n.Beat);

            // First frame from this peer: a raw PROXY v1 preamble (injected as bytes).
            byte[] preamble = System.Text.Encoding.ASCII.GetBytes("PROXY TCP4 9.9.9.9 5.6.7.8 51000 443\r\n");
            h.ClientLink.Deliver(new System.ArraySegment<byte>(preamble));
            // Second frame: a normal notify.
            var hb = h.ClientCatalog.Take<HeartbeatNotify>();
            hb.Beat = 12;
            h.Client.Post(hb);

            h.Server.Update(); // peer arrives, preamble consumed, then notify dispatched

            Assert.AreEqual("9.9.9.9", h.Server.RemoteAddressOf(LoopbackHub.PeerKey));
            Assert.AreEqual(12, beat);
        }

        // A3: without proxy parsing, RemoteAddressOf falls back to the raw link address.
        [Test]
        public void RemoteAddressFallsBackToRawWhenProxyDisabled()
        {
            var h = Harness.Build();
            h.Hub.RawPeerAddress = "198.51.100.5";
            h.Server.Update(); // peer arrives
            Assert.AreEqual("198.51.100.5", h.Server.RemoteAddressOf(LoopbackHub.PeerKey));
        }
#endif
    }
}
