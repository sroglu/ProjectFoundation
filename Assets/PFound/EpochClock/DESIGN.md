# EpochClock — time-value algebra

`Timestamp` and `TimestampDelta` form an **affine time model**: a `Timestamp` is a *point* on the
timeline (seconds since the Unix epoch, UTC), a `TimestampDelta` is a *displacement* (a signed
duration). The two are distinct types on purpose, so the operators only allow combinations that make
physical sense — the type system rejects nonsense like "add two dates" at compile time.

## Operator rules

| Expression | Result | Meaning |
|---|---|---|
| `Timestamp + TimestampDelta` | `Timestamp` | move a point forward by a duration |
| `Timestamp - TimestampDelta` | `Timestamp` | move a point backward by a duration |
| `Timestamp - Timestamp` | `TimestampDelta` | the (signed) duration between two points |
| `TimestampDelta + TimestampDelta` | `TimestampDelta` | sum of durations |
| `TimestampDelta - TimestampDelta` | `TimestampDelta` | difference of durations |
| `-TimestampDelta` | `TimestampDelta` | negate (flip the direction of a duration) |
| `TimestampDelta * (double\|int\|long)` | `TimestampDelta` | scale a duration (commutative) |
| `TimestampDelta / (double\|int\|long)` | `TimestampDelta` | divide a duration by a scalar |
| `TimestampDelta / TimestampDelta` | `double` | dimensionless ratio (how many times one fits the other) |
| `TimestampDelta % TimestampDelta` | `TimestampDelta` | remainder — the offset within a period |
| `Timestamp + Timestamp` | — (no operator) | **intentionally undefined** — adding two points is meaningless |

Both types provide full ordering (`< > <= >= == !=`, `CompareTo`, `Equals`, `GetHashCode`) over their
single `double` backing field (`Timestamp.UnixSeconds`, `TimestampDelta.Seconds`). `TimestampDelta`
is signed — a negative delta is a duration into the past — and `IsPositive` is strictly `> 0`.

The delta-÷-delta ratio and delta-%-delta remainder are what power the period family:
`GetPeriodIndex(period)` is `(long)(durationSinceEpoch / period)` (truncated toward the epoch), and a
period offset falls out of `durationSinceEpoch % period`.

## Interpolation & clamping

`Timestamp` carries the affine-space interpolation pair:

- `Timestamp.Lerp(start, end, t)` — the point at fraction `t` between two instants (`t` un-clamped).
- `Timestamp.Unlerp(start, end, x)` — the inverse: where `x` falls in `[start, end]` as a fraction,
  un-clamped (may fall outside `[0,1]`).
- `Timestamp.Clamp / Min / Max` and `TimestampDelta.Clamp / Min / Max` — bound a value or pick an
  extreme, over the same `double` field.

The clock's progress helpers layer on top: `GetCurrentProgressUnClamped(start, end)` is
`Unlerp(start, end, Now)` in spirit (returned as `float`, with a zero-span guard), and the `Clamped`
variants pin the result into `[0,1]`. The `(end, totalDuration)` overloads derive `start` as
`end - totalDuration`, so an operation known only by its finish time and length can still report
progress.

## Second rounding

`Timestamp` rounds to whole-second boundaries three ways: `FloorToSecond` (largest whole second at or
before), `CeilToSecond` (smallest at or after), `RoundToSecond` (nearest). These back the clock's
`NowRoundedDown` / `NowRoundedUp` and the `CalculateEndTimeRoundedUp` / `CalculateStartTimeRoundedDown`
helpers — the direction is chosen so an operation never *appears* to finish early and a past instant
never lands in the future.

## Decomposition & units

`TimestampDelta` reads out both **component** and **total** breakdowns against `CalendarConstants`:

- Components (`Days`, `Hours`, `Minutes`) give the piece within the next-larger unit — `Hours` is
  `[0,23]`, `Minutes` is `[0,59]` for positive durations.
- Totals (`TotalDays`, `TotalHours`, `TotalMinutes`) give the whole count of that unit across the
  entire duration.

Unit constants (`OneSecond … OneYear`) and factories (`FromMinutes/Hours/Days`) are built from the
`CalendarConstants` table; `OneMonth` / `OneYear` (and the corresponding constants) are nominal
30-/365-day approximations — fine for game cadence, not civil-calendar math.

## Conversions

`Timestamp`:
- `Timestamp.FromUnixSeconds(double)` / `new Timestamp(double)` — from raw epoch seconds.
- `Timestamp.FromUtc(DateTime)` — from a UTC `DateTime` (via millisecond `DateTimeOffset`).
- `ToUtc()` — back to a UTC `DateTime`; `ToFloorRoundedMilliseconds()` — floored ms for integer keys.
- `Timestamp.Zero` / `MinValue` / `MaxValue`; `TryParse(string, out Timestamp)`.

`TimestampDelta`:
- `TimestampDelta.FromSeconds/Minutes/Hours/Days(double)` / `new TimestampDelta(double)`.
- `TimestampDelta.FromTimeSpan(TimeSpan)` / `ToTimeSpan()` — bridge to `System.TimeSpan`.
- `TimestampDelta.Zero` / unit constants / `MinValue` / `MaxValue`; `TryParse(string, out …)`.

## Notes

- Both structs are `[Serializable]` (single `double` field), so they survive Unity serialization and
  save payloads directly.
- Conversions go through `DateTimeOffset` millisecond APIs, so round-tripping a `DateTime` is
  millisecond-accurate, not tick-accurate.
- Precision is `double` seconds: near the epoch this is far below a millisecond, but it degrades as
  `UnixSeconds` grows — adequate for game timing, not for high-resolution measurement.
- `ToString()` is diagnostic only: `Timestamp` renders as `@<seconds>`, `TimestampDelta` as
  `<seconds>s`. `TryParse` reads a plain `double` (not this decorated form); do not round-trip the
  decorated string.
