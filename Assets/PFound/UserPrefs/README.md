# PFound.UserPrefs

A typed, schema-versioned player-preferences store built with a fluent builder. Keys are strongly
typed `PrefKey<T>`, each value is routed to a backend (Unity `PlayerPrefs` for primitives, an atomic
JSON file for complex types), reads/writes are change-observable, and a migration chain upgrades an
older on-disk schema to the current one.

## Model

- **`PrefKey<T>`** — `new PrefKey<T>(string key, T defaultValue, PrefStorage storage = Auto, bool
  requiresEncryption = false)`. Reading a missing key returns `DefaultValue`. Keys may not start with
  the reserved `__userprefs.` prefix.
- **Storage routing (`PrefStorage`)** — `Auto` (primitives → `PlayerPrefs`, complex → `JsonFile`),
  `PlayerPrefs`, `JsonFile`. `PlayerPrefs`-supported types: `bool, int, long, float, double, string,
  Enum` — anything else forced there throws.
- **Backends (`IPrefsBackend`)** — `PlayerPrefsBackend`, `JsonFileBackend` (writes
  `Application.persistentDataPath/<file>` atomically via a temp file), `MemoryBackend` (tests). Both
  persistent backends track a dirty flag, so `Flush` is a no-op when nothing changed.
- **Schema version + migrations** — the store records `SchemaVersion`; on load, sequential
  `MigrationFrom(v, v+1, ...)` steps upgrade stored data. A downgrade (stored > current) or a broken
  chain fails fast.

## Public API (`IPrefsStore`)

`int SchemaVersion`, `bool IsLoaded`.
- `T Get<T>(PrefKey<T>)` — stored value or default.
- `void Set<T>(PrefKey<T>, T)` — writes; fires a change only when the value actually differs.
- `void SetDeferred<T>(...)` — reserved for batched writes (currently same as `Set`).
- `void Clear<T>(PrefKey<T>)` — deletes; change reports `OldValue → DefaultValue`.
- `void Flush()` / `Task FlushAsync(CancellationToken = default)` — persist both backends.
- `IDisposable Subscribe<T>(PrefKey<T>, Action<PrefChange<T>>)` — dispose to unsubscribe.
- `void Dispose()` — flushes, detaches the lifecycle hook, drops subscribers.

`PrefChange<T>` carries `Key`, `OldValue`, `NewValue`, `Kind` (`Set` / `Cleared` / `MigratedIn`).
Calling `Get`/`Set`/`Clear`/`Subscribe` before `IsLoaded` throws `PrefsNotLoadedException`; after
`Dispose`, `ObjectDisposedException`.

## Setup / wiring

Build the store once at app startup. `PrefsBuilder.Build()` constructs the store, loads both
backends, runs the migration chain, installs the lifecycle hook, and returns an already-loaded
`IPrefsStore`:

```csharp
static readonly PrefKey<int>   Level  = new PrefKey<int>("game.level", 1);
static readonly PrefKey<float> Volume = new PrefKey<float>("audio.volume", 0.8f);

IPrefsStore store = new PrefsBuilder()
    .Add(Level)
    .Add(Volume)
    .SchemaVersion(2)
    .MigrationFrom(1, 2, ctx => ctx.PrimitiveBackend.Set(
        "game.level", ctx.PrimitiveBackend.TryGet<int>("legacy.level", out var v) ? v : 1))
    .WithJsonStorage("userprefs.json")     // filename only → persistentDataPath
    .Build();                              // store.IsLoaded == true here

int level = store.Get(Level);
store.Set(Volume, 1.0f);                   // fires a PrefChange<float>
using var sub = store.Subscribe(Volume, c => AudioMixer.Set(c.NewValue));
store.Flush();                             // or rely on the lifecycle hook (below)
```

Builder surface: `Add<T>(PrefKey<T>)`, `SchemaVersion(int ≥ 1)`,
`MigrationFrom(int from, int to /* == from+1 */, Action<MigrationContext>)`,
`WithJsonStorage(string fileName)`, `WithBackend(IPrefsBackend, PrefStorage role)` (custom backend
for the `PlayerPrefs` or `JsonFile` role), `WithSettings(UserPrefsSettings)`,
`WithLogger(IPrefsLogger)`. `Build()` may be called only once.

Decisions a consumer makes:

- **Lifecycle / autosave is automatic.** `Build()` spawns a hidden `DontDestroyOnLoad` MonoBehaviour
  (`[UserPrefsLifecycle]`, `HideAndDontSave`) that `Flush()`es on `OnApplicationPause(true)` and
  `OnApplicationQuit`. You do not place any component yourself; `Dispose()` tears it down. Explicit
  `Flush()` is still available for save points.
- **Where to build it.** Any bootstrap object that runs once — the store is a plain object, not a
  MonoBehaviour. Keep the single instance and the static `PrefKey<T>` definitions somewhere shared.
- **Settings ScriptableObject (optional).** `UserPrefsSettings` (Create ▸
  `GameSpecific/UserPrefsSettings`) holds `JsonFileName`, `VerboseLogging`,
  `EditorResetClearsPlayerPrefs`. Pass it via `WithSettings(...)`; when omitted the builder falls
  back to `"userprefs.json"` + non-verbose defaults. In the editor the `UserPrefsAssetProvider`
  (an `IGameSpecificAssetProvider`) auto-provisions the asset at
  `Assets/GameSpecific/UserPrefs/UserPrefsSettings.asset` via the GameSpecific guard.
- **No DI registration required** — the builder is the composition root; hand the returned
  `IPrefsStore` to consumers however you prefer.

Reference the `PFound.UserPrefs` assembly from a consumer asmdef.

## Migrations

Register one step per version bump; each `MigrationContext` exposes `PrimitiveBackend`
(`PlayerPrefs`-routed data) and `JsonData` (`IDictionary<string, object>` for JSON-routed data) plus
`TryGetJsonKey<T>`, `SetJsonKey`, `RemoveJsonKey`. Steps must form a continuous chain up to
`SchemaVersion`; a gap or a thrown step raises `PrefsMigrationException` and leaves the stored version
untouched, and a stored version newer than current raises `PrefsSchemaDowngradeException`.

## Testing

`Tests/EditAndPlayModes/` — NUnit Edit+Play suites over the store, both backends, change events,
migrations, edge cases, and allocations (`MemoryBackend` isolates the pure logic from disk).

## Layout

- `Runtime/` — `PrefsBuilder`, `PrefsStore`/`IPrefsStore`, `PrefKey`, `PrefChange`, `PrefStorage`,
  `UserPrefsSettings`, `IPrefsLogger`, exceptions; `Backends/`, `Migration/`, `Lifecycle/`. Assembly
  `PFound.UserPrefs`.
- `Editor/` — `UserPrefsAssetProvider`, prefs inspector window + menu items. Assembly
  `PFound.UserPrefs.Editor`.
- `Tests/EditAndPlayModes/`.
