# PFound.NetworkLayer

Transport-agnostic request/reply/notify messaging for Unity clients and dedicated servers. Typed
messages ride opcode-tagged, length-prefixed frames over a pluggable link — Telepathy TCP in
production, in-process loopback for tests.

## Quick reference

```csharp
// shared catalog (same opcode→type map on both ends)
// Recommended: BANDED two-level opcodes — a tiny central NetDomain (high byte, one per subsystem) plus a
// per-domain op enum (low byte) with local values, folded to (domain << 8) | op. Different domain ⇒
// different high byte ⇒ cross-subsystem collisions are impossible. Each operation costs ONE op — the
// reply is opcode-less (decoded by correlation, pooled by type), so there is no separate reply op.
enum NetDomain : byte { Movement = 1 }   // value = opcode HIGH byte (0x01)
enum MoveOp    : byte { Move = 1 }
// The codec serializes each message's payload DTO through MessagePack's AOT source generator. A host pushes
// its assembly's generated resolver — the Instance of a [MessagePack.GeneratedMessagePackResolver] anchor —
// so there is zero runtime IL and zero reflection (IL2CPP-safe). No dynamic/contractless resolver.
var catalog = new MessageCatalog(new MessagePackBodyCodec(GameNetworkResolver.Instance));
catalog.Enroll<MoveRequest, MoveReply>(NetDomain.Movement, MoveOp.Move);   // request/reply PAIR — prefer this for RPC
// (Also: single-type Enroll<T>(NetDomain.X, XxxOp.Y) for a notify; Enroll<T>(Enum) for a flat enum; Enroll<T>(ushort) for a raw number.)
// (Re-enrolling the same opcode OR the same type throws NetworkFault — collisions fail fast, never silent.)

// client — pure library, consumer owns the peer
var link   = new TelepathyClientLink(ClientLinkOptions.Default);   // TCP transport
var client = new ClientPeer(link, catalog, ClientLinkOptions.Default);
await client.ConnectAsync("game.example.com", 7777);
MoveReply reply = await client.CallAsync<MoveReply>(new MoveRequest { X = 3 });

void Update() => client.Update();   // pump once per frame

// per-opcode round-trip latency + a slow-call alert (reuses the reply correlation)
client.Latency.SlowCall += r =>
    Debug.Log($"opcode {r.Opcode} took {r.RoundTripMs} ms (>= {r.ThresholdMs} ms)");
client.Latency.TryGet(Opcode.Of(NetDomain.Movement, MoveOp.Move), out var move);   // move.Completed / MinMs / MaxMs / AverageMs
```

## Dependencies

MessagePack-CSharp 3.1.8 — runtime + annotations vendored under `Runtime/Plugins/`, plus its official AOT
source generator (`MessagePack.SourceGenerator.dll`, a Roslyn analyzer under `Plugins/`); Telepathy (vendored
under `Runtime/`); ZString. Wire DTOs are authored as `[MessagePackObject]` types with `[Key]` members and
serialize through compile-time generated formatters — no runtime IL emit, no reflection.

## Docs

- Deep reference: [MODULE.md](MODULE.md)
- Third-party attributions: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
