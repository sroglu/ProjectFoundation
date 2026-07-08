# PFound.NetworkLayer

Transport-agnostic request/reply/notify messaging for Unity clients and dedicated servers. Typed
messages ride opcode-tagged, length-prefixed frames over a pluggable link — Telepathy TCP in
production, in-process loopback for tests.

## Quick reference

```csharp
// shared catalog (same opcode→type map on both ends)
var catalog = new MessageCatalog(new MessagePackBodyCodec());
catalog.Enroll<MoveRequest>(1042);
catalog.Enroll<MoveReply>(1043);

// client — pure library, consumer owns the peer
var link   = new TelepathyClientLink(ClientLinkOptions.Default);   // TCP transport
var client = new ClientPeer(link, catalog, ClientLinkOptions.Default);
await client.ConnectAsync("game.example.com", 7777);
MoveReply reply = await client.CallAsync<MoveReply>(new MoveRequest { X = 3 });

void Update() => client.Update();   // pump once per frame

// per-opcode round-trip latency + a slow-call alert (reuses the reply correlation)
client.Latency.SlowCall += r =>
    Debug.Log($"opcode {r.Opcode} took {r.RoundTripMs} ms (>= {r.ThresholdMs} ms)");
client.Latency.TryGet(1042, out var move);   // move.Completed / MinMs / MaxMs / AverageMs
```

## Dependencies

MessagePack-CSharp + Telepathy (both vendored under `Runtime/`); ZString.

## Docs

- Deep reference: [MODULE.md](MODULE.md)
- Third-party attributions: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
