# EpochClock

## Purpose
A Unix-epoch game clock: an advancing `Now` plus duration/progress math, period and day/week
boundaries, drift diagnostics, and time comparisons — built on the value types `Timestamp` (seconds
since the Unix epoch, UTC) and `TimestampDelta` (a signed duration), with a fixed `CalendarConstants`
arithmetic table. Pure C# — no engine dependency.

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
- **`Timestamp`** (`[Serializable]` readonly struct) — a UTC instant as `double UnixSeconds`. Full
  ordering, arithmetic with `TimestampDelta`, `FromUtc` / `ToUtc` conversions, second-rounding,
  `Lerp` / `Unlerp` / `Clamp`, period and calendar-day helpers.
- **`TimestampDelta`** (`[Serializable]` readonly struct) — a signed second-based duration. Full
  operator algebra (`+ - * / %`), ordering, unit factories/decomposition, unit constants,
  `Clamp` / `Max` / `Min`.
- **`CalendarConstants`** (static) — fixed seconds/minutes/hours-per-unit table plus ready-made
  `TimeSpan` unit values; month/year use nominal 30-/365-day approximations.
- **`EpochClock`** (abstract) — reports `Now` (scaled game time) and `TickIndependentNow` (wall
  clock, for drift diagnosis); `TimeScale` multiplies elapsed real time; records `LastTickDelta`;
  subclasses decide how time advances on `Tick()`. Provides the query/boundary/period/drift helpers
  below.
- **`ClientEpochClock`** (sealed) — production clock: `Tick()` accrues scaled real time (from an
  internal `Stopwatch`) onto a wall-clock base captured at construction.
- **`TestEpochClock`** (sealed) — deterministic: `Tick()` is a no-op; time only moves via `AddDelta`
  / `AddSeconds` / `SetNow`.

## Public API

### `EpochClock` (abstract base)

Time / ticking:
```csharp
const double DriftToleranceSeconds = 0.1;      // drift beyond this is reported
static Action<string> DriftLogger;             // sink for drift reports; defaults to stderr

float TimeScale { get; set; }                  // multiplier applied to elapsed real time; default 1
Timestamp Now { get; }                         // current scaled game time (abstract)
Timestamp TickIndependentNow { get; }          // wall clock, independent of ticking/scale (abstract)
void Tick();                                    // advance Now (abstract)
TimestampDelta LastTickDelta { get; }           // amount Now moved on the most recent advance
Timestamp NowRoundedUp { get; }                 // Now ceiled to a whole second
Timestamp NowRoundedDown { get; }               // Now floored to a whole second
TimeSpan CurrentHourOfDay { get; }              // time-of-day within the compressed in-game day
```

Comparison:
```csharp
bool IsPassed(Timestamp time);                  // Now >= time
bool IsBetween(Timestamp start, Timestamp end); // start <= Now <= end
```

Passed / remaining duration:
```csharp
TimestampDelta GetPassedDuration(Timestamp time);        // signed, Now - time
TimestampDelta GetPassedDurationOrZero(Timestamp time);  // never negative
TimestampDelta GetRemainingDuration(Timestamp end);      // signed, end - Now
TimestampDelta GetRemainingDurationOrZero(Timestamp end); // never negative
```

Progress:
```csharp
float GetCurrentProgressClamped(Timestamp start, Timestamp end);            // -> [0,1]
float GetCurrentProgressUnClamped(Timestamp start, Timestamp end);          // may exit [0,1]
float GetCurrentProgressClamped(Timestamp end, TimestampDelta totalDuration);   // by end + duration
float GetCurrentProgressUnClamped(Timestamp end, TimestampDelta totalDuration); // by end + duration
```

Start / end time:
```csharp
Timestamp CalculateEndTime(TimeSpan duration);
Timestamp CalculateEndTime(TimestampDelta duration);
Timestamp CalculateEndTimeRoundedUp(TimestampDelta duration);      // ceiled so it never finishes early
Timestamp CalculateStartTime(TimeSpan duration);
Timestamp CalculateStartTime(TimestampDelta duration);
Timestamp CalculateStartTimeRoundedDown(TimestampDelta duration);  // floored so a past instant stays past
```

Periods (fixed-length buckets counted from the epoch — daily/weekly resets, cyclic slots):
```csharp
TimestampDelta GetDurationSinceEpoch();                              // epoch -> Now
long GetCurrentPeriodIndex(TimestampDelta period);                   // bucket Now falls in
long GetCurrentPeriodIndex(TimestampDelta period, long maxIndexExclusive); // wrapped into [0,max)
long GetPassedPeriod(TimestampDelta period, Timestamp startTime);          // signed whole periods
long GetPassedPeriodOrZero(TimestampDelta period, Timestamp startTime);    // never negative
long GetRemainingPeriod(TimestampDelta period, Timestamp endTime);        // signed whole periods
long GetRemainingPeriodOrZero(TimestampDelta period, Timestamp endTime);  // never negative
```

Day / week boundaries (UTC):
```csharp
long GetDayIndex(Timestamp time);              // whole UTC days since the epoch
Timestamp GetDayStart(Timestamp time);         // UTC midnight at or before time
Timestamp GetCurrentDayStartTime();            // UTC midnight of the current day
Timestamp GetNextDayStartTime();               // next UTC midnight after Now (e.g. daily-reward reset)
Timestamp GetStartOfTargetDay(DayOfWeek dayOfWeek); // next future UTC midnight on that weekday (weekly reset)
```

Drift diagnostics:
```csharp
TimestampDelta GetDriftError();                // TickIndependentNow - Now
bool IsDriftWithinTolerance();                 // |drift| <= DriftToleranceSeconds
bool CheckDriftWithinTolerance();              // reports via DriftLogger once past tolerance; returns in-tolerance
```

`ClientEpochClock`: `new ClientEpochClock()`.
`TestEpochClock`: `new TestEpochClock(Timestamp start = default)`, plus `AddDelta(TimestampDelta)`,
`AddSeconds(double)`, `SetNow(Timestamp)`.

### `Timestamp`
```csharp
readonly double UnixSeconds;
static readonly Timestamp Zero, MinValue, MaxValue;

static Timestamp FromUnixSeconds(double seconds);
static Timestamp FromUtc(DateTime utc);
DateTime ToUtc();
Timestamp FloorToSecond();  Timestamp CeilToSecond();  Timestamp RoundToSecond();
ulong ToFloorRoundedMilliseconds();

static Timestamp Lerp(Timestamp start, Timestamp end, double t);
static double Unlerp(Timestamp start, Timestamp end, Timestamp x);   // un-clamped inverse lerp
static Timestamp Clamp(Timestamp value, Timestamp min, Timestamp max);
static Timestamp Max(Timestamp a, Timestamp b);  static Timestamp Min(Timestamp a, Timestamp b);

TimestampDelta GetDurationSinceEpoch();
long GetPeriodIndex(TimestampDelta period);      // throws if period <= 0
Timestamp GetStartOfDay();  Timestamp GetStartOfNextDay();
TimeSpan ToScaledDayTime(double dayLengthInSeconds); // time-of-day within a compressed in-game day

static bool TryParse(string text, out Timestamp value);
// + operators (see DESIGN.md), CompareTo / Equals / GetHashCode / ToString ("@<seconds>")
```

### `TimestampDelta`
```csharp
readonly double Seconds;
static readonly TimestampDelta Zero, OneSecond, OneMinute, OneHour, OneDay, OneWeek,
                               OneMonth /*approx*/, OneYear /*approx*/, MinValue, MaxValue;

static TimestampDelta FromSeconds(double), FromMinutes(double), FromHours(double),
                      FromDays(double), FromTimeSpan(TimeSpan);
TimeSpan ToTimeSpan();

bool IsPositive;                                 // strictly > 0
long Days, Hours, Minutes;                       // component decomposition within the larger unit
long TotalDays, TotalHours, TotalMinutes;        // whole units spanned

static TimestampDelta Clamp(TimestampDelta value, TimestampDelta min, TimestampDelta max);
static TimestampDelta Max(TimestampDelta a, TimestampDelta b);
static TimestampDelta Min(TimestampDelta a, TimestampDelta b);

static bool TryParse(string text, out TimestampDelta value);
// + - * / % operators (see DESIGN.md), CompareTo / Equals / GetHashCode / ToString ("<seconds>s")
```

### `CalendarConstants`
```csharp
const int DefaultHourOfDay = 12;
const int DefaultDayLengthSeconds = 900;         // one compressed in-game day, real seconds
const int SecondsInMinute, MinutesInHour, HoursInDay, DaysInWeek, DaysInMonth /*approx*/,
          DaysInYear /*approx*/, WeeksInMonth /*approx*/, WeeksInYear /*approx*/, MonthsInYear;
const int SecondsInHour, SecondsInDay, SecondsInWeek, SecondsInMonth /*approx*/, SecondsInYear /*approx*/;
const int MinutesInDay, MinutesInWeek, MinutesInMonth /*approx*/, MinutesInYear /*approx*/;
const int HoursInWeek, HoursInMonth /*approx*/, HoursInYear /*approx*/;
static readonly TimeSpan OneSecond, OneMinute, OneHour, OneDay, OneWeek,
                         OneMonth /*approx*/, OneYear /*approx*/;
```

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
long dailyIndex = clock.GetCurrentPeriodIndex(TimestampDelta.OneDay); // which day-bucket we are in
```

In tests use `TestEpochClock` and advance time by hand (`AddSeconds` / `SetNow`) — no driver needed.
Main-thread only.

**Drift diagnosis (optional):** wire `EpochClock.DriftLogger` to your engine logger and call
`CheckDriftWithinTolerance()` periodically; it reports once when `Now` diverges from the wall clock
past `DriftToleranceSeconds` (0.1s).

## File Structure
```
EpochClock/
  README.md
  MODULE.md
  DESIGN.md                 # Timestamp/TimestampDelta operator + conversion algebra
  Runtime/
    PFound.EpochClock.asmdef
    EpochClock.cs           # abstract EpochClock + ClientEpochClock + TestEpochClock
    Timestamp.cs            # UTC instant value type
    TimestampDelta.cs       # signed duration value type
    CalendarConstants.cs    # fixed calendar arithmetic table + TimeSpan unit values
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
- **UTC only.** Day boundaries are UTC midnights (`GetDayStart` / `GetNextDayStartTime` /
  `GetStartOfTargetDay`); local-time or timezone-aware resets are the consumer's job.
- **Approximate month/year.** `CalendarConstants` (and `TimestampDelta.OneMonth` / `OneYear`) use
  nominal 30-/365-day figures — adequate for game cadence, not civil-calendar accuracy.
- **No wall-clock rollback protection.** `ClientEpochClock` bases `Now` on the wall clock captured at
  construction plus stopwatch-measured elapsed time; a user changing the device clock mid-session is
  not reconciled here (only surfaced via the drift diagnostics).
