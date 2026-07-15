# Pause
Reference-counted pause coordination: any number of callers hold a `PauseHandle`; the app stays paused until the last one is released.

**Key classes:** `PauseCoordinator`, `PauseHandle`
**Assembly:** `PFound.Utilities.Pause`
**Tier:** core
**Depends on:** none

## Current API
- `PauseCoordinator.Acquire(string reason = null)` — returns a `PauseHandle`; the first
  outstanding handle raises `PauseChanged(true)`.
- `PauseCoordinator` — `IsPaused`, `ActiveCount`, `ActiveReasons` (snapshot in acquisition
  order), `ReleaseAll()`, and `event Action<bool> PauseChanged` (fires only on
  running↔paused transitions).
- `PauseHandle : IDisposable` — `Release()` / `Dispose()` (idempotent alias), `IsHeld`,
  `Reason`. Works with `using` blocks.

## Wiring seam
Deliberately engine-free (no `UnityEngine` reference) so the counting logic runs under plain
csc/mono. Consumers own the lifetime: `new PauseCoordinator()`, share it, and each subsystem
`Acquire`s a handle while it needs the app paused. Bridging the `PauseChanged` edges to
`Time.timeScale`, audio, or input is the consumer's responsibility.

## Limitations
- Single-threaded main-loop use; not thread-safe.
- Pure state/counting only — it does not itself stop time, audio, or input.
