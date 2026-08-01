# GameSpecific.Networking (sample)

This game's realtime networking: its wire DTOs, operations, flows, opcodes, and boot wiring. It is a
**consumer** of the framework — the framework itself is documented in its own modules:
- `PFound/NetworkLayer/MODULE.md` — wire messaging, the shared client+server contract, the `#if BACKEND` server.
- `PFound/ServerOperationFlow/MODULE.md` — the client operation lifecycle.

## Folder layout
| Folder / file | What lives here |
|---|---|
| `Data/` | Domain DTOs used BOTH on the wire and in-game (`PlayerId`, `PlayerData`, `ItemId`, …). |
| `InterOpWrapperData/` | Wire-ONLY wrapper DTOs — bundle several values into one `[MessagePackObject]`. |
| `Operations/` | `[RemoteProcedure]` / `[Notify]` ops (each ends `Operation`; + optional `<Op>OperationFlow`, one file per op). |
| `Tests/` | Round-trip / loopback tests. |
| `NetOpcodes.cs` | Opcode sheet — `NetDomain` (band per subsystem) + per-domain op enums. |
| `GameNetworkResolver.cs` | The `[GeneratedMessagePackResolver]` anchor for this assembly. |
| `GameNetworkSetup.cs` | Boot wiring (codec + catalog + ambient client + ambient host). |
| `NetworkingPolicy.cs` | The one switch: require a `ServerOperationFlow` project-wide, or leave it free. |

## 1. Write a DTO
`readonly partial struct` + MessagePack attributes + **init-only** members.
```csharp
[MessagePackObject]
public readonly partial struct SpendRequest { [Key(0)] public int Amount { get; init; } }
```
Strongly-typed IDs (no bare primitives on the wire): a single-field DTO IS an id type. `[Reserved(2, 1.01)]` marks
a retired key so its number is never reused. Any wire-reachable type needs `[MessagePackObject]`.

## 2. Request/reply operation (the default RPC)
The op class stays EMPTY; the generator emits `Execute`. A reply is always a DTO.
```csharp
[RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendRequest), typeof(SpendResult))]
public partial class SpendCoinsOperation { }

await SpendCoinsOperation.Execute(amount);                       // returns the reply DTO
```
`Execute` takes only the op's params; transport/seams/run-policy/state are ambient (from `GameNetworkSetup`).

## 3. Fire-and-forget
`SpendCoinsOperation.Execute(amount).Forget();` — drops the reply; faults route to `ForgetExtensions.OnFault`
(a cancellation is swallowed).

## 4. Notify — one-way (no reply)
```csharp
[Notify(NetDomain.Player, PlayerOp.Presence, typeof(PlayerPresence))]
public partial class PresencePingOperation { }
PresencePingOperation.Notify(playerId, online);                 // returns void — never chain .Forget()
```

## 5. ServerOperationFlow — the lifecycle `Execute` runs
A reply-bearing op can own a flow (predict → send → interpret → apply, single-flight + loading). Extend
**`GameServerOperationFlow<TRequest,TResponse>`** (the game base fixing the result to `ServerOperationResult<OpResult>`).
```csharp
public sealed class SpendCoinsOperationFlow
    : GameServerOperationFlow<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>
{
    readonly SpendRequest _request;
    public SpendCoinsOperationFlow(SpendRequest request) => _request = request;   // NO context — it's ambient

    protected override ServerOperationResult<OpResult> PreCheck()                   { /* client-side reject */ }
    protected override SpendCoinsOperation.RequestMessage BuildRequest()            { /* new RequestMessage { Content = _request } */ }
    protected override ServerOperationResult<OpResult> Interpret(SpendCoinsOperation.ReplyMessage reply) { /* OpResults.Ok()/Fail(...) */ }
    protected override void ApplySuccess(ServerOperationResult<OpResult> result)    { /* apply authoritative state */ }
}
```
Call sites use `SpendCoinsOperation.Execute(amount)` (§2), never the flow directly. Cancellation is ambient (the
host's gameloop token). **Boot:** `GameNetworkSetup.Configure(peer, catalog, sessionCancellation)`.

## 5b. Error handling → toast
Failures report an **`OpResult`** value (`OpResults.Fail(OpResult.InsufficientBalance, "log detail")`; a non-Ok
reply maps via `OpResults.FromReplyStatus(reply.Status)`). `ResultCode` IS the typed `OpResult`.
`CodeToastFailurePresenter<OpResult>` shows `[<EnumType>.<Member>] <diagnostic>`. **Add an error** = add an
`OpResult` value + fail with it. `DebugToastPresenter` logs for now; one `[OperationResultCode]` enum per assembly
(enforced by PFNET0011).

## 6. Require a ServerOperationFlow project-wide (optional)
`NetworkingPolicy.cs` — uncomment `[assembly: PFound.NetworkLayer.RequireServerOperationFlow]`. Mandatory: a
flowless op's direct `Execute` becomes `internal` + same-assembly calls raise **PFNET0010**; **Alt+Enter**
scaffolds the `<Op>Flow`. After an analyzer-DLL change, **restart Unity once**.

---
*Framework internals, the shared client+server wire contract, the server side, and where domain types live →
`PFound/NetworkLayer/MODULE.md`.*
