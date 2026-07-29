# PFound.NetworkLayer

Transport-agnostic request/reply/notify messaging for Unity clients and dedicated servers. Typed
messages ride opcode-tagged, length-prefixed frames over a pluggable link — Telepathy TCP in
production, in-process loopback for tests.

## Quick reference

```csharp
// shared catalog (same opcode→type map on both ends)
// Recommended: BANDED two-level opcodes — a tiny central NetDomain (high byte, one per subsystem) plus a
// per-domain op enum (low byte) with local values, folded to (domain << 8) | op. Different domain ⇒
// different high byte ⇒ cross-subsystem collisions are impossible. Request and reply get distinct ops.
enum NetDomain : byte { Movement = 1 }   // value = opcode HIGH byte (0x01)
enum MoveOp    : byte { Request  = 1, Reply = 2 }
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest>(NetDomain.Movement, MoveOp.Request);   // banded overload — prefer this
catalog.Enroll<MoveReply>(NetDomain.Movement, MoveOp.Reply);
// (Also: Enroll<T>(Enum) for a single flat enum, or Enroll<T>(ushort) for a raw number.)
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
client.Latency.TryGet(Opcode.Of(NetDomain.Movement, MoveOp.Request), out var move);   // move.Completed / MinMs / MaxMs / AverageMs
```

## Dependencies

MessagePack-CSharp + Telepathy (both vendored under `Runtime/`); ZString.

## Docs

- Deep reference: [MODULE.md](MODULE.md)
- Third-party attributions: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
