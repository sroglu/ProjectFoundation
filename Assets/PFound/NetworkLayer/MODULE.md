# NetworkLayer

## Purpose
Transport-agnostic request/reply/notify messaging layer for Unity clients and dedicated servers.
Typed messages are bound to opcodes in a shared catalog, packed into length-prefixed frames, and
carried over a pluggable link (in-process loopback for tests, Telepathy TCP in production). The
messaging core only ever sees opaque byte frames — it knows nothing about sockets. All delivery is
single-threaded and pump-driven via a per-frame `Update()`.

## Assemblies
| Assembly | Location | Notes |
|---|---|---|
| `PFound.NetworkLayer` | `Runtime/` | Foundation layer — no deps on other `PFound.*` assemblies. |
| `PFound.NetworkLayer.Tests.EditAndPlayModes` | `Tests/EditAndPlayModes/` | Edit + play mode tests. |

Namespace: `PFound.NetworkLayer` (vendored Telepathy code lives under `PFound.NetworkLayer.Telepathy`).

## Dependencies
- MessagePack-CSharp (precompiled `MessagePack.dll` / `MessagePack.Annotations.dll`, vendored in `Runtime/Plugins/`)
- Telepathy (vendored MIT TCP library, `Runtime/Telepathy/`) — not called directly by consumers
- ZString (asmdef reference)

This module does **not** use BestHTTP. It is realtime TCP messaging via Telepathy. (BestHTTP is the
HTTP transport for the ContentDelivery / RemoteResourceCache modules, not for NetworkLayer.)

## Key Types

### Messaging (`Runtime/Messaging/`)
| Type | Description |
|---|---|
| `Message` | Abstract payload root. Poolable — override `Clear()` to wipe fields. |
| `RequestMessage` | Abstract base for requests (expects a correlated reply). |
| `ReplyMessage` | Abstract base for replies. Carries a `ReplyStatus Status`. |
| `NotifyMessage` | Abstract base for fire-and-forget notifications. |
| `MessageCatalog` | Binds a `ushort` opcode to each message type; shared by both peers. Also owns the pool and codec. |
| `MessageContractAttribute` | `[MessageContract(opcode)]` for reflection-based catalog population. |
| `IBodyCodec` | Payload serialization seam. |

### Endpoints (`Runtime/Endpoints/`)
| Type | Description |
|---|---|
| `ClientPeer` | Client messaging engine: RPC correlation, notify routing, deadlines, per-opcode round-trip latency (`Latency`). |
| `ServerPeer` | Server messaging engine (`#if BACKEND`): handlers, deferred requests, broadcast, watchdog. |
| `RequestExchange<TRequest>` | Deferred-reply handle (with `RequestExchangeBase`). |
| `EarlyArrivalBuffer` | Buffers frames that arrive before their handler is ready. |

### Transports (`Runtime/Transports/`)
| Type | Description |
|---|---|
| `IClientLink` / `IServerLink` | Transport seam: `Open`/`Close`, `Deliver` (queue a frame), `Pump(budget)` (drain inbound, raise `Received`). |
| `TelepathyClientLink` | TCP client link via Telepathy. |
| `TelepathyServerLink` | TCP server link via Telepathy (`#if BACKEND`). |
| `LoopbackClientLink` / `LoopbackServerLink` / `LoopbackHub` | In-process loopback transport for tests (no sockets). |
| `LatencyShapedLink` | Wraps a client link to inject delay for timeout/reconnect tests. |

### Serialization (`Runtime/Serialization/`)
| Type | Description |
|---|---|
| `MessagePackBodyCodec` | Production codec (contractless MessagePack). |
| `ReflectionBodyCodec` | Codec for the standalone mono/csc test build (no MessagePack facades). |

### Core / Config / Diagnostics
| Type | Description |
|---|---|
| `FrameCodec` | Static wire framing (length-prefixed envelopes). |
| `IClock` / `MonotonicClock` | Time source for deadlines. |
| `ReplyStatus` (`Core/WireEnums.cs`) | Reply status wire enum (`byte`). |
| `ClientLinkOptions` / `ServerLinkOptions` | Tunables, each with a static `.Default`. |
| `NetLog` | Logging (Unity-side logging under `#if UNITY`). |
| `CallLatencyStats` | Client-side per-opcode round-trip latency (count / min / max / average) + a slow-call event. Engine-free; on `ClientPeer.Latency`. |
| `ServerMetrics` / `ServerDiagnostics` | Server-only metrics/diagnostics (`#if BACKEND`). Server *service-time* per opcode (a narrower view than the client's full round-trip). |

Game projects extend `RequestMessage` / `ReplyMessage` / `NotifyMessage` with their own message
types and enroll them (per type) in the shared `MessageCatalog`.

## Public API

**Client (`ClientPeer`):** `Connect(host, port)` / `ConnectAsync(host, port, timeoutMs)` /
`Reconnect()` / `ReconnectAsync(timeoutMs)` / `Disconnect()`; `Task<TReply> CallAsync<TReply>(RequestMessage, deadlineMs)`;
`Post(NotifyMessage)`; `OnNotify<T>(Action<T>)`; `Update(budget)` (call once per frame);
`IsConnected`, `OutstandingCallCount`, `RemoteHost`/`RemotePort`, `Connected`/`Disconnected` events;
`Latency` (per-opcode round-trip stats + slow-call event, see below).

**Client latency telemetry (`ClientPeer.Latency` → `CallLatencyStats`):** every request/reply
round-trip is timed per opcode, reusing the call-token correlation. `PerOpcode` /
`TryGet(opcode, out OpcodeLatency)` expose `Completed` (count), `MinMs`, `MaxMs`, `SumMs`, and the
derived `AverageMs`. Set `ClientLinkOptions.SlowCallThresholdMs` (or `Latency.SlowCallThresholdMs`
at runtime) to raise `Latency.SlowCall` (a `SlowCallReport` of opcode + actual round-trip +
threshold) the instant a completed call crosses that ceiling — the legacy request-time-exceeded
callback, now per opcode with the real round-trip. `SlowCallCount` tallies the crossings; `Reset()`
clears the table. The measurement covers the network hop *and* the server's service time, so it is a
strictly wider view than the server-only `ServerDiagnostics` service-time record.

**Server (`ServerPeer`, `#if BACKEND`):** `Listen(port)` / `Halt()` / `Kick(peer)`;
`Handle<TReq,TReply>(Func<int,TReq,TReply>)` (synchronous reply); `HandleDeferred<TReq>(Action<RequestExchange<TReq>>)`
+ `FulfillDeferred(exchange, reply)` (answer later); `OnNotify<T>(Action<int,T>)`;
`SendTo(peer, notify)` / `Broadcast(notify)` / `SendToMany(peers, notify)`; `Update(budget)`;
`IsListening`, `Peers`, `Metrics`, `Diagnostics`, `PeerConnected`/`PeerDisconnected` events.

**Catalog (`MessageCatalog`):** `Enroll<T>(opcode)` (explicit) or `HarvestContracts(assembly)`
(reads `[MessageContract(opcode)]`); `Take<T>()` / `Recycle(msg)` (pooling);
`OpcodeFor(type)` / `TypeFor(opcode)`.

## Setup / wiring

Pure library — everything is `new X()`; there is no MonoBehaviour, scene object, or singleton. The
consumer owns the peer and calls `Update()` every frame from its host loop. Both ends need the same
catalog registration (same opcode→type mapping) for frames to decode.

```csharp
// --- shared: one catalog, one codec, same on both ends ---
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest>(1042);
catalog.Enroll<MoveReply>(1043);
catalog.Enroll<ChatNotify>(2001);
// or: catalog.HarvestContracts(assembly);   // reads [MessageContract(opcode)]

// --- client ---
var options = ClientLinkOptions.Default;
var link    = new TelepathyClientLink(options);        // TCP transport
var client  = new ClientPeer(link, catalog, options);
client.OnNotify<ChatNotify>(n => ShowChat(n));
await client.ConnectAsync("game.example.com", 7777);

MoveReply reply = await client.CallAsync<MoveReply>(new MoveRequest { X = 3 });
client.Post(new ChatNotify { Text = "hi" });

// per frame:
void Update() => client.Update();
```

## File Structure
```
NetworkLayer/
  Runtime/                       # assembly PFound.NetworkLayer
    Messaging/                   # Message hierarchy, MessageCatalog, MessageContractAttribute, IBodyCodec
    Endpoints/                   # ClientPeer, ServerPeer (#if BACKEND), RequestExchange<T>, EarlyArrivalBuffer
    Transports/                  # IClientLink/IServerLink, Telepathy links, Loopback links + hub, LatencyShapedLink
    Serialization/               # MessagePackBodyCodec (prod), ReflectionBodyCodec (test)
    Config/                      # ClientLinkOptions / ServerLinkOptions
    Core/                        # FrameCodec, IClock/MonotonicClock, wire enums, PROXY-protocol parsing
    Diagnostics/                 # NetLog, CallLatencyStats (client), ServerMetrics/ServerDiagnostics (server-only)
    Telepathy/                   # vendored MIT TCP library (sockets/threads/framing)
    Plugins/                     # vendored MessagePack-CSharp
  Tests/EditAndPlayModes/        # assembly PFound.NetworkLayer.Tests.EditAndPlayModes
  Tests/Standalone/              # csc/mono runner for engine-free metrics (guarded by PF_STANDALONE_TESTS)
  MODULE.md / README.md / THIRD-PARTY-NOTICES.md
```

## Downstream Dependents
None within PFound — no other `PFound.*` module references NetworkLayer. Game projects are the
consumers; they own the peer, define message types, and drive `Update()`.

## Limitations / Known Gaps
- Single-threaded, pump-driven: no delivery happens without a per-frame `Update()` call; a stalled
  host loop stalls the peer.
- Both ends must share an identical opcode→type catalog; a mismatch fails to decode frames.
- The standalone mono/csc test build cannot use `MessagePackBodyCodec` (facade deps) and falls back
  to `ReflectionBodyCodec`; only Unity/full-runtime builds exercise the production codec.

## Conditional Compilation
- `#if BACKEND` — Server-only code (`ServerPeer`, `TelepathyServerLink`, server metrics/diagnostics).
  A client build must NOT define it, so server internals never ship to players. Define it only in the
  dedicated-server assembly/build.
- `#if UNITY` — Unity-side logging in `NetLog` (with a `!UNITY` fallback).
- `#if DEBUGABLES` — Extra diagnostic hooks.

The production codec (`MessagePackBodyCodec`) pulls in `netstandard`/`System.Memory` facades that a
bare `csc`/`mono` build lacks, so the standalone test runner uses `ReflectionBodyCodec` instead;
Unity references the MessagePack assembly directly and uses `MessagePackBodyCodec`. The framing/
dispatch core is codec-agnostic, so the swap changes only the payload bytes.

## Extending for Game Projects
1. Define message types extending `RequestMessage`, `ReplyMessage`, `NotifyMessage` (override `Clear()` for pooling).
2. Assign an opcode per message type and enroll it in the shared `MessageCatalog` (or use `[MessageContract(opcode)]` + `HarvestContracts`).
3. Register handlers on the server (`Handle` / `HandleDeferred` / `OnNotify`) and notify subscriptions on the client (`OnNotify`).

## Testing
Use the loopback transport for deterministic, socket-free tests: one `LoopbackHub`, a
`LoopbackClientLink(hub)` on a `ClientPeer` and a `LoopbackServerLink(hub)` on a `ServerPeer`, driven
by `Update()`. `LatencyShapedLink` wraps a link to inject delay for timeout/reconnect tests.
Tests live in `Tests/EditAndPlayModes/` (assembly `PFound.NetworkLayer.Tests.EditAndPlayModes`).

The engine-free client latency telemetry (`CallLatencyStats` + the `OutstandingCalls.Closed`
fan-out) is also covered by a standalone csc/mono runner in `Tests/Standalone/`, guarded by the
`PF_STANDALONE_TESTS` define so Unity skips it:
```
csc -nologo -warn:0 -define:PF_STANDALONE_TESTS -out:/tmp/pf_net.exe \
    Assets/PFound/NetworkLayer/Runtime/Diagnostics/CallLatencyStats.cs \
    Assets/PFound/NetworkLayer/Runtime/Messaging/OutstandingCalls.cs \
    Assets/PFound/NetworkLayer/Runtime/Messaging/Message.cs \
    Assets/PFound/NetworkLayer/Runtime/Core/WireEnums.cs \
    Assets/PFound/NetworkLayer/Runtime/Core/NetworkExceptions.cs \
    Assets/PFound/NetworkLayer/Tests/Standalone/CallLatencyStandaloneTests.cs \
&& mono /tmp/pf_net.exe
```
