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
| `TimestampDelta * double` | `TimestampDelta` | scale a duration |
| `Timestamp + Timestamp` | — (no operator) | **intentionally undefined** — adding two points is meaningless |

Both types provide full ordering (`< > <= >= == !=`, `CompareTo`, `Equals`, `GetHashCode`) over their
single `double` backing field (`Timestamp.UnixSeconds`, `TimestampDelta.Seconds`). `TimestampDelta`
is signed — a negative delta is a duration into the past — and `IsPositive` is strictly `> 0`.

## Conversions

`Timestamp`:
- `Timestamp.FromUnixSeconds(double)` / `new Timestamp(double)` — from raw epoch seconds.
- `Timestamp.FromUtc(DateTime)` — from a UTC `DateTime` (via millisecond `DateTimeOffset`).
- `ToUtc()` — back to a UTC `DateTime`.
- `Timestamp.Zero` — the epoch itself.

`TimestampDelta`:
- `TimestampDelta.FromSeconds(double)` / `new TimestampDelta(double)` — from raw seconds.
- `TimestampDelta.FromTimeSpan(TimeSpan)` / `ToTimeSpan()` — bridge to `System.TimeSpan`.
- `TimestampDelta.Zero` — a zero duration.

## Notes

- Conversions go through `DateTimeOffset` millisecond APIs, so round-tripping a `DateTime` is
  millisecond-accurate, not tick-accurate.
- Precision is `double` seconds: near the epoch this is far below a millisecond, but it degrades as
  `UnixSeconds` grows — adequate for game timing, not for high-resolution measurement.
- `ToString()` is diagnostic only: `Timestamp` renders as `@<seconds>`, `TimestampDelta` as
  `<seconds>s`. Do not parse these.
