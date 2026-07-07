# PFound.EpochClock

A Unix-epoch game clock: an advancing `Now` plus duration/progress math, day/period boundaries, and
time comparisons — built on the value types `Timestamp` (UTC epoch seconds) and `TimestampDelta` (a
signed duration). Pure C#, no engine dependency.

## Quick reference

```csharp
var clock = new ClientEpochClock();                 // production clock
LoopScheduler.RegisterUpdateLoop(clock.Tick, host); // MUST drive one Tick() per frame

if (clock.IsPassed(dailyResetAt)) GrantDailyReward();
float progress = clock.GetCurrentProgressClamped(startedAt, endsAt); // -> [0,1]
Timestamp reset = clock.GetNextDayStartTime();      // next UTC midnight

// tests: deterministic, no driver
var t = new TestEpochClock();
t.AddSeconds(3600);
```

## Dependencies

None — BCL only (`noEngineReferences`, `autoReferenced:false`).

## Docs

- Deep reference: [MODULE.md](MODULE.md) — clock model, full API, and the per-frame `Tick()` driver
  requirement (who calls it).
- Time-value algebra: [DESIGN.md](DESIGN.md) — `Timestamp` / `TimestampDelta` operator and
  conversion rules (affine point-vs-duration model).
