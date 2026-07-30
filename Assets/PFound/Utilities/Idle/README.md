# Idle
Detects user inactivity: flips to "idle" after a quiet span and fires an edge event when activity resumes.

**Key classes:** `IdleWatcher`
**Assembly:** `PFound.Utilities.Idle`
**Tier:** engine
**Depends on:** `UniTask`

## Current API
- `new IdleWatcher(TimeSpan idleThreshold, Func<bool> activityProbe = null, TimeSpan? pollInterval = null, Func<double> clock = null)` — clock defaults to Unity's unscaled realtime; injectable for tests.
- `event Action<bool> OnIdleChanged` — fires on the idle/active edge.
- `bool CurrentlyIdle`, `bool IsRunning`.
- `Poke()` — register activity now (resets the quiet timer, flips back to active).
- `Start()` / `Stop()` — drive the internal async poll loop (UniTask).
- `Tick()` — advance the watcher manually from your own update loop instead of `Start()`.
- `Dispose()` — stops the loop.

## Limitations
- Plain C# class, no MonoBehaviour host — the consumer owns the instance and its lifecycle.
- Async loop uses UniTask; call `Tick()` yourself if you prefer to avoid the internal loop.
