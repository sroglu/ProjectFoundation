# Backend

> **Module group — Networking.** The server-side layer on top of `PFound.NetworkLayer`, opposite
> `PFound.ServerOperationFlow` (the client-side layer over the same transport). Grouped by purpose —
> see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

Makes NetworkLayer's `ServerPeer` **hostable**: a per-tick host loop (`BackendHost`), a fluent
registration surface that binds typed **async** handlers to deferred request exchanges
(`OperationHandlerRegistry`), and an engine-free contract layer (`PFound.Backend.Core`) holding the four
seams an authoritative server needs — an operation handler, a notify handler, a peer ↔ player session
store, and a credential authenticator. The module carries **no game rules**: every concrete handler,
repository, and authenticator is supplied by the game.

The problem it actually solves is the threading one. An `async` handler completes on a threadpool thread,
but `ServerPeer` is single-threaded — frames arrive and replies leave only inside `Update()`. The registry
therefore never calls `exchange.Reply(...)` from the continuation; it enqueues a closure onto a shared
`ConcurrentQueue<Action>` that `BackendHost.Tick()` drains immediately after `ServerPeer.Update()`. So a
handler may `await` freely and its reply still leaves on the pump thread, with no race against the
deferred-expiry watchdog. The registry also guarantees a reply on the failure path: a handler task that
does not complete successfully still sends a `ReplyStatus.Faulted` reply, instead of leaving the caller to
wait out its full deadline.

## Scope boundary

- **Backend is the server half.** The predict → send → interpret → apply lifecycle is client-only and
  lives in `PFound.ServerOperationFlow`; there is no flow here. The server decides, the client predicted.
- **Wire types are not defined here.** Requests, replies, opcodes, and the `MessageCatalog` live in the
  game's engine-free wire-contract assembly, referenced by BOTH ends (see NetworkLayer's MODULE.md,
  "Client and server share ONE contract"). This module only constrains them to
  `RequestMessage` / `ReplyMessage`.
- **No storage, no game logic, no auth policy.** Repositories, the concrete `IAuthenticator<T>`, and the
  handlers themselves are game-side. `PFound.Backend.Core` states the contracts and ships exactly one
  default implementation (`InMemorySessionStore`).
- **No engine.** Both assemblies are `noEngineReferences: true` so the same server runs as a plain .NET
  console app or a Unity-headless build. A `MonoBehaviour` that merely ticks the host belongs in a
  separate Unity-only assembly.

## Assemblies

| Assembly | Location | Notes |
|---|---|---|
| `PFound.Backend.Core` | `Core/Runtime/` | Engine-free contracts + the in-memory session store. `noEngineReferences: true`, `overrideReferences: true` with an empty `precompiledReferences`, **no** references at all. mono/csc-testable. |
| `PFound.Backend.Core.Tests` | `Core/Tests/` | Standalone mono/csc runner (`Program.cs` `Main` + `TestKit`), references Core only, engine-free. Not a Unity TestRunner assembly. |
| `PFound.Backend` | `Runtime/` | The host + handler registry. References `PFound.Backend.Core` and `PFound.NetworkLayer`. `noEngineReferences: true`, `defineConstraints: ["BACKEND"]`. |

Both runtime assemblies are `autoReferenced: false` — a consumer references them explicitly.

Namespaces: `PFound.Backend.Core` (contracts + session store), `PFound.Backend` (host + registry).

## Dependencies

- **Core** — none. No `PFound.*` assemblies, no third-party packages, no engine. Uses only
  `System`, `System.Collections.Generic`, `System.Threading`, and `System.Threading.Tasks`.
  `Core/Runtime/AssemblyInfo.cs` ships the one-line `IsExternalInit` shim that .NET Standard 2.1 lacks,
  so the C# 9 `init` accessors on `PlayerSession` / `AuthResult` compile.
- **Runtime** — `PFound.Backend.Core` + `PFound.NetworkLayer` only. Still engine-free: the console host
  is a supported target, and a single `UnityEngine` reference anywhere in this chain would break it.
- **`BACKEND` define** — the `PFound.Backend` assembly has `defineConstraints: ["BACKEND"]`, matching
  NetworkLayer's `#if BACKEND` server types (`ServerPeer`, `RequestExchange<T>`, `TelepathyServerLink`,
  server metrics/diagnostics). Without the symbol the assembly is skipped, so a client build never
  contains the server. `PFound.Backend.Core` is **not** constrained — the contracts compile everywhere.

## Key types

| Type | Description |
|---|---|
| `BackendHost` | The tick loop. Owns the `ServerPeer` and the deferred reply queue and drives both in lockstep: `Tick()` calls `ServerPeer.Update()`, then drains every queued reply closure. `Listen(port)` / `Halt()` forward to the peer; `Peers` exposes its read-only connected-peer view. Owns no thread and no clock — the caller drives it. |
| `OperationHandlerRegistry` | Binds handlers to a `ServerPeer`. `Register<TReq,TReply>(handler)` stores a typed closure (fluent, returns `this`); `AttachTo(serverPeer)` runs every closure, each calling `serverPeer.HandleDeferred<TReq>` with its captured request type. Constructed with the same `ConcurrentQueue<Action>` the host drains. |
| `IOperationHandler<TRequest,TReply>` | The request/reply seam: `Task<TReply> HandleAsync(int peerId, TRequest request, CancellationToken ct)`. Always async — the registry only wires deferred exchanges. |
| `INotifyHandler<TNotify>` | The one-way seam: `void Handle(int peerId, TNotify notify)`. Synchronous by contract; **not** wired by the registry today (see Limitations). |
| `ISessionStore` | The connection-lifecycle seam: `Bind(peerId, session)`, `TryGet(peerId, out session)`, `Unbind(peerId)`. |
| `PlayerSession` | `readonly struct` — `long PlayerId`, `string AuthToken`, both `init`-only. |
| `InMemorySessionStore` | The default `ISessionStore`: a `Dictionary<int, PlayerSession>`. `Bind` throws `InvalidOperationException` if the peer is already bound (a rebind is a bug, not an overwrite). |
| `IAuthenticator<TCredentials>` | The login seam: `Task<AuthResult> AuthenticateAsync(TCredentials credentials, CancellationToken ct)`. Generic over the game's own credential type. |
| `AuthResult` | `readonly struct` — `bool Success`, `long PlayerId`, `string ErrorCode`, all `init`-only. |

## Public API

**`PFound.Backend` (`BACKEND`-gated)**
```csharp
public sealed class BackendHost
{
    public BackendHost(ServerPeer serverPeer, ConcurrentQueue<Action> replyQueue);
    public void Listen(int port);
    public void Tick();                                   // Update() then drain the reply queue
    public void Halt();
    public IReadOnlyCollection<int> Peers { get; }
}

public sealed class OperationHandlerRegistry
{
    public OperationHandlerRegistry(ConcurrentQueue<Action> replyQueue);
    public OperationHandlerRegistry Register<TReq, TReply>(IOperationHandler<TReq, TReply> handler)
        where TReq : RequestMessage, new()
        where TReply : ReplyMessage, new();
    public void AttachTo(ServerPeer serverPeer);
}
```

**`PFound.Backend.Core` (engine-free)**
```csharp
public interface IOperationHandler<TRequest, TReply>
{
    Task<TReply> HandleAsync(int peerId, TRequest request, CancellationToken ct);
}

public interface INotifyHandler<in TNotify>
{
    void Handle(int peerId, TNotify notify);
}

public interface ISessionStore
{
    void Bind(int peerId, PlayerSession session);
    bool TryGet(int peerId, out PlayerSession session);
    void Unbind(int peerId);
}

public readonly struct PlayerSession { public long PlayerId { get; init; } public string AuthToken { get; init; } }

public interface IAuthenticator<TCredentials>
{
    Task<AuthResult> AuthenticateAsync(TCredentials credentials, CancellationToken ct);
}

public readonly struct AuthResult
{
    public bool Success { get; init; }
    public long PlayerId { get; init; }
    public string ErrorCode { get; init; }
}

public sealed class InMemorySessionStore : ISessionStore { /* throws on duplicate Bind */ }
```

## Use-case examples

### 1. Booting a host

One `ConcurrentQueue<Action>` is shared by the registry (producer, on threadpool continuations) and the
host (consumer, on the pump thread). That shared queue **is** the thread boundary — build both with it.

```csharp
var catalog    = GameNetworkSetup.CreateCatalog();          // the SHARED opcode ↔ type catalog
var serverLink = new TelepathyServerLink(ServerLinkOptions.Default);
var serverPeer = new ServerPeer(serverLink, catalog, ServerLinkOptions.Default);

var sessions   = new InMemorySessionStore();
var replyQueue = new ConcurrentQueue<Action>();

new OperationHandlerRegistry(replyQueue)
    .Register<LoginOperation.RequestMessage,      LoginOperation.ReplyMessage>(new LoginHandler(auth, sessions))
    .Register<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>(new SpendCoinsHandler(wallets, sessions))
    .AttachTo(serverPeer);

var host = new BackendHost(serverPeer, replyQueue);
host.Listen(7777);
```

### 2. Writing an operation handler

A handler is a plain class — no base type, no attributes. The five handler rules from NetworkLayer's
MODULE.md ("Authoring a server handler") still bind; the registry only takes rules 2 and 5 off your hands.

```csharp
public sealed class SpendCoinsHandler
    : IOperationHandler<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>
{
    readonly IWalletRepository _wallets;
    readonly ISessionStore _sessions;

    public async Task<SpendCoinsOperation.ReplyMessage> HandleAsync(
        int peerId, SpendCoinsOperation.RequestMessage request, CancellationToken ct)
    {
        // Rule 3: copy every value out of the POOLED message BEFORE the first await.
        int amount = request.Content.Amount;

        // Resolve the authenticated player from the peer — the registry does not do this for you.
        if (!_sessions.TryGet(peerId, out var session))
            return new SpendCoinsOperation.ReplyMessage { Status = ReplyStatus.Faulted };

        int balance = await _wallets.SpendAsync(session.PlayerId, amount, ct);

        // Rule 5: fail with a status, never with silence. Build the reply just before returning.
        return balance < 0
            ? new SpendCoinsOperation.ReplyMessage { Status = ReplyStatus.Faulted }
            : new SpendCoinsOperation.ReplyMessage { Status = ReplyStatus.Ok, Content = new SpendResult { Balance = balance } };
    }
}
```

Returning normally is enough — the registry sends whatever you returned. Throwing (or a faulted/cancelled
task) is also answered: the client gets `ReplyStatus.Faulted` rather than hanging.

### 3. Binding a peer to a player at login

`ISessionStore` is the only state the module keeps about a connection, and only a handler writes it. A
login handler authenticates, then `Bind`s; every later handler resolves the player with `TryGet(peerId, …)`.

```csharp
var auth = await _authenticator.AuthenticateAsync(new LoginCredentials { … }, ct);
if (!auth.Success)
    return new LoginOperation.ReplyMessage { Status = ReplyStatus.Faulted };

_sessions.Bind(peerId, new PlayerSession { PlayerId = auth.PlayerId, AuthToken = token });
```

Unbinding is **not** automatic: subscribe `ServerPeer.PeerDisconnected` to call `Unbind(peerId)`, or the
map grows for the life of the process.

## Setup / wiring

Pure library — everything is `new X()`; no MonoBehaviour, no singleton, no service locator. Two supported
hosts, same engine-free assembly, same `Tick()`:

**Plain .NET console** — one `Main` loop:
```csharp
host.Listen(7777);
while (running) { host.Tick(); Thread.Sleep(15); }   // ~66 ticks/s
host.Halt();
```

**Unity (headless build or the editor)** — a thin, Unity-only `MonoBehaviour` in its OWN assembly that
does nothing but `Start` → `Listen`, `Update` → `Tick`, `OnDestroy` → `Halt`. Never move that file into the
engine-free server assembly; the console build breaks the moment it references `UnityEngine`.

Both ends must be built against the SAME wire assembly and share the SAME opcode catalog, and the server
build must define `BACKEND`. `Assets/GameSpecific/Backend/` in this repo is the copyable end-to-end
reference (handlers, repositories, authenticator, console `Program.cs`), with the Unity tick host kept
separate in `Assets/GameSpecific/Backend.Unity/`.

## Testing

- **Core (engine-free):** standalone mono/csc runner — 19/19 assertions green.
  ```
  csc -nologo -warn:0 -out:/tmp/pf_backend_core.exe \
      Assets/PFound/Backend/Core/Runtime/*.cs \
      Assets/PFound/Backend/Core/Tests/*.cs && mono /tmp/pf_backend_core.exe
  ```
  Covers session bind/retrieve, unbind, duplicate-bind throw, miss on an unbound peer, multiple peers, the
  two handler contracts, and the `PlayerSession` / `AuthResult` value shapes.
- **Runtime:** exercised end-to-end over NetworkLayer's in-memory loopback transport — a `BackendHost` on a
  `LoopbackServerLink` and a `ClientPeer` on a `LoopbackClientLink` sharing one `LoopbackHub`, pumped until
  the round-trip completes. The registry's pump-thread marshaling and the `Faulted` failure path are what
  that test is for. The in-repo instance lives with the game's handlers in
  `Assets/GameSpecific/Backend/Tests/`.

## Limitations / Known gaps

- **`INotifyHandler<T>` is a contract with no wiring.** The registry only registers request/reply
  operations; nothing attaches a notify handler to `ServerPeer.OnNotify<T>`. Wire notifies directly on the
  peer until a registration path exists.
- **No authentication gate in the dispatch path.** `IAuthenticator<T>` and `ISessionStore` are seams the
  module never calls. Every handler must resolve its own session and refuse unauthenticated peers itself —
  an unregistered peer reaches the handler exactly like a logged-in one.
- **Nothing unbinds a session.** `Unbind` exists but no code path calls it; the game must hook
  `ServerPeer.PeerDisconnected`.
- **`CancellationToken.None` is passed to every handler.** The serve-deadline watchdog is not linked to the
  handler's token: when an exchange expires, the client gets `Expired`, the handler keeps running to
  completion, and its late reply is silently dropped by `ServerPeer`.
- **A faulted handler's exception is discarded.** The registry maps any non-successful task to
  `ReplyStatus.Faulted` and never logs or surfaces the exception; the failure detail is lost.
- **`InMemorySessionStore` is process-local and not thread-safe.** Plain `Dictionary`, no locking — use it
  from the pump thread only. Nothing is persisted, so every session dies with the process.
- **One handler per request type.** `ServerPeer` keys routes by opcode, so registering a second handler for
  the same `TReq` replaces the first — silently.
- **Nothing runs without `Tick()`.** `BackendHost` owns no thread and no timer; a stalled caller stalls
  delivery, replies, and the watchdog alike.
- **Single peer, single port.** No rooms, matchmaking, sharding, or horizontal scale concerns live here.
