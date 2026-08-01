# GameSpecific.Networking

> **Game layer — Networking.** How this game talks to its servers. One networking assembly; the source
> generators + analyzer do the boilerplate. Thin landing in [README.md](README.md); this is the full reference.

## Purpose
The game's **realtime** client contract + operations: typed wire DTOs, opcode-bound messages, and the
client-side server-authoritative operation lifecycle. Built on `PFound.NetworkLayer` (wire) +
`PFound.ServerOperationFlow` (lifecycle).

## Two server worlds — they coexist
| World | Transport | Use for | Modules |
|---|---|---|---|
| **Realtime dedicated** | socket (Telepathy TCP) — persistent, bidirectional, low-latency | live gameplay/session: move, spend, alliance, presence | NetworkLayer + ServerOperationFlow (**this assembly**) |
| **HTTP LiveOps** | HTTP request/response — stateless | remote config, IAP receipt verify, player-data blob sync | RemoteGameConfig / Commerce / PlayerDataSync (BestHTTP) |

A game runs **both**: a dedicated realtime server for gameplay + HTTP backends for config/commerce/persistence.
`ServerOperationFlow` is a client lifecycle over a transport **seam** — today it binds the realtime peer; an
HTTP adapter is a future option, not a requirement.

## Wire contract is SHARED — client + server, single source of truth
The server is **not** a separate client. Both ends share the SAME wire types:
- Same DTOs (`Data/` + `InterOpWrapperData/`, `[MessagePackObject]`), same opcodes (`NetOpcodes`), same
  `MessageCatalog` + MessagePack formatters. Formatters are generated from the DTOs, so both ends are compatible
  by construction.
- The server **references this assembly** (or the shared subset) and compiles the messaging engine with `BACKEND`
  defined — NetworkLayer's `ServerPeer` lives in the same module under `#if BACKEND`. The server is the other END
  of one contract, not a second copy of it.
- **Never hand-write the server's messages separately** — duplicated wire types drift and break compatibility.
- `ServerOperationFlow` is **client-only** (client-prediction + local apply are client concerns). Server side:
  receive `RequestMessage` → run authoritative logic → return `ReplyMessage`. No flow.

## Folder layout
| Folder / file | What lives here |
|---|---|
| `Data/` | Domain DTOs used BOTH on the wire and in-game (`PlayerId`, `PlayerData`, `ItemId`, …). |
| `InterOpWrapperData/` | Wire-ONLY wrapper DTOs — bundle several values into one `[MessagePackObject]`. Not used in gameplay. |
| `Operations/` | `[RemoteProcedure]` / `[Notify]` ops (each ends in `Operation`; + optional `<Op>OperationFlow` lifecycle, one file per op). |
| `Tests/` | Round-trip / loopback tests. |
| `NetOpcodes.cs` | Central opcode sheet — `NetDomain` (one band per subsystem) + per-domain op enums. |
| `GameNetworkResolver.cs` | The `[GeneratedMessagePackResolver]` anchor MessagePack fills for this assembly. |
| `GameNetworkSetup.cs` | Boot wiring (codec + catalog + ambient client + ambient host). |
| `NetworkingPolicy.cs` | The one switch: require a `ServerOperationFlow` project-wide, or leave it free. |

Folders are organisational only — the generators find types by attribute across the whole assembly.

## Authoring

### 1. Write a DTO
`readonly partial struct` + MessagePack attributes + **init-only** members. The generator adds value equality +
`ToString`; you write only the `[Key]` members.
```csharp
[MessagePackObject]
public readonly partial struct SpendRequest { [Key(0)] public int Amount { get; init; } }
```
- **Strongly-typed IDs** (no bare primitives on the wire): a single-field DTO IS an id type.
  `[MessagePackObject] public readonly partial struct PlayerId { [Key(0)] public long Value { get; init; } }`
- **Retired keys** — `[Reserved(2, 1.01)]` marks a key so its number is never reused (append-only wire discipline).
- Any wire-reachable type needs `[MessagePackObject]`; a type used ONLY in-game needs nothing.

### 2. Request/reply operation (the default RPC)
Declare the op — the class stays EMPTY; the generator emits `Execute`. Calling is uniform in both policy modes.
```csharp
[RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendRequest), typeof(SpendResult))]
public partial class SpendCoinsOperation { }        // + a sibling SpendCoinsOperationFlow (§5)

[RemoteProcedure(NetDomain.Player, PlayerOp.GetData, typeof(PlayerId), typeof(PlayerData))]
public partial class GetPlayerDataOperation { }

await SpendCoinsOperation.Execute(amount);            // mutation — result lands in ambient state
PlayerData p = await GetPlayerDataOperation.Execute(playerId);   // query — Execute returns the reply DTO
```
A reply is ALWAYS a DTO. `Execute` takes only the op's own params; transport/seams/run-policy/state are ambient
(from `ServerOperationHost` / game singletons), configured once in `GameNetworkSetup`.

> **`Execute` is ALWAYS generated; the op class is always empty.** Every form takes the request DTO (plus a
> members-overload) and returns `Task<TReply>`, so a call site is identical with or without a flow. Flowless →
> a direct `Execute` that sends+returns the reply (public in **free** mode, `internal` under **mandatory**).
> Flow-backed → an `Execute` that news the flow up and returns `flow.Reply.Content` — the same DTO.

### 3. Fire-and-forget an RPC
Needs `using PFound.NetworkLayer;`. `Execute` returns a `Task`, so drop the outcome with `.Forget()`:
```csharp
SpendCoinsOperation.Execute(amount).Forget();
```
Faults route to `ForgetExtensions.OnFault` (wire it to `Debug.LogException` at boot); a cancellation is swallowed.

### 4. Notify — one-way (no reply)
```csharp
[Notify(NetDomain.Player, PlayerOp.Presence, typeof(PlayerPresence))]
public partial class PresencePingOperation { }

PresencePingOperation.Notify(playerId, online);       // returns void — never chain .Forget()
```
Use `Notify` when the server sends nothing back; use `Execute(...).Forget()` when there IS a reply you drop.

### 5. ServerOperationFlow — the lifecycle `Execute` runs
A reply-bearing op can own a flow (client-prediction → send → interpret → apply, with single-flight + loading).
It reuses the same generated messages and lives in the op's OWN file. Extend
**`GameServerOperationFlow<TRequest,TResponse>`** (the game base fixing the result to `ServerOperationResult<OpResult>`).
```csharp
public sealed class SpendCoinsOperationFlow
    : GameServerOperationFlow<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>
{
    readonly SpendRequest _request;
    public SpendResult Result { get; private set; }
    public SpendCoinsOperationFlow(SpendRequest request) => _request = request;   // NO context — it's ambient

    protected override ServerOperationResult<OpResult> PreCheck()                   { /* client-side reject */ }
    protected override SpendCoinsOperation.RequestMessage BuildRequest()            { /* new RequestMessage { Content = _request } */ }
    protected override ServerOperationResult<OpResult> Interpret(SpendCoinsOperation.ReplyMessage reply) { /* OpResults.Ok()/Fail(...) */ }
    protected override void ApplySuccess(ServerOperationResult<OpResult> result)    { /* apply authoritative state */ }
}
```
The base owns send/single-flight/loading/failure/cancellation and can't be skipped. Call sites use
`SpendCoinsOperation.Execute(amount)` (§2), never the flow directly. Cancellation is **ambient**: the host holds a
gameloop-cancelled token that aborts not-yet-started flows + post-effects (the in-flight hop is deadline-bounded).

**Boot (once):** `GameNetworkSetup.Configure(peer, catalog, sessionCancellation)` publishes the ambient client +
the ambient `ServerOperationHost<ServerOperationResult<OpResult>>` (transport factory + toast presenter + gate).

### 5b. Error handling → toast
Every failure (pre-check reject AND server failure) funnels through the ambient host's failure presenter:
- Failures report an **`OpResult`** value (`OpResults.Fail(OpResult.InsufficientBalance, "log detail")`; a non-Ok
  reply maps via `OpResults.FromReplyStatus(reply.Status)`). `ResultCode` IS the typed `OpResult`, not a number.
- `CodeToastFailurePresenter<OpResult>` shows `[<EnumType>.<Member>] <diagnostic>` through the toast seam
  (`[OpResult.InsufficientBalance] insufficient balance`); an out-of-range code shows the raw number.
- **Add an error** = add an `OpResult` value + fail with it. `DebugToastPresenter` logs for now; swap for a real
  toast widget, or a localization-resolving presenter, without touching the pipeline. One `[OperationResultCode]`
  enum per assembly (enforced by **PFNET0011**).

### 6. Require a ServerOperationFlow project-wide (optional policy)
`NetworkingPolicy.cs` — uncomment `[assembly: PFound.NetworkLayer.RequireServerOperationFlow]`:
- **Mandatory:** a flowless op's direct `Execute` becomes `internal` (hard cross-assembly block); same-assembly
  calls raise **PFNET0010**. `[Notify]` stays allowed. **Alt+Enter** on PFNET0010 → "Create ServerOperation flow"
  scaffolds a `<Op>Flow` against `ServerOperationResult<OpResult>` (the `[OperationResultCode]` enum).
- **Free** (default): both the direct and flow-backed `Execute` are allowed.
- After an analyzer-DLL change, **restart Unity once** so it reloads the analyzer/generator (sticky at domain start).

## Server side (`#if BACKEND`)
The server shares this assembly's DTOs/opcodes/catalog and compiles NetworkLayer with `BACKEND`. It handles the
same messages the client sends — no flow, no ServerOperationFlow:
```csharp
serverPeer.Handle<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>((peerId, request) =>
{
    // authoritative logic: the server DECIDES success; the client only predicted it.
    var reply = new SpendCoinsOperation.ReplyMessage { Status = ReplyStatus.Ok, Content = new SpendResult { NewBalance = … } };
    return reply;
});
```
The client's flow `Interpret` maps that `ReplyMessage` (status + DTO) into the result it applies. Keep the wire
contract (this assembly) as the single source both ends compile against.

## Types vs data — where things live
- **Types are compile-time code — you cannot inject or deliver a type.** Domain identity primitives (`PlayerId`,
  `AllianceId`, `ItemId`) are the game's vocabulary; they start where they're used (`Data/`). Extract them to a
  shared game assembly (`GameSpecific.Domain`) only when a primitive is genuinely reused across wire + datastore +
  gameplay — the server already shares this assembly, so wire IDs need no move for the server.
- **Data / definitions are game-by-game and delivered however the game chooses:** baked ScriptableObject config,
  ContentDelivery (addressables / CDN), runtime injection, or straight from the server. `PFound.GameDataStore` is
  the runtime model — entity types are data-driven via numeric ids (no code type per entity); it does NOT read
  JSON itself. So `PlayerId` the type is code; the player's data is delivered/injected.

## Serialization
MessagePack's official source generator produces the formatters + `GameNetworkResolver` (AOT-safe, zero
reflection). The poolable envelope is a runtime-only carrier; only the DTO crosses the wire. See
`Assets/PFound/NetworkLayer/MODULE.md` for framework internals.

## Dependencies
`PFound.NetworkLayer` (wire), `PFound.ServerOperationFlow` + `.Core` (client lifecycle), MessagePack.
