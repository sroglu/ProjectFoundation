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

**Catalog (`MessageCatalog`):** the **recommended RPC enrolment is the request/reply PAIR overload** —
`Enroll<TRequest, TReply>(Enum domain, Enum op)`: the request takes the banded opcode; the reply is
registered for pooling by TYPE only (opcode-less — see the reply-model note below). This declares an
operation's request and reply together in one call, so each operation costs exactly one op value. The
single-type overloads enroll ONE message (a notify, or a hand-written message) against an opcode:
`Enroll<T>(Enum domain, Enum op)` (the **banded** form: a central `NetDomain : byte` + a per-domain
`XxxOp : byte`, folded to `(domain << 8) | op` via `Opcode.Of` so cross-subsystem collisions are
structurally impossible — recommended for a multi-subsystem game), `Enroll<T>(Enum opcode)` (a single
flat per-domain opcode enum, narrowed to `ushort` — fine for a small game), or `Enroll<T>(ushort opcode)`
(a raw number) — plus two bulk-enrol conveniences: `ForDomain(domain)` returns a chainable `DomainEnroller`
so the domain is written once and ops chain — it carries BOTH the pair form and the single-type form
(`ForDomain(NetDomain.Wallet).Enroll<SpendRequest, SpendReply>(WalletOp.Spend).Enroll<BalanceNotify>(WalletOp.BalanceChanged)`)
— and `EnrollAll(params Action<MessageCatalog>[])` runs several per-domain enrol blocks in one call.
Also `RegisterReplyType<T>()` (pool a reply type with no opcode — what the pair overload calls under the
hood; use it directly to pool a hand-written reply); `HarvestContracts(assembly)` (reads
`[MessageContract(opcode)]`); `Take<T>()` / `Recycle(msg)` (pooling); `OpcodeFor(type)` / `TypeFor(opcode)`.

**Reply-model note (replies are un-enrolled).** A reply carries NO opcode: it is decoded by CORRELATION —
the outstanding call already knows the reply TYPE (`CallAsync<TReply>` stored `typeof(TReply)`), so the
reply frame needs no opcode entry. Only requests and notifies are enrolled against opcodes; the full
per-domain op space (1..255) therefore serves requests + notifies with no reserved "reply half". Replies
are only registered for POOLING by type (`RegisterReplyType<T>()`, done for you by the pair overload).
Raw composition without the catalog: `Opcode.Of(domain, op)` (`Convert.ToByte` throws on an out-of-range
byte → fail-fast).

### The one-file opcode sheet (recommended convention)

Keep the whole game's wire opcodes in **one `.cs` file** — every `NetDomain` band and every per-domain
op enum side by side — so collisions are visible at a glance instead of scattered across subsystems.
`ForDomain` + `EnrollAll` let that one file also own the wiring:

```csharp
// GameMessages.cs — the whole game's opcode sheet in one file
public enum NetDomain : byte { Wallet = 1, Alliance = 2, Reward = 3 }   // value = opcode HIGH byte (0x01 / 0x02 / 0x03)
// One op per operation — replies are opcode-less (decoded by correlation), so there is NO reply op value.
public enum WalletOp   : byte { Spend = 1, Grant = 2, BalanceChanged = 3 }
public enum AllianceOp : byte { Join = 1 }
public enum RewardOp   : byte { Grant = 1 }

public static class GameMessages
{
    public static void RegisterAll(MessageCatalog c) => c.EnrollAll(Wallet, Alliance, Reward);
    static void Wallet(MessageCatalog c)   => c.ForDomain(NetDomain.Wallet)
        .Enroll<SpendCoinsRequest, SpendCoinsReply>(WalletOp.Spend)   // RPC → pair overload
        .Enroll<GrantCoinsRequest, GrantCoinsReply>(WalletOp.Grant)
        .Enroll<BalanceChangedNotify>(WalletOp.BalanceChanged);       // notify → single-type
    static void Alliance(MessageCatalog c) => c.ForDomain(NetDomain.Alliance)
        .Enroll<JoinAllianceRequest, JoinAllianceReply>(AllianceOp.Join);
    static void Reward(MessageCatalog c)   => c.ForDomain(NetDomain.Reward)
        .Enroll<GrantRewardRequest, GrantRewardReply>(RewardOp.Grant);
}
// usage: GameMessages.RegisterAll(catalog);
```

The per-call `catalog.Enroll<TRequest, TReply>(NetDomain.X, XxxOp.Y)` pair form (and the single-type
`Enroll<T>(NetDomain.X, XxxOp.Y)` for a notify — see the wiring example above) stays valid —
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
enum MoveOp    : byte { Move = 1 }     // per-domain, local values (1..255); one op per operation
enum ChatOp    : byte { Say = 1 }      // the reply has NO op — it is decoded by correlation
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest, MoveReply>(NetDomain.Movement, MoveOp.Move);   // pair overload (recommended for RPC)
catalog.Enroll<ChatNotify>(NetDomain.Chat, ChatOp.Say);                    // single-type for a notify
// Different domain ⇒ different high byte ⇒ Movement and Chat can never collide.
// Alternatives: catalog.Enroll<ChatNotify>(SomeFlatOp.Say)  // single flat enum, narrowed to ushort
//               catalog.Enroll<ChatNotify>((ushort)0x0201)  // raw number
//               catalog.HarvestContracts(assembly);         // reads [MessageContract(opcode)]

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
  `Enroll<TRequest, TReply>(NetDomain.X, XxxOp.Y)` for an RPC (the request takes the op; the reply is
  opcode-less, pooled by type, decoded by correlation) or the single-type `Enroll<T>(NetDomain.X, XxxOp.Y)`
  for a notify — both fold `(domain << 8) | op` through `Opcode.Of`. Different domain ⇒ different high byte
  ⇒ cross-subsystem collisions are structurally impossible; the catalog's duplicate-enroll guard backstops
  within-domain dupes. Each operation costs ONE op value (no reply half). For a small single-subsystem game
  a single flat enum via `Enroll<T>(Enum)` is a fine simpler alternative.

The `ServerOperation` module builds on exactly this discipline (its request/reply DTOs). You can write
that boilerplate by hand (above) or let the **source generator** emit it from a compact declaration (below).

## Message codegen (source generator)

A Roslyn incremental source generator turns one compact partial-class declaration per operation into the
full wire boilerplate — the immutable `[MessagePackObject]` DTO(s) with explicit `[Key]`s, the poolable
envelope(s), and the catalog enrolment — so the discipline above is applied for you and can't be gotten
subtly wrong.

### Declaring an operation

```csharp
using PFound.NetworkLayer;

[NetworkOp(NetDomain.Wallet, WalletOp.Spend)]     // an RPC: request + reply
public partial class Spend
{
    [Request(0)] public int  Amount;      // request payload fields, explicit wire indices
    [Request(1)] public long AccountId;
    [Reply(0)]   public long NewBalance;  // reply payload fields
}

[NetworkNotify(NetDomain.Wallet, WalletOp.BalanceChanged)]   // one-way notify, no reply
public partial class BalanceChanged
{
    [Field(0)] public long NewBalance;
}
```

The generator emits, nested under each partial (names derived by convention):

- `Spend.Req` / `Spend.Reply` (and `BalanceChanged.Data`) — immutable `[MessagePackObject] readonly struct`
  with `[Key(n)]` taken **verbatim from the `[Request/Reply/Field(n)]` index** (not declaration order),
  `[SerializationConstructor]`, `IEquatable<>`, and a value `ToString`.
- `Spend.RequestMessage` / `Spend.ReplyMessage` (and `BalanceChanged.NotifyMessage`) — the poolable
  envelope carrying a settable `Content` field plus a zero-copy `ref readonly … View` read accessor, and a
  `Clear()` override for pooling.
- `Spend.Register(catalog)` — enrols **only the request** (`ForDomain(domain).Enroll<RequestMessage,
  ReplyMessage>(op)`); the reply carries no opcode (decoded by correlation) but is registered for pooling by
  type. A notify enrols its single opcode.
- one assembly-wide `GeneratedMessages.RegisterAll(catalog)` that calls every generated `Register` — boot a
  catalog with a single call and no operation can be forgotten. (The hand-written `ForDomain`/`EnrollAll`
  path still works for messages you don't generate.)

Usage mirrors the hand-written path: `new Spend.RequestMessage { Content = new Spend.Req(50, id) }`; the
`ServerOperation` subclass is generic over `Spend.RequestMessage, Spend.ReplyMessage`.

### `[Reserved]` — retired-index discipline (wire versioning)

Explicit indices are **append-only**: gaps are legal, but a deleted field's number must never be reused (an
old peer's bytes would be read as the new field). Record retired numbers on the type so the source itself is
the history:

```csharp
[NetworkOp(NetDomain.Wallet, WalletOp.Spend)]
[Reserved(1)]                 // AccountId used to live at index 1 — never reuse
public partial class Spend { [Request(0)] public int Amount; [Request(2)] public string Note; }
```

The compiler has no memory of deleted fields, so without a `[Reserved]` marker there is no cross-version
protection. (A companion analyzer that enforces reserved-index reuse, duplicate indices, and opcode
collisions is a separate later piece; the `[Reserved]` attribute already ships so the intent is recorded.)

### Wiring the generator into a Unity project

The generator ships as `Plugins/PFound.NetworkLayer.Generator.dll`, labelled **RoslynAnalyzer** (all
platforms disabled — it runs in the compiler, not at runtime). Because it sits **outside any `.asmdef`
folder**, Unity applies it globally to every user assembly, so any assembly that declares `[NetworkOp]` /
`[NetworkNotify]` partials gets its types generated with no per-assembly wiring. The assembly that declares
the partials must reference `PFound.NetworkLayer` (for the attributes + `MessageCatalog`) and
`MessagePack.Annotations.dll` (for the `[Key]`/`[MessagePackObject]` attributes on the generated structs).

Rebuild the DLL from `Generator~/` (a Unity-ignored folder) with `dotnet build -c Release`, or `./build.sh`
when no .NET SDK is present (compiles with mono `csc` against the Roslyn 4.3 netstandard2.0 reference
assemblies — the version Unity 6000.3 hosts; System.Collections.Immutable is pinned to 6.0.0 to match, or
the analyzer silently fails to load). `CodegenSample/` is a runnable example + EditMode round-trip test.

## File Structure
```
NetworkLayer/
  Runtime/                       # assembly PFound.NetworkLayer
    Messaging/                   # Message hierarchy, MessageCatalog, MessageContractAttribute, IBodyCodec
    Codegen/                     # [NetworkOp]/[NetworkNotify]/[Request]/[Reply]/[Field]/[Reserved] attributes
    Endpoints/                   # ClientPeer, ServerPeer (#if BACKEND), RequestExchange<T>, EarlyArrivalBuffer
    Transports/                  # IClientLink/IServerLink, Telepathy links, Loopback links + hub, LatencyShapedLink
    Serialization/               # MessagePackBodyCodec (prod), ReflectionBodyCodec (test)
    Config/                      # ClientLinkOptions / ServerLinkOptions
    Core/                        # FrameCodec, IClock/MonotonicClock, wire enums, PROXY-protocol parsing
    Diagnostics/                 # NetLog, CallLatencyStats (client), ServerMetrics/ServerDiagnostics (server-only)
    Telepathy/                   # vendored MIT TCP library (sockets/threads/framing)
    Plugins/                     # vendored MessagePack-CSharp
  Plugins/                       # PFound.NetworkLayer.Generator.dll (RoslynAnalyzer, global source generator)
  Generator~/                    # generator source + .csproj + build.sh (Unity-ignored; builds the DLL above)
  CodegenSample/                 # runnable [NetworkOp]/[NetworkNotify] example + EditMode round-trip test
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
