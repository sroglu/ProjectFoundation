# NetworkLayer

> **Module group — Networking.** Single-module group. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose
Transport-agnostic request/reply/notify messaging layer for Unity clients and dedicated servers.
Typed messages are bound to opcodes in a shared catalog, packed into length-prefixed frames, and
carried over a pluggable link (in-process loopback for tests, Telepathy TCP in production). The
messaging core only ever sees opaque byte frames — it knows nothing about sockets. All delivery is
single-threaded and pump-driven via a per-frame `Update()`.

## Notify vs in-process Signaling (scope note)

NetworkLayer `notify` (`Post`/`OnNotify`, one-way, opcode-bound, MessagePack-serialized over the link)
crosses a transport to a client/server — do not confuse it with the in-process event bus. For app-wide
payload-free notifications between C# systems on one machine use `PFound.Signaling`; for data-carrying
events inside an ECS `World` use `World.Events`. This module is strictly the across-the-wire path.

**Transport choice (socket vs HTTP):** NetworkLayer is realtime, bidirectional, connection-oriented
messaging over a socket (Telepathy TCP) — use it for live client↔server gameplay/session traffic. For
one-shot request/response **fetch over HTTP**, use the BestHTTP-based modules instead:
`PFound.ContentDelivery` (assets/bundles), `PFound.RemoteGameConfig` (config values),
`PFound.RemoteResourceCache` (generic cached blobs).

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
| `Opcode` | `Opcode.Of(domain, op)` folds a banded two-level opcode: `(domain << 8) | op` (high-byte domain + low-byte op). |
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

**Catalog (`MessageCatalog`):** three enroll overloads —
`Enroll<T>(Enum domain, Enum op)` (the **banded** form: a central `NetDomain : byte` + a per-domain
`XxxOp : byte`, folded to `(domain << 8) | op` via `Opcode.Of` so cross-subsystem collisions are
structurally impossible — recommended for a multi-subsystem game), `Enroll<T>(Enum opcode)` (a single
flat per-domain opcode enum, narrowed to `ushort` — fine for a small game), or `Enroll<T>(ushort opcode)`
(a raw number) — plus two bulk-enrol conveniences over the banded form: `ForDomain(domain)` returns a
chainable `DomainEnroller` so the domain is written once and ops chain
(`ForDomain(NetDomain.Wallet).Enroll<SpendReq>(WalletOp.Spend).Enroll<SpendReply>(WalletOp.SpendReply)`),
and `EnrollAll(params Action<MessageCatalog>[])` runs several per-domain enrol blocks in one call.
Also `HarvestContracts(assembly)` (reads `[MessageContract(opcode)]`);
`Take<T>()` / `Recycle(msg)` (pooling); `OpcodeFor(type)` / `TypeFor(opcode)`. A request and its reply
are DISTINCT entries (the reply frame carries its own opcode) — enroll both with their own op values.
Raw composition without the catalog: `Opcode.Of(domain, op)` (`Convert.ToByte` throws on an out-of-range
byte → fail-fast).

### The one-file opcode sheet (recommended convention)

Keep the whole game's wire opcodes in **one `.cs` file** — every `NetDomain` band and every per-domain
op enum side by side — so collisions are visible at a glance instead of scattered across subsystems.
`ForDomain` + `EnrollAll` let that one file also own the wiring:

```csharp
// GameMessages.cs — the whole game's opcode sheet in one file
public enum NetDomain : byte { Wallet = 1, Alliance = 2, Reward = 3 }   // value = opcode HIGH byte (0x01 / 0x02 / 0x03)
public enum WalletOp   : byte { Spend = 1, SpendReply = 2, Grant = 3, GrantReply = 4 }
public enum AllianceOp : byte { Join = 1, JoinReply = 2 }
public enum RewardOp   : byte { Grant = 1, GrantReply = 2 }

public static class GameMessages
{
    public static void RegisterAll(MessageCatalog c) => c.EnrollAll(Wallet, Alliance, Reward);
    static void Wallet(MessageCatalog c)   => c.ForDomain(NetDomain.Wallet)
        .Enroll<SpendCoinsRequest>(WalletOp.Spend).Enroll<SpendCoinsReply>(WalletOp.SpendReply)
        .Enroll<GrantCoinsRequest>(WalletOp.Grant).Enroll<GrantCoinsReply>(WalletOp.GrantReply);
    static void Alliance(MessageCatalog c) => c.ForDomain(NetDomain.Alliance)
        .Enroll<JoinAllianceRequest>(AllianceOp.Join).Enroll<JoinAllianceReply>(AllianceOp.JoinReply);
    static void Reward(MessageCatalog c)   => c.ForDomain(NetDomain.Reward)
        .Enroll<GrantRewardRequest>(RewardOp.Grant).Enroll<GrantRewardReply>(RewardOp.GrantReply);
}
// usage: GameMessages.RegisterAll(catalog);
```

The per-call `catalog.Enroll<T>(NetDomain.X, XxxOp.Y)` form (see the wiring example above) stays valid —
`ForDomain`/`EnrollAll` are just the collision-visible convenience for a game with several subsystems.

## Setup / wiring

Pure library — everything is `new X()`; there is no MonoBehaviour, scene object, or singleton. The
consumer owns the peer and calls `Update()` every frame from its host loop. Both ends need the same
catalog registration (same opcode→type mapping) for frames to decode.

```csharp
// --- shared: one catalog, one codec, same on both ends ---
// Banded two-level opcodes: ONE tiny central domain list (eyeball it for collisions), then each
// domain owns a small op enum with LOCAL values. opcode = (domain << 8) | op.
enum NetDomain : byte { Movement = 1, Chat = 2 }  // central; value = opcode HIGH byte (0x01 / 0x02)
enum MoveOp    : byte { Request  = 1,    Reply = 2 }     // per-domain, local values (1..255)
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest>(NetDomain.Movement, MoveOp.Request);   // banded overload (recommended)
catalog.Enroll<MoveReply>(NetDomain.Movement, MoveOp.Reply);
catalog.Enroll<ChatNotify>(NetDomain.Chat, ChatOp.Say);
// Different domain ⇒ different high byte ⇒ Movement and Chat can never collide.
// Alternatives: catalog.Enroll<MoveReply>(SomeFlatOp.Reply)  // single flat enum, narrowed to ushort
//               catalog.Enroll<MoveReply>((ushort)0x1002)    // raw number
//               catalog.HarvestContracts(assembly);          // reads [MessageContract(opcode)]

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

## Wire contract discipline (stable messages across independent deploys)

The default codec is MessagePack **contractless** (plain fields "just work"), which is convenient but
serializes by member order/name — fragile once the client and server deploy independently. For anything
that crosses the wire and evolves, use this discipline instead:

- **Model the payload CONTENT as an immutable value DTO** — a `readonly struct` marked
  `[MessagePackObject]` with an explicit `[Key(n)]` on every field + `[SerializationConstructor]`, and
  `IEquatable<>` for value-equality. Explicit keys are append-only forward/backward compatible, and
  `[MessagePackObject]` makes a forgotten key a hard error. The `ContractlessStandardResolver` routes
  attributed types through the stable path automatically — **no codec change**.
- **Keep the message ENVELOPE (`RequestMessage`/`ReplyMessage` subclass) a plain poolable class** that
  carries the DTO as a field. Do NOT put `[MessagePackObject]` on the envelope or the base message types:
  the base types stay MessagePack-attribute-free so the standalone mono/csc core build keeps compiling
  (it excludes MessagePack). The envelope's only members are the base `Status` + the DTO, so its
  contractless-ness is a framework constant, not a per-message churn point.
- **Declare opcodes with the banded two-level scheme** — a central `NetDomain : byte` (one entry per
  subsystem) plus a per-domain `XxxOp : byte` with local values — and enroll via
  `Enroll<T>(NetDomain.X, XxxOp.Y)` (which folds `(domain << 8) | op` through `Opcode.Of`). Different
  domain ⇒ different high byte ⇒ cross-subsystem collisions are structurally impossible; the catalog's
  duplicate-enroll guard backstops within-domain dupes. Request and reply get distinct op values. For a
  small single-subsystem game a single flat enum via `Enroll<T>(Enum)` is a fine simpler alternative.

The `ServerOperation` module builds on exactly this discipline (its request/reply DTOs), and a future
codegen phase will emit the attributed DTOs from field declarations automatically.

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
