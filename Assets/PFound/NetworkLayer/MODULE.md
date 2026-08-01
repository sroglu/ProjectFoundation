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
- MessagePack-CSharp 3.1.8 (precompiled `MessagePack.dll` / `MessagePack.Annotations.dll`, vendored in
  `Runtime/Plugins/`) + its official AOT source generator `MessagePack.SourceGenerator.dll` (a Roslyn analyzer
  under `Plugins/`, labeled RoslynAnalyzer, all platforms excluded) — generates the wire formatters at compile
  time, so serialization uses no runtime IL emit and no reflection
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
| `MessagePackBodyCodec` | Production codec: serializes each message's payload DTO through MessagePack's AOT source-generated formatters (pushed generated resolver + `BuiltinResolver`; no dynamic/contractless, no reflection). Constructed with the host's `[GeneratedMessagePackResolver]` instance(s). |
| `IMessagePayload` | Implemented by envelopes that carry a DTO: exposes `PayloadType` + `Payload` so the codec serializes the DTO, not the envelope. |
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

The production codec serializes each message's **payload DTO** through MessagePack's official AOT source
generator — every wire type is a first-party `[MessagePackObject]` DTO whose formatter is generated at compile
time (no runtime IL emit, no reflection, IL2CPP-safe). Explicit `[Key(n)]` indices make the contract
append-only forward/backward compatible. Use this discipline for anything that crosses the wire and evolves:

- **Model the payload CONTENT as an immutable value DTO** — a `[MessagePackObject] readonly partial struct`
  whose members are `init`-only `[Key(n)]` properties (`[Key(0)] public int Amount { get; init; }`). Write
  just the keyed members: the PFound generator adds value equality (`IEquatable<>` +
  `Equals`/`GetHashCode`/`ToString`); MessagePack's own generator adds the wire formatter, which member-sets
  through the `init` accessors (so no first-party constructor is needed). Because .NET Standard 2.1 lacks
  `IsExternalInit`, each DTO-authoring assembly carries a one-line `internal static class IsExternalInit` shim
  (see `GameNetworkResolver.cs`). See "Authoring a DTO" below.
- **Keep the message ENVELOPE (`RequestMessage`/`ReplyMessage` subclass) a plain poolable carrier** that holds
  the DTO in a field and implements `IMessagePayload` (the generated envelopes do this for you). The envelope
  is a runtime-only routing/pooling carrier and is **never serialized** — only its DTO is. Do NOT put
  `[MessagePackObject]` on the envelope or the base message types: they stay MessagePack-attribute-free so the
  standalone mono/csc core build keeps compiling (it excludes MessagePack).
- **Declare opcodes with the banded two-level scheme** — a central `NetDomain : byte` (one entry per
  subsystem) plus a per-domain `XxxOp : byte` with local values — and enroll via
  `Enroll<TRequest, TReply>(NetDomain.X, XxxOp.Y)` for an RPC (the request takes the op; the reply is
  opcode-less, pooled by type, decoded by correlation) or the single-type `Enroll<T>(NetDomain.X, XxxOp.Y)`
  for a notify — both fold `(domain << 8) | op` through `Opcode.Of`. Different domain ⇒ different high byte
  ⇒ cross-subsystem collisions are structurally impossible; the catalog's duplicate-enroll guard backstops
  within-domain dupes. Each operation costs ONE op value (no reply half). For a small single-subsystem game
  a single flat enum via `Enroll<T>(Enum)` is a fine simpler alternative.

The `ServerOperationFlow` module builds on exactly this discipline (its request/reply DTOs). You can write
that boilerplate by hand (above) or let the **source generator** emit it from a compact declaration (below).

## Message codegen (source generator)

A Roslyn incremental source generator turns one compact partial-class declaration per operation into the
full wire boilerplate — the immutable `[MessagePackObject]` DTO(s) with explicit `[Key]`s, the poolable
envelope(s), and the catalog enrolment — so the discipline above is applied for you and can't be gotten
subtly wrong.

> **Your operations live in `Assets/GameSpecific/Networking/`** — the canonical, copyable
> real-game reference. Exactly two folders, no per-domain/per-op nesting: `Data/` holds every shared DTO
> struct, `Operations/` holds every operation (`SpendCoinsOperation.cs`, `JoinAllianceOperation.cs`, `GetPlayerDataOperation.cs`); the
> central `NetOpcodes.cs` + `GameNetworkSetup.cs` sit at the root. To add one: right-click →
> **Create → PFound → Server Operation** inside an assembly that references `PFound.NetworkLayer` +
> `PFound.ServerOperationFlow.Core` + `MessagePack.Annotations.dll`. The minimal framework-level opcode sheet
> in `Samples/` stays as the bare example.
>
> **Where things go.** Opcodes → the one central `NetOpcodes.cs` (`NetDomain` + one op enum per domain,
> each starting `Invalid = 0`); a new op is a single line THERE. Every DTO struct → `Data/` (e.g.
> `Data/PlayerData.cs`, reused by any operation; single-value replies get their own DTO too — `SpendResult`,
> `JoinResult`). Every operation → `Operations/`. Every operation is a `[RemoteProcedure]` partial, and the
> primary way to call ANY of them is the generated uniform entry point `await <Op>.Execute(args)` — always
> generated, so every operation class stays empty (`SpendCoinsOperation`, `JoinAllianceOperation`,
> `GetPlayerDataOperation` all read the same). A **query** (`GetPlayerDataOperation`, `JoinAllianceOperation`)
> returns its reply DTO by value and is nothing more than that call. A **mutation** that must
> predict local state then reconcile it can additionally opt into a `ServerOperationFlow` subclass (validate →
> send → apply authoritative state) — that lifecycle is the advanced path (`SpendCoinsOperation` keeps one as the
> example); it reuses the same generated messages.
>
> **A reply is ALWAYS a DTO, never a bare primitive** — wrap a lone value in a single-member DTO (that is what
> `SpendResult`/`JoinResult` are) so the member keeps an explicit wire `[Key]` index and can grow append-only.
> Nested data — a DTO as a member of another DTO, or as the reply of an operation — serializes through its
> generated formatter automatically; `PlayerData` returned by `GetPlayerDataOperation` is the reference case.

### Declaring an operation

```csharp
using MessagePack;
using PFound.NetworkLayer;

// The request and reply are named [MessagePackObject] DTOs (see "Authoring a DTO").
[MessagePackObject] public readonly partial struct SpendReq    { [Key(0)] public int  Amount    { get; init; } }
[MessagePackObject] public readonly partial struct SpendResult { [Key(0)] public long NewBalance { get; init; } }

// The operation REFERENCES the DTO types — no inline fields, no synthesized structs.
[RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendReq), typeof(SpendResult))]
public partial class Spend { }

[Notify(NetDomain.Wallet, WalletOp.BalanceChanged, typeof(BalanceChangedPayload))]   // one-way, no reply
public partial class BalanceChanged { }
```

The generator emits, nested under each partial:

- `Spend.RequestMessage` / `Spend.ReplyMessage` (and `BalanceChanged.NotifyMessage`) — the poolable envelope
  that CARRIES the DTO in a settable `Content` field, implements `IMessagePayload` (so the codec serializes the
  DTO, not the envelope), exposes a zero-copy `ref readonly … View` read accessor, and overrides `Clear()` for
  pooling. The envelope is never serialized.
- `Spend.Register(catalog)` — enrols **only the request** (`ForDomain(domain).Enroll<RequestMessage,
  ReplyMessage>(op)`); the reply carries no opcode (decoded by correlation) but is registered for pooling by
  type. A notify enrols its single opcode.
- `Spend.Execute(…)` — the uniform call entry point, ALWAYS generated (the operation class is always empty; no
  `Execute` is ever hand-written). Two shapes: for a **flowless** op it takes the request DTO (`Execute(SpendReq
  request)`), builds the request envelope, sends it through the ambient `NetworkClient.Current`, awaits the
  correlated reply, and returns the reply DTO (`SpendResult`) directly — plus a convenience overload built from the
  request DTO's members (`Execute(int amount)`). For an op that owns a `<Op>Flow`, the generator instead emits an
  `Execute(SpendReq request)` (plus the same members overload) that news the flow up, runs its lifecycle, and
  returns the reply DTO (`flow.Reply.Content`) — so both shapes share the identical `Task<TReply>` signature and a
  call site never changes. A `[Notify]` gets the mirror `Notify(payload)` / `Notify(members…)` instead (fire-and-forget via
  `NetworkClient.Current.Post`).
- one assembly-wide `GeneratedMessages.RegisterAll(catalog)` that calls every generated `Register` — boot a
  catalog with a single call and no operation can be forgotten. (The hand-written `ForDomain`/`EnrollAll`
  path still works for messages you don't generate.)

### Calling an operation (primary usage)

```csharp
// once, at boot: after building the catalog + peer, publish the peer as the ambient client.
GameNetworkSetup.RegisterOperations(catalog);          // GeneratedMessages.RegisterAll(catalog)
var client = new ClientPeer(link, catalog, options);
NetworkClient.Current = client;                        // configure ONCE (GameNetworkSetup.UseAsAmbientClient)

// anywhere: every operation is called the same way, and the reply DTO comes back by await.
PlayerData player = await GetPlayerDataOperation.Execute(playerId);   // flowless op → generated direct Execute → reply DTO
await SpendCoinsOperation.Execute(50);                                // flow op → its flow-running Execute runs the lifecycle
BalanceChanged.Notify(newBalance);                            // one-way notify, fire-and-forget
```

`NetworkClient.Current` is a single ambient holder set once at startup; the generated `Execute`/`Notify` send
through it. It is left unset by default and has no defensive guard — an unconfigured call faults fast (nothing
should be null at runtime). The manual path still exists for messages you hand-write:
`await client.CallAsync<Spend.ReplyMessage>(new Spend.RequestMessage { Content = new SpendReq { Amount = 50 } })`;
a `ServerOperationFlow` subclass (the advanced lifecycle) is generic over `Spend.RequestMessage, Spend.ReplyMessage`.

### Authoring a DTO

A wire DTO (a request, a reply, a value reused across operations, a member of another DTO) is a
`[MessagePackObject] readonly partial struct` whose members are `init`-only `[Key(n)]` properties. Write just
the keyed members — the two generators supply the rest:

```csharp
using MessagePack;

[MessagePackObject]
public readonly partial struct PlayerData
{
    [Key(0)] public int    Level { get; init; }
    [Key(1)] public long   Coins { get; init; }
    [Key(2)] public string Name  { get; init; }
}
```

- The **PFound generator** adds, into the same `partial struct`: `IEquatable<PlayerData>` +
  `Equals`/`GetHashCode`/`ToString`. (It only does this for a `partial` type that does not already declare
  `IEquatable<self>`, so it never fights a hand-authored DTO.)
- **MessagePack's own source generator** adds the AOT wire formatter and collects every DTO formatter in the
  assembly into one `[MessagePack.GeneratedMessagePackResolver]`-anchored resolver (e.g. `GameNetworkResolver`,
  one per DTO-authoring assembly). The formatter member-sets through the `init` accessors on deserialize —
  which is why the DTO needs **no first-party constructor** (a generator-supplied `[SerializationConstructor]`
  would be invisible to MessagePack's generator and silently lose data — so it is deliberately NOT used).
- Explicit `[Key]` indices are append-only forward/backward compatible: add a member at a fresh higher index;
  never reuse a retired one. MessagePack's own analyzer flags key collisions.
- The host pushes the assembly's resolver into the codec once at boot —
  `new MessagePackBodyCodec(GameNetworkResolver.Instance)` (see `GameNetworkSetup.CreateCodec`). The codec
  composes the pushed resolver(s) ahead of `BuiltinResolver` **only** — no dynamic/contractless resolver, so
  every wire type is either a generated DTO or a builtin. No reflection discovery.

### Retired-index discipline (wire versioning)

Explicit `[Key]` indices are **append-only**: gaps are legal, but a deleted member's number must never be
reused (an old peer's bytes would be read as the new member). Add a member at a fresh higher index; leave the
retired number's gap permanently (a source comment records why). MessagePack's own analyzer flags a duplicate
`[Key]`; cross-version reuse is a review-time discipline, not an attribute.

### Wiring the generators into a Unity project

Two Roslyn analyzers run at compile time, both labelled **RoslynAnalyzer** (all platforms disabled — they run
in the compiler, not at runtime), both applied globally because they sit **outside any `.asmdef` folder**:

- `Plugins/PFound.NetworkLayer.Generator.dll` — the PFound generators: expands `[RemoteProcedure]`/`[Notify]`
  partials into their envelopes/enrolment/`Execute`, and adds value equality + `ToString` to `[MessagePackObject]`
  DTOs.
- `Plugins/MessagePack.SourceGenerator.dll` — MessagePack's official AOT generator: emits the wire formatter
  for each `[MessagePackObject]` DTO and the per-assembly `[GeneratedMessagePackResolver]`-anchored resolver.

A DTO/operation-authoring assembly must reference `PFound.NetworkLayer` (for the attributes + `MessageCatalog`
+ `IMessagePayload`), `MessagePack.Annotations.dll` (for `[MessagePackObject]`/`[Key]`), and **`MessagePack.dll`**
(for the generated resolver's `IFormatterResolver`/formatter types). It must also declare one
`[MessagePack.GeneratedMessagePackResolver] public partial class <Name> { }` anchor and a one-line
`internal static class IsExternalInit` shim (for `init` on .NET Standard 2.1). `Assets/GameSpecific/Networking/`
(`GameNetworkResolver.cs`) is the copyable reference.

> **IL2CPP note:** serialization is fully AOT (generated formatters, no runtime IL, no reflection resolver
> discovery), so no `link.xml` is needed for the wire path. Keep the resolver push explicit
> (`new MessagePackBodyCodec(GameNetworkResolver.Instance)`) rather than any reflection scan.

Rebuild the PFound generator DLL from `Generator~/` (a Unity-ignored folder) with `dotnet build -c Release`, or
`./build.sh` when no .NET SDK is present (compiles with mono `csc` against the Roslyn 4.3 netstandard2.0
reference assemblies — the version Unity 6000.3 hosts; System.Collections.Immutable is pinned to 6.0.0 to
match, or the analyzer silently fails to load). MessagePack's analyzer is vendored as-is from its NuGet
package. `Assets/GameSpecific/Networking/` is the runnable real-game reference (with EditMode round-trip +
catalog tests under its `Tests/`).

## File Structure
```
NetworkLayer/
  Runtime/                       # assembly PFound.NetworkLayer
    Messaging/                   # Message hierarchy, MessageCatalog, MessageContractAttribute, IBodyCodec
    Codegen/                     # [RemoteProcedure]/[Notify] attributes (DTOs use MessagePack's [MessagePackObject]/[Key])
    Endpoints/                   # ClientPeer, NetworkClient (ambient holder), ServerPeer (#if BACKEND), RequestExchange<T>, EarlyArrivalBuffer
    Transports/                  # IClientLink/IServerLink, Telepathy links, Loopback links + hub, LatencyShapedLink
    Serialization/               # MessagePackBodyCodec (prod), ReflectionBodyCodec (test)
    Config/                      # ClientLinkOptions / ServerLinkOptions
    Core/                        # FrameCodec, IClock/MonotonicClock, wire enums, PROXY-protocol parsing
    Diagnostics/                 # NetLog, CallLatencyStats (client), ServerMetrics/ServerDiagnostics (server-only)
    Telepathy/                   # vendored MIT TCP library (sockets/threads/framing)
    Plugins/                     # vendored MessagePack-CSharp
  Plugins/                       # PFound.NetworkLayer.Generator.dll (RoslynAnalyzer, global source generator)
  Generator~/                    # generator source + .csproj + build.sh (Unity-ignored; builds the DLL above)
  Samples/                       # minimal framework-level opcode sheet (hand-written messages)
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
