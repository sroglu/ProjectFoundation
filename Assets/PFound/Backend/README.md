# PFound.Backend

The server side of `PFound.NetworkLayer`: a tick loop that hosts a `ServerPeer`, a registry that binds
typed **async** handlers to deferred request exchanges and marshals their replies back onto the pump
thread, and an engine-free contract layer (operation/notify handler, peer ↔ player session store,
authenticator). No game rules of its own — handlers, repositories, and the authenticator are yours.
Engine-free, so the same build runs as a plain .NET console app or a Unity-headless server.

## Quick reference

One `ConcurrentQueue<Action>` is shared by the registry (writes from threadpool continuations) and the
host (drains on the pump thread) — that queue is the whole thread boundary.

```csharp
var serverPeer = new ServerPeer(new TelepathyServerLink(ServerLinkOptions.Default),
                               GameNetworkSetup.CreateCatalog(),      // the SHARED opcode ↔ type catalog
                               ServerLinkOptions.Default);
var sessions   = new InMemorySessionStore();                          // peer id → PlayerSession
var replyQueue = new ConcurrentQueue<Action>();

new OperationHandlerRegistry(replyQueue)
    .Register<LoginOperation.RequestMessage,      LoginOperation.ReplyMessage>(new LoginHandler(auth, sessions))
    .Register<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>(new SpendCoinsHandler(wallets, sessions))
    .AttachTo(serverPeer);

var host = new BackendHost(serverPeer, replyQueue);
host.Listen(7777);
while (running) { host.Tick(); Thread.Sleep(15); }   // Update() then drain replies; ~66 ticks/s
host.Halt();

// A handler is a plain class. Copy values out of the POOLED request before the first await,
// and always answer — a faulted task still replies ReplyStatus.Faulted instead of hanging the client.
public sealed class SpendCoinsHandler
    : IOperationHandler<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>
{
    public async Task<SpendCoinsOperation.ReplyMessage> HandleAsync(
        int peerId, SpendCoinsOperation.RequestMessage request, CancellationToken ct) { … }
}
```

In Unity, put the `MonoBehaviour` that calls `Listen`/`Tick`/`Halt` in its OWN Unity-only assembly — a
single `UnityEngine` reference in the server assembly breaks the console host.

## Dependencies

`PFound.Backend.Core` has none at all (engine-free, no `PFound.*`, no third-party). `PFound.Backend`
references `PFound.Backend.Core` + `PFound.NetworkLayer` and is `defineConstraints: ["BACKEND"]`, matching
NetworkLayer's `#if BACKEND` server types — so a client build never contains it. Both are
`autoReferenced: false`; a consumer asmdef references them explicitly.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies and namespaces, the full public API, the pump-thread
reply-marshaling model, handler authoring rules, console vs Unity-headless hosting, the standalone
`csc`/`mono` test runner, and the known gaps.
