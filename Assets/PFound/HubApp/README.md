# HubApp

Project-specific integration layer for kid-safe hub apps built on the PFound Unity framework. Provides save persistence, cross-game services (profile / badges / stickers / photos / audio / parent gate / IAP / analytics), and a mini-game module contract with lifecycle orchestration.

Currently scaffolded for **Playnest** (Toddler Games Hub App). See [`MODULE.md`](MODULE.md) for scope and quick start.

**Status:** v0.0.0 — scaffold only. Runtime implementation in progress (see consuming project's `docs/IMPLEMENTATION.md` → Faz 1).

## Dependencies

- `mehmetsrl.ServiceRegistry`
- `mehmetsrl.Signaling`
- `mehmetsrl.Navigation`
- `mehmetsrl.Localization`
- `mehmetsrl.Utilities` + `mehmetsrl.Utilities.FileSystemTools`
- `mehmetsrl.GameTime`

## License

Proprietary.
