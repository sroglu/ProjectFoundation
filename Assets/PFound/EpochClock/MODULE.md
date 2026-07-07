# EpochClock

## Purpose
A Unix-epoch game clock: an advancing `Now` plus duration/progress math, day/period boundaries, and
time comparisons — built on the value types `Timestamp` (seconds since the Unix epoch, UTC) and
`TimestampDelta` (a signed duration). Pure C# — no engine dependency.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.EpochClock` | `Runtime/PFound.EpochClock.asmdef` | `noEngineReferences: true`, `autoReferenced: false` |
| `PFound.EpochClock.Tests` | `Tests/PFound.EpochClock.Tests.asmdef` | NUnit suite, `noEngineReferences: true` |

## Dependencies
None — BCL only (`System`, `System.Diagnostics.Stopwatch`, `DateTimeOffset`). No PFound module, no
third-party package, no scripting define.

## Key Types

### `PFound.EpochClock`
- **`Timestamp`** (readonly struct) — a UTC instant as `double UnixSeconds`. Full ordering, arithmetic
  with `TimestampDelta`, `FromUtc` / `ToUtc` conversions.
- **`TimestampDelta`** (readonly struct) — a signed second-based duration. `+ - *`, ordering,
  `FromSeconds` / `FromTimeSpan` / `ToTimeSpan`, `IsPositive`, `Zero`.
- **`EpochClock`** (abstract) — reports `Now` (scaled game time) and `TickIndependentNow` (wall
  clock, for drift diagnosis); `TimeScale` multiplies elapsed real time; subclasses decide how time
  advances on `Tick()`. Provides the query/boundary helpers below.
- **`ClientEpochClock`** (sealed) — production clock: `Tick()` accrues scaled real time (from an
  internal `Stopwatch`) onto a wall-clock base captured at construction.
- **`TestEpochClock`** (sealed) — deterministic: `Tick()` is a no-op; time only moves via `AddDelta`
  / `AddSeconds` / `SetNow`.

## Public API

`EpochClock` (abstract base):
```csharp
float TimeScale { get; set; }              // multiplier applied to elapsed real time; default 1
Timestamp Now { get; }                     // current scaled game time (abstract)
Timestamp TickIndependentNow { get; }      // wall clock, independent of ticking/scale (abstract)
void Tick();                               // advance Now (abstract)

bool IsPassed(Timestamp time);
bool IsBetween(Timestamp start, Timestamp end);
TimestampDelta GetRemainingDurationOrZero(Timestamp end);       // never negative
float GetCurrentProgressClamped(Timestamp start, Timestamp end); // -> [0,1]
Timestamp CalculateEndTime(TimeSpan duration);

long GetDayIndex(Timestamp time);          // whole UTC days since the epoch
Timestamp GetDayStart(Timestamp time);     // UTC midnight at or before time
Timestamp GetNextDayStartTime();           // next UTC midnight after Now (e.g. daily-reward reset gate)
```

`ClientEpochClock`: `new ClientEpochClock()`.
`TestEpochClock`: `new TestEpochClock(Timestamp start = default)`, plus `AddDelta(TimestampDelta)`,
`AddSeconds(double)`, `SetNow(Timestamp)`.

For the `Timestamp` / `TimestampDelta` operator and conversion rules see [DESIGN.md](DESIGN.md).

## Setup / wiring

Pure library with a **per-frame driver requirement** — `new ClientEpochClock()`, no scene object.
`Now` only advances when someone calls `Tick()` each frame; nothing ticks it for you. The consumer
owns the instance (keep it in your bootstrap, or register it in `DependencyContainer`) and drives it.

**Who calls `Tick()`:** a single per-frame call site you choose — a `LoopScheduler` Update callback,
a host `MonoBehaviour.Update`, or your own frame loop. One `Tick()` per frame; skipping it freezes
`Now` (while `TickIndependentNow` keeps reporting the wall clock, which is how you diagnose drift).

```csharp
var clock = new ClientEpochClock();                 // production clock
LoopScheduler.RegisterUpdateLoop(clock.Tick, host); // drive one Tick per frame

if (clock.IsPassed(dailyResetAt)) GrantDailyReward();
float progress = clock.GetCurrentProgressClamped(startedAt, endsAt);
```

In tests use `TestEpochClock` and advance time by hand (`AddSeconds` / `SetNow`) — no driver needed.
Main-thread only.

## File Structure
```
EpochClock/
  README.md
  MODULE.md
  DESIGN.md               # Timestamp/TimestampDelta operator + conversion algebra
  Runtime/
    PFound.EpochClock.asmdef
    EpochClock.cs         # abstract EpochClock + ClientEpochClock + TestEpochClock
    Timestamp.cs          # UTC instant value type
    TimestampDelta.cs     # signed duration value type
  Tests/
    PFound.EpochClock.Tests.asmdef
    EpochClockTests.cs
```

## Downstream Dependents
None within PFound (only the module's own test assembly references it).

## Limitations / Known Gaps
- **Needs an external `Tick()` driver.** Nothing advances `ClientEpochClock.Now` for you; forget the
  driver and time freezes.
- **Main-thread only.** No synchronization around `Tick()` / `Now`.
- **`double`-second precision.** Sub-millisecond precision degrades far from the epoch; fine for game
  time, not a substitute for high-resolution timing.
- **UTC only.** Day boundaries are UTC midnights (`GetDayStart` / `GetNextDayStartTime`); local-time
  or timezone-aware resets are the consumer's job.
- **No wall-clock rollback protection.** `ClientEpochClock` bases `Now` on the wall clock captured at
  construction plus stopwatch-measured elapsed time; a user changing the device clock mid-session is
  not reconciled here.
