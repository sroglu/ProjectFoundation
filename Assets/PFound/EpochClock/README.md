# PFound.EpochClock

A Unix-epoch game clock: an advancing `Now` plus duration/progress math, day/period boundaries, and
time comparisons, built on the value types `Timestamp` (seconds since the Unix epoch, UTC) and
`TimestampDelta` (a signed duration). Pure C# — no engine dependency.

## Model

- **`Timestamp`** — a UTC instant as `double UnixSeconds`; full ordering, arithmetic with
  `TimestampDelta`, and `FromUtc`/`ToUtc` conversions.
- **`TimestampDelta`** — a signed second-based duration; `+ - *`, ordering, `FromSeconds` /
  `FromTimeSpan` / `ToTimeSpan`, `IsPositive`, `Zero`.
- **`EpochClock`** (abstract) — reports `Now` (scaled game time) and `TickIndependentNow` (wall
  clock, for drift diagnosis). `TimeScale` multiplies elapsed real time. Subclasses decide how time
  advances on `Tick()`.
  - **`ClientEpochClock`** — production clock: `Tick()` accrues scaled real time (from an internal
    `Stopwatch`) onto a wall-clock base.
  - **`TestEpochClock`** — deterministic: `Tick()` is a no-op; time only moves via `AddDelta` /
    `AddSeconds` / `SetNow`.

## Public API (`EpochClock`)

Queries against `Now`: `IsPassed(time)`, `IsBetween(start, end)`,
`GetRemainingDurationOrZero(end)` (never negative), `GetCurrentProgressClamped(start, end)` (→ [0,1]),
`CalculateEndTime(TimeSpan)`. Day boundaries: `GetDayIndex(time)`, `GetDayStart(time)`,
`GetNextDayStartTime()` (next UTC midnight — e.g. a daily-reward reset gate).

## Setup / wiring

Pure library with a **per-frame driver requirement** — `new ClientEpochClock()`, no scene object.
`Now` only advances when someone calls `Tick()` each frame; nothing ticks it for you. The consumer
owns the instance (keep it in your bootstrap, or register it in `DependencyContainer`) and drives it:

```csharp
var clock = new ClientEpochClock();                 // production clock
LoopScheduler.RegisterUpdateLoop(clock.Tick, host); // drive one Tick per frame

if (clock.IsPassed(dailyResetAt)) GrantDailyReward();
float progress = clock.GetCurrentProgressClamped(startedAt, endsAt);
```

In tests use `TestEpochClock` and advance time by hand (`AddSeconds` / `SetNow`) — no driver needed.
Main-thread only.

## Layout

`Runtime/` — `EpochClock` (+ `ClientEpochClock` / `TestEpochClock`), `Timestamp`, `TimestampDelta`.
Assembly `PFound.EpochClock` (`noEngineReferences`, `autoReferenced:false`). `Tests/` — NUnit suite.

## Testing

Engine-free, so it runs under `csc`/`mono` as well as Unity EditMode. Drive `TestEpochClock`
deterministically to assert boundary/progress math without real time.
