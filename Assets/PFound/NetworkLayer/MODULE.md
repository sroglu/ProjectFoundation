# NetworkLayer Module

## Overview
Transport-agnostic request/reply/notify messaging layer for Unity clients and dedicated servers.
Typed messages are bound to opcodes in a shared catalog, packed into length-prefixed frames, and
carried over a pluggable link (in-process loopback for tests, Telepathy TCP in production). The
messaging core only ever sees opaque byte frames — it knows nothing about sockets. All delivery is
single-threaded and pump-driven via a per-frame `Update()`.

**Assembly:** `PFound.NetworkLayer`
**Layer:** Foundation (no deps on other PFound.* assemblies)
**Namespace:** `PFound.NetworkLayer` (Telepathy vendor code under `PFound.NetworkLayer.Telepathy`)

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
| `ClientPeer` | Client messaging engine: RPC correlation, notify routing, deadlines. |
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
| `ServerMetrics` / `ServerDiagnostics` | Server-only metrics/diagnostics (`#if BACKEND`). |

Game projects extend `RequestMessage` / `ReplyMessage` / `NotifyMessage` with their own message
types and enroll them (per type) in the shared `MessageCatalog`.

## Conditional Compilation
- `#if BACKEND` — Server-only code (`ServerPeer`, `TelepathyServerLink`, server metrics/diagnostics).
  A client build must NOT define it, so server internals never ship to players.
- `#if UNITY` — Unity-side logging in `NetLog` (and a `!UNITY` fallback).
- `#if DEBUGABLES` — Extra diagnostic hooks.

## Usage

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

Pure library — everything is `new X()`; there is no MonoBehaviour, scene object, or singleton. The
consumer owns the peer and calls `Update()` every frame from its host loop. Both ends need the same
catalog registration (same opcode→type mapping) for frames to decode.

## Public API

**Client (`ClientPeer`):** `Connect(host, port)` / `ConnectAsync(host, port, timeoutMs)` /
`Reconnect()` / `ReconnectAsync(timeoutMs)` / `Disconnect()`; `Task<TReply> CallAsync<TReply>(RequestMessage, deadlineMs)`;
`Post(NotifyMessage)`; `OnNotify<T>(Action<T>)`; `Update(budget)` (call once per frame);
`IsConnected`, `OutstandingCallCount`, `RemoteHost`/`RemotePort`, `Connected`/`Disconnected` events.

**Server (`ServerPeer`, `#if BACKEND`):** `Listen(port)` / `Halt()` / `Kick(peer)`;
`Handle<TReq,TReply>(Func<int,TReq,TReply>)` (synchronous reply); `HandleDeferred<TReq>(Action<RequestExchange<TReq>>)`
+ `FulfillDeferred(exchange, reply)` (answer later); `OnNotify<T>(Action<int,T>)`;
`SendTo(peer, notify)` / `Broadcast(notify)` / `SendToMany(peers, notify)`; `Update(budget)`;
`IsListening`, `Peers`, `Metrics`, `Diagnostics`, `PeerConnected`/`PeerDisconnected` events.

**Catalog (`MessageCatalog`):** `Enroll<T>(opcode)` (explicit) or `HarvestContracts(assembly)`
(reads `[MessageContract(opcode)]`); `Take<T>()` / `Recycle(msg)` (pooling);
`OpcodeFor(type)` / `TypeFor(opcode)`.

## Extending for Game Projects
1. Define message types extending `RequestMessage`, `ReplyMessage`, `NotifyMessage` (override `Clear()` for pooling).
2. Assign an opcode per message type and enroll it in the shared `MessageCatalog` (or use `[MessageContract(opcode)]` + `HarvestContracts`).
3. Register handlers on the server (`Handle` / `HandleDeferred` / `OnNotify`) and notify subscriptions on the client (`OnNotify`).

## Testing
Use the loopback transport for deterministic, socket-free tests: one `LoopbackHub`, a
`LoopbackClientLink(hub)` on a `ClientPeer` and a `LoopbackServerLink(hub)` on a `ServerPeer`, driven
by `Update()`. `LatencyShapedLink` wraps a link to inject delay for timeout/reconnect tests.
Tests live in `Tests/EditAndPlayModes/` (assembly `PFound.NetworkLayer.Tests.EditAndPlayModes`).
