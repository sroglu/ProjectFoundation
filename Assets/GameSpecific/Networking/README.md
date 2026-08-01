# GameSpecific.Networking

How this game talks to its servers — one networking assembly; the source generators + analyzer do the
boilerplate.

**Full reference + authoring guide → [MODULE.md](MODULE.md)** (DTOs, operations, flows, error handling, the
mandatory policy, the shared client+server wire contract, and the server side).

## 30-second orientation
- **Two server worlds coexist.** Realtime (this assembly, over `NetworkLayer` + `ServerOperationFlow`): live
  gameplay through a dedicated socket server. HTTP LiveOps (separate, BestHTTP): config / IAP / blob sync.
- Declare an op with `[RemoteProcedure]` / `[Notify]`; the class stays **empty** and the generator emits
  `Execute` / `Notify`. A reply is always a DTO; call sites are always `Op.Execute(args)`.
- A reply-bearing op can own a `<Op>Flow` (predict → send → interpret → apply). Errors funnel to one presenter
  keyed by the game's single `OpResult` enum.
- **Client and server share the wire contract** — same DTOs / opcodes / catalog; the server is the other end of
  the same messages (`#if BACKEND ServerPeer`), never a second copy. See MODULE.md.
