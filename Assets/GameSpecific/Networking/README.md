# GameSpecific.Networking — developer cookbook

How this game talks to the server. One networking assembly; the source generators + analyzer do the
boilerplate. *(Living doc — a few ergonomics are still being refined; usage may shift slightly.)*

## Folder layout
| Folder | What lives here |
|---|---|
| `Data/` | Domain DTOs used BOTH on the wire and in-game (`PlayerId`, `PlayerData`, `ItemId`, …). |
| `InterOpWrapperData/` | Wire-ONLY wrapper DTOs — when one message must carry several values (e.g. `float` + `PlayerData`), bundle them into a single `[MessagePackObject]` here. Not used in gameplay. |
| `Operations/` | `[RemoteProcedure]` / `[Notify]` ops (each ends in `Operation`; + optional `OperationFlow` lifecycle class, one file per op). |
| `Tests/` | Round-trip / loopback tests. |
| `NetOpcodes.cs` | The central opcode sheet — `NetDomain` (one band per subsystem) + per-domain op enums. |
| `GameNetworkResolver.cs` | The `[GeneratedMessagePackResolver]` anchor MessagePack fills for this assembly. |
| `GameNetworkSetup.cs` | Boot wiring (codec + catalog + ambient client). |
| `NetworkingPolicy.cs` | The one switch: require `ServerOperation` project-wide, or leave it free. |

Folders are organisational only — the generators find types by attribute across the whole assembly, so
placement never affects codegen.

## 1. Write a DTO
`readonly partial struct` + real MessagePack attributes + **init-only** members. The generator adds value
equality + `ToString`; you write only the `[Key]` members.
```csharp
[MessagePackObject]
public readonly partial struct SpendRequest { [Key(0)] public int Amount { get; init; } }
```
- **Strongly-typed IDs** (avoid bare primitives on the wire): a single-field DTO IS an id type.
  ```csharp
  [MessagePackObject] public readonly partial struct PlayerId { [Key(0)] public long Value { get; init; } }
  ```
- **Retired keys** — mark them so the number is never reused (append-only wire discipline):
  ```csharp
  [MessagePackObject]
  [Reserved(2, 1.01)]   // key 2 was live through v1.01 (inclusive); reusable once no v1.01-or-older client remains
  public readonly partial struct PlayerData { [Key(0)] …; [Key(1)] …; [Key(5)] …; }
  ```
- Any wire-reachable type needs `[MessagePackObject]`. A type used ONLY in-game needs nothing.

## 2. Request/reply operation (the default — an RPC)
Declare the op — the operation class stays EMPTY. The generator emits its `Execute(<request params>)`; you never
hand-write one. Calling is uniform and works under BOTH policy modes:
```csharp
// A mutation with local prediction OWNS a flow (see §5) → the generated Execute news that flow up and runs it,
// returning the reply DTO. The class is still empty:
[RemoteProcedure(NetDomain.Wallet, WalletOp.Spend, typeof(SpendRequest), typeof(SpendResult))]
public partial class SpendCoinsOperation { }        // + a sibling SpendCoinsOperationFlow in the same file (§5)

// A query that validates the reply + captures the fetched DTO ALSO owns a flow → Execute news it up and returns
// the reply DTO. The class is still empty:
[RemoteProcedure(NetDomain.Player, PlayerOp.GetData, typeof(PlayerId), typeof(PlayerData))]
public partial class GetPlayerDataOperation { }     // + a sibling GetPlayerDataOperationFlow in the same file (§5)

// mutation — await it; the authoritative result lands in ambient game state (the wallet):
await SpendCoinsOperation.Execute(amount);

// query — Execute returns the reply DTO:
PlayerData p = await GetPlayerDataOperation.Execute(playerId);
```
A reply is ALWAYS a DTO (never a bare primitive). `Execute` takes ONLY the operation's own parameters — the
transport, seams, run policy, and mutated game state are ambient (resolved from `ServerOperationHost.Current` /
game singletons), configured once in `GameNetworkSetup`.

> **`Execute` is ALWAYS generated — the operation class is always empty.** Every form takes the request DTO (plus a
> convenience overload over its members) and returns `Task<TReply>`, so a call site is identical whether or not a
> flow exists. For a flowless op the generator emits a direct `Execute` that just sends and returns the reply
> (public in **free** mode, `internal` — unreachable — under the **mandatory** policy, which steers you to a flow).
> For an op that owns a `<Op>Flow`, the generator instead emits an `Execute` that news the flow up and returns
> `flow.Reply.Content` — the same DTO. Either way you write no `Execute` by hand.

## 3. Fire-and-forget an RPC (don't wait for the reply)
Needs `using PFound.NetworkLayer;`. `Execute` returns a `Task` (or `Task<DTO>`, which is-a `Task`), so drop the
outcome with `.Forget()`:
```csharp
SpendCoinsOperation.Execute(amount).Forget();
GetPlayerDataOperation.Execute(playerId).Forget();     // the returned DTO is simply dropped
```
Faults surface via `ForgetExtensions.OnFault` (wire it to `Debug.LogException` at boot).

## 4. Notify — genuinely one-way (no reply on the wire)
```csharp
[Notify(NetDomain.Player, PlayerOp.Presence, typeof(PlayerPresence))]
public partial class PresencePingOperation { }

PresencePingOperation.Notify(playerId, online);        // returns void — it IS fire-and-forget; never chain .Forget()
```
Use `Notify` when the server sends nothing back (telemetry, presence). Use `Execute(...).Forget()` when the
op HAS a reply you just don't want to await.

## 5. ServerOperation flow — the lifecycle `Execute` runs
Each reply-bearing op owns a flow (client-prediction + send + interpret + apply, with single-flight + loading).
It reuses the SAME generated messages — no extra wire declaration — and lives in the op's OWN file (one file per
op). The flow is constructed with ONLY the request DTO; its base ctor resolves the context (transport +
outcome seams + run policy) from the ambient `ServerOperationHost.Current`. See `Operations/SpendCoinsOperation.cs`
for the full live reference (`SpendCoinsOperationFlow`). Shape:
```csharp
public sealed class SpendCoinsOperationFlow
    : ServerOperationFlow<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage, ServerOperationResult<OpResult>>
{
    readonly SpendRequest _request;                             // the request DTO the generated Execute passes in
    public SpendResult Result { get; private set; }             // capture the reply DTO here if callers need it
    public SpendCoinsOperationFlow(SpendRequest request) => _request = request;   // NO context param — it's ambient

    protected override ServerOperationResult<OpResult> PreCheck()                   { /* client-side reject, no request sent */ }
    protected override SpendCoinsOperation.RequestMessage BuildRequest()            { /* new RequestMessage { Content = _request } */ }
    protected override ServerOperationResult<OpResult> Interpret(SpendCoinsOperation.ReplyMessage reply) { /* OpResults.Ok() / OpResults.Fail(...) */ }
    protected override void ApplySuccess(ServerOperationResult<OpResult> result) { /* apply authoritative state (ambient) */ }
}
```
The result is the framework's ready-made `ServerOperationResult<OpResult>` — its `ResultCode` is the typed
`OpResult` itself, so `PreCheck`/`Interpret` report through `OpResults.Ok()` / `OpResults.Fail(OpResult.X, "…")`.
Everything else (send, single-flight, loading, uniform failure) is owned by the base and cannot be skipped. Call
sites never touch the flow directly — they use `SpendCoinsOperation.Execute(amount)` (§2), which news it up and
runs it.

**Boot wiring (once):** `GameNetworkSetup.Configure(peer, catalog)` publishes both the ambient client and the
ambient `ServerOperationHost` (transport factory + a toast failure presenter (§5b) + a shared single-flight
gate), so every `Execute` has what it needs.

## 5b. Error handling → toast
Every failure — a pre-check reject AND a server failure — funnels through the ambient host's failure
presenter, so error handling is identical everywhere:
- Failures report an **`OpResult`** enum value (`OpResults.Fail(OpResult.InsufficientBalance, "log detail")`;
  a non-Ok reply maps via `OpResults.FromReplyStatus(reply.Status)`). The result's `ResultCode` IS that typed
  `OpResult` (the flow's result type is `ServerOperationResult<OpResult>`), not a bare number.
- The framework `CodeToastFailurePresenter<OpResult>` prints that typed code and shows
  `[<EnumType>.<Member>] <diagnostic>` through the toast seam — e.g.
  `[OpResult.InsufficientBalance] insufficient balance`. An out-of-range code shows the raw number
  (`[OpResult.999] …`) instead of a blank.

**To add an error:** add an `OpResult` value and fail with it — that is the whole change, no mapping. The toast
destination is `DebugToastPresenter` (logs `[toast] …` for now — swap for a real on-screen widget later);
`LoggingFailurePresenter` remains a valid alternative presenter. When you later want translated, user-facing
copy, swap `CodeToastFailurePresenter` for one that resolves the code through localization — nothing else in
the pipeline changes.

## 6. Require ServerOperation project-wide (optional policy)
`NetworkingPolicy.cs` — uncomment the one line to make the pipeline mandatory:
```csharp
[assembly: PFound.NetworkLayer.RequireServerOperationFlow]   // present = mandatory; commented = free
```
- **Mandatory:** the raw direct request→reply `Execute` (emitted for a flowless op) is made `internal` — a hard
  cross-assembly block — and same-assembly calls to it raise **PFNET0010**, steering every call through the op's
  flow. `[Notify]` `Notify(...)` stays allowed. The flow-backed `Execute` compiles fine: it stays `public`, and the
  analyzer flags only the `internal` direct shortcut (both return `Task<TReply>`, so accessibility is the tell).
- **Alt+Enter** on a PFNET0010 error → **"Create ServerOperation flow for `<Op>`"** inserts ONLY a `<Op>Flow` class
  into the op's OWN file (envelope types filled, ctor takes the request DTO; hook bodies are TODO). It adds NO
  `Execute` — the generator, seeing the flow, now emits the op's `Execute(<request DTO>)` (plus a members overload)
  that news the flow up, so the op class stays empty and the flow-backed `Execute` is the sole entry point.
- **Free** (default, line commented): both the direct `Execute` (flowless ops) and the flow-backed `Execute` are
  allowed. Either way it is generated; the op class is always empty.
- NOTE: after the analyzer DLL changes, **restart Unity once** so it loads the analyzer (Rider also needs
  Invalidate Caches / a reload for the same reason).

## Serialization (under the hood)
MessagePack's official source generator produces the formatters + `GameNetworkResolver` — AOT-safe, zero
reflection, no dynamic resolver. The poolable envelope is a runtime-only carrier; only the DTO crosses the wire.
See `Assets/PFound/NetworkLayer/MODULE.md` for framework internals.
