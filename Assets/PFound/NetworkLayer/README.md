# PFound.NetworkLayer

A transport-agnostic request/reply/notify messaging layer for Unity clients and dedicated
servers. Typed messages are bound to opcodes in a shared catalog, packed into length-prefixed
frames, and carried over a pluggable link (in-process loopback for tests, Telepathy TCP in
production). The messaging core knows nothing about sockets — it only ever sees opaque byte frames.

## Model

- **Message** (`abstract`) is the root of the payload model game projects extend:
  `RequestMessage` (expects a correlated reply), `ReplyMessage` (carries a `ReplyStatus`),
  `NotifyMessage` (fire-and-forget). Instances are poolable — override `Clear()` to wipe fields.
- **Opcode catalog** — `MessageCatalog` binds a `ushort` opcode to each message type; both peers
  share the same catalog so the sender's opcode resolves to the receiver's type.
- **Link** — `IClientLink` / `IServerLink` are the transport seam: `Open`/`Close`, `Deliver`
  (queue a frame), and `Pump(budget)` (drain inbound frames, raising `Received`). All delivery
  is single-threaded and pump-driven — nothing fires off the host tick.
- **Peer** — `ClientPeer` and `ServerPeer` are the messaging engines that own RPC correlation,
  notify routing, deadlines, and (server) the deferred-request watchdog.

## Sub-modules (`Runtime/`)

| Folder | Contents |
|---|---|
| `Messaging/` | `Message`/`RequestMessage`/`ReplyMessage`/`NotifyMessage`, `MessageCatalog`, `MessageContractAttribute`, `IBodyCodec`, outstanding-call/token bookkeeping. |
| `Endpoints/` | `ClientPeer`, `ServerPeer` (`#if BACKEND`), `RequestExchange<T>` (deferred replies), early-arrival buffer. |
| `Transports/` | `IClientLink`/`IServerLink`, `TelepathyClientLink`/`TelepathyServerLink` (TCP), `LoopbackClientLink`/`LoopbackServerLink` + `LoopbackHub` (in-process), `LatencyShapedLink` (delay sim). |
| `Serialization/` | `MessagePackBodyCodec` (production, contractless), `ReflectionBodyCodec` (mono/csc test build). |
| `Config/` | `ClientLinkOptions` / `ServerLinkOptions` (tunables, each with a `.Default`). |
| `Core/` | `FrameCodec` (wire framing), `IClock`/`MonotonicClock`, PROXY-protocol parsing, wire enums. |
| `Diagnostics/` | `NetLog`, `ServerMetrics`, `ServerDiagnostics` (server-only). |
| `Telepathy/` | Vendored MIT TCP library (sockets/threads/framing) — not called directly. |
| `Plugins/` | Vendored MessagePack-CSharp. |

## Public API

**Client (`ClientPeer`):** `Connect(host, port)` / `ConnectAsync(host, port, timeoutMs)` /
`Reconnect()` / `ReconnectAsync()` / `Disconnect()`; `Task<TReply> CallAsync<TReply>(RequestMessage, deadlineMs)`;
`Post(NotifyMessage)`; `OnNotify<T>(Action<T>)`; `Update(budget)` (pump + evaluate deadlines,
call once per frame); `IsConnected`, `OutstandingCallCount`, `Connected`/`Disconnected` events.

**Server (`ServerPeer`, `#if BACKEND`):** `Listen(port)` / `Halt()` / `Kick(peer)`;
`Handle<TReq,TReply>(Func<int,TReq,TReply>)` (synchronous reply); `HandleDeferred<TReq>(Action<RequestExchange<TReq>>)`
+ `FulfillDeferred(exchange, reply)` (answer later); `OnNotify<T>(Action<int,T>)`;
`SendTo(peer, notify)` / `Broadcast(notify)` / `SendToMany(peers, notify)`; `Update(budget)`;
`Peers`, `Metrics`, `Diagnostics`, `PeerConnected`/`PeerDisconnected` events.

**Catalog (`MessageCatalog`):** `Enroll<T>(opcode)` (explicit, reflection-free) or
`HarvestContracts(assembly)` (reads `[MessageContract(opcode)]`); `Take<T>()` / `Recycle(msg)` (pooling).

## Setup / wiring

Pure library — everything is `new X()`; there is **no MonoBehaviour, no scene object, no
singleton**. The consumer owns the peer instance and must call `Update()` every frame from
whatever host it likes (a `MonoBehaviour.Update`, a server main loop, a test pump).

Both ends need the **same catalog registration** (same opcode→type mapping) for frames to decode.

```csharp
// --- shared: one catalog, one codec, same on both ends ---
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest>(1042);
catalog.Enroll<MoveReply>(1043);
catalog.Enroll<ChatNotify>(2001);

// --- client ---
var options = ClientLinkOptions.Default;
var link    = new TelepathyClientLink(options);      // TCP transport
var client  = new ClientPeer(link, catalog, options);
client.OnNotify<ChatNotify>(n => ShowChat(n));
await client.ConnectAsync("game.example.com", 7777);

MoveReply reply = await client.CallAsync<MoveReply>(new MoveRequest { X = 3 });
// per frame:
void Update() => client.Update();
```

### Define symbols

- **`BACKEND`** — gates all server-side code (`ServerPeer`, `TelepathyServerLink`, server
  transports, metrics/diagnostics). A **client build must NOT define it**, so the server
  internals never ship to players. Define it only in the dedicated-server assembly/build.
- `UNITY` — selects Unity-side logging in `NetLog`. Set in the Unity player build.
- `DEBUGABLES` — extra diagnostic hooks.

The production codec (`MessagePackBodyCodec`) pulls in `netstandard`/`System.Memory` facades that
a bare `csc`/`mono` build lacks, so the standalone test runner uses `ReflectionBodyCodec` instead;
Unity references the MessagePack assembly directly and uses `MessagePackBodyCodec`. The framing/
dispatch core is codec-agnostic, so the swap changes only the payload bytes.

## Testing

Use the loopback transport for deterministic, socket-free tests: create one `LoopbackHub`, wire a
`LoopbackClientLink(hub)` to a `ClientPeer` and a `LoopbackServerLink(hub)` to a `ServerPeer`,
then drive both with `Update()` — frames cross in-process, no ports. `LatencyShapedLink` wraps a
link to inject delay for timeout/reconnect tests.

## Layout

- `Runtime/` — assembly `PFound.NetworkLayer` (no deps on other `PFound.*` assemblies).
- `Tests/EditAndPlayModes/` — assembly `PFound.NetworkLayer.Tests.EditAndPlayModes`.
- `THIRD-PARTY-NOTICES.md` — Telepathy (MIT) + MessagePack-CSharp attributions.
