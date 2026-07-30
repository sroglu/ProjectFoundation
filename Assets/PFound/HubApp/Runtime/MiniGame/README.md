# HubApp.MiniGame

Mini-game contract and lifecycle orchestration.

- `IMiniGameModule` interface — game-specific code implements this on a MonoBehaviour in its scene
- `MiniGameContext` struct — scoped services passed at `Initialize()`
- `MiniGameHost` — additive scene load/unload + ServiceRegistry scope + GC cleanup
- `ScopedSaveService` — ensures mini-games can only touch their own save namespace
- Mini-game signals (`MiniGameStartedSignal`, `MiniGameCompletedSignal`, etc.) — decouple hub shell from specific mini-games

**Scope (planned):** see parent `MODULE.md`.
