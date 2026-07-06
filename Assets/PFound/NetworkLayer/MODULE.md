# NetworkLayer Module

## Overview
Reusable networking abstraction providing request/response/notify messaging patterns, TCP transport via Telepathy, in-memory transport for testing, and server-side message tracking.

**Assembly:** `PFound.NetworkLayer`
**Layer:** Foundation (no deps on other PFound.* assemblies)
**Namespace:** `PFound.NetworkLayer`

## Dependencies
- MessagePack 3.1.3 (embedded in `Runtime/Plugins/`)
- ZString / com.cysharp.zstring (server-only, for NetworkMessageTracker)

## Key Types

### Core
| Type | Description |
|---|---|
| `Broker` | Main coordinator. Generic `SendRequestAsync<TResp>(RequestMessage)`, `SendResponse()`, `Notify()`, `NotifyMany()`. |
| `NetworkMessageHandler` | Message routing: OpCode → handler dispatch, request/response matching via TaskCompletionSource, message queueing. |
| `NetworkMessageTracker` | Server-only (`#if BACKEND`). Request timeout tracking, message count metrics. |

### Message Hierarchy
| Type | Description |
|---|---|
| `NetworkMessage` | Abstract base. Has `SenderId`. |
| `RequestMessage` | Base for requests. Has `RequestId`, pooling support. |
| `ResponseMessage` | Base for responses. Has `RequestId`, `OpResult`, `ErrorMessage`, implements `INetworkResponse`. |
| `NotifyMessage` | Base for notifications. Pooling support. |

Game projects extend these with their own routing classes (e.g., `RequestToRealm`, `NotifyPlayer`).

### Data Types
| Type | Description |
|---|---|
| `OpCode` | `struct` (ushort). Message type identifier. MessagePack serializable. |
| `OpResult` | `enum`. Generic result codes (Success, Fail, FailedToSendRequest, etc.). Game projects add their own values. |
| `ConnectionId` | `readonly struct` (int). Client connection identifier. |
| `TransportMessage` | `struct`. ConnectionId + ArraySegment<byte> data. |
| `OpCodeTools` | Reflection-based OpCode → Type mapping discovery. |

### Transport Layer
| Type | Description |
|---|---|
| `TransportClientBase` | Abstract client. Network delay simulation (constant/variable), ArrayPool-based data queueing. |
| `TransportServerBase` | Abstract server (`#if BACKEND`). Connection management, broadcast support. |
| `TelepathyTransportClient` | TCP client via Telepathy. Async connect with timeout. |
| `TelepathyTransportServer` | TCP server via Telepathy (`#if BACKEND`). Proxy protocol support. |
| `MemoryTransportClient` | In-memory transport for testing (`#if BACKEND`). |
| `MemoryTransportServer` | In-memory server for testing (`#if BACKEND`). |

### Telepathy (Internal)
TCP networking library (12 files). Thread-per-connection model, send/receive pipes with pooling, SSL support.

## Conditional Compilation
- `#if BACKEND` — Server-only code (TransportServer*, MemoryTransport*, NetworkMessageTracker, Broker server methods)
- `#if UNITY` — Unity-specific logging
- `#if DEBUG || LOCAL` — Network delay simulation
- `#if DEVELOPMENT` — Extended timeout (60s vs 5s)

## Usage

```csharp
// Setup
var broker = new Broker();
var responseOpCodes = new HashSet<OpCode> { /* your response opcodes */ };
var handler = new NetworkMessageHandler(broker, responseOpCodes);

// Client
var config = new TransportClientConfig(noDelay: true, maxMessageSize: 16384, ...);
var client = new TelepathyTransportClient(handler.HandleNetworkData, config);
broker.Initialize(client, handler);

// Register handler
handler.Register<MyRequest>(myOpCode, (broker, msg) => { /* handle */ });

// Send request (returns Task)
var response = await broker.SendRequestAsync<MyResponse>(myRequest);

// Tick (call each frame)
client.Tick();
```

## Extending for Game Projects

Game projects should:
1. Define message types extending `RequestMessage`, `ResponseMessage`, `NotifyMessage`
2. Define OpCode constants per message type
3. Define game-specific `OpResult` values (use ranges above 1000)
4. Add convenience extension methods on `Broker` for routing (e.g., `SendToRealm<T>()`)
