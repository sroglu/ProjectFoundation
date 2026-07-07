# HubApp

Project-specific integration layer for kid-safe hub apps built on the PFound Unity framework. Provides save persistence, cross-game services (profile / badges / stickers / photos / audio / parent gate / IAP / analytics), and a mini-game module contract with lifecycle orchestration.

Currently scaffolded for **Playnest** (Toddler Games Hub App). See [`MODULE.md`](MODULE.md) for scope and quick start.

**Status:** v0.0.0 — scaffold only. Runtime implementation in progress (see consuming project's `docs/IMPLEMENTATION.md` → Faz 1).

## Dependencies

- `PFound.DependencyContainer`
- `PFound.Signaling`
- `PFound.ScreenRouter`
- `PFound.LocalizationService`
- `PFound.Utilities` + `PFound.Utilities.FileSystemTools`
- `PFound.EpochClock`

## License

Proprietary.
