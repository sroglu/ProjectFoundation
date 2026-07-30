# Pooling
Ergonomic scoped facades and a drain registry over `UnityEngine.Pool`.

**Key classes:** `PoolRegistry`, `PooledList<T>`, `PooledHashSet<T>`, `PooledDictionary<TKey,TValue>`, `DictionaryPool<TKey,TValue>`, `HashSetPool<T>`
**Assembly:** `PFound.Utilities.Pooling`
**Tier:** engine
**Depends on:** none

## Current API
- `PoolRegistry.Create<T>(...)` — thin pass-through to `UnityEngine.Pool.ObjectPool<T>` that
  registers the pool. `DrainAll()` clears every registered pool (e.g. on scene teardown /
  memory pressure); `DrainAndClearRegistry()` also forgets them; `RegisteredCount`.
- `PooledList<T>` / `PooledHashSet<T>` / `PooledDictionary<TKey,TValue>` — `readonly struct`
  disposables: `using var scope = PooledList<T>.New();` exposes `scope.Value` (or an implicit
  conversion to the collection) and returns it to the stdlib pool on dispose.
- `DictionaryPool<TKey,TValue>` / `HashSetPool<T>` — static rent/return facades with a
  ref-nulling `Release` overload.

## Wiring seam
No installation. Only pools created via `PoolRegistry.Create` are tracked by `DrainAll` — the
static collection pools (`ListPool<T>` etc.) are intentionally left untouched. The registry is
lock-guarded; the scoped structs are single-threaded borrow/return sugar.

## Limitations
- `PoolRegistry` tracks only pools it created, not Unity's own static collection pools.
- Each pooled scope must be disposed exactly once per `New()`; double-return corrupts the pool.
