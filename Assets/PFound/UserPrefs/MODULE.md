# UserPrefs

## Purpose

A typed, schema-versioned player-preferences store built with a fluent builder. Keys are strongly
typed `PrefKey<T>`; each value is routed to a backend (Unity `PlayerPrefs` for primitives, an atomic
JSON file for complex types); reads/writes are change-observable; and a migration chain upgrades an
older on-disk schema to the current one.

## Assemblies

- `PFound.UserPrefs` (runtime) — the store, builder, backends, migration, lifecycle. `autoReferenced:
  true`. Zero `PFound.*` references (Submodule Independence).
- `PFound.UserPrefs.Editor` — inspector window, menu items, GameSpecific asset provider.
- `PFound.UserPrefs.Tests.EditAndPlayModes` — NUnit Edit+Play suite.

Namespaces: `PFound.UserPrefs`, `PFound.UserPrefs.Editor`.

## Dependencies

| Asmdef | References | Reason |
|---|---|---|
| `PFound.UserPrefs` (runtime) | _(none)_ | Submodule Independence. Uses only `UnityEngine.PlayerPrefs`, `UnityEngine.JsonUtility`, `Application.persistentDataPath`, `OnApplicationPause`/`OnApplicationQuit`. |
| `PFound.UserPrefs.Editor` | `PFound.UserPrefs`, `PFound.Utilities.EditorHelpers` | Editor-only. The `EditorHelpers` reference is the framework convention for `IGameSpecificAssetProvider`. |
| `PFound.UserPrefs.Tests.EditAndPlayModes` | `PFound.UserPrefs`, NUnit, TestRunner | Tests. |

No third-party precompiled refs, no scripting defines.

## Key Types

`PFound.UserPrefs` namespace:

- `PrefKey<T>` — strongly-typed handle: string key + default + storage hint + encryption flag.
- `IPrefsStore` / `PrefsStore` — the store surface + implementation.
- `PrefsBuilder` — construction-time configuration (composition root).
- `PrefChange<T>` — value-type change payload (`Key`, `OldValue`, `NewValue`, `Kind`), zero-alloc dispatch.
- `PrefStorage` — `Auto` / `PlayerPrefs` / `JsonFile` routing hint.
- `UserPrefsSettings` — project config ScriptableObject.
- Exceptions: `PrefsNotLoadedException`, `PrefsMigrationException`, `PrefsSchemaDowngradeException`,
  `PrefsBackendException` (in `PrefsExceptions.cs`).

`Backends/`: `IPrefsBackend`, `PlayerPrefsBackend`, `JsonFileBackend`, `MemoryBackend`.
`Migration/`: `IPrefsMigration`, `MigrationContext`, `PrefsMigrationRunner`.
`Lifecycle/`: `PrefsLifecycleHook`. Also `IPrefsLogger`, `PrefsLog`.

## Public API

**`IPrefsStore`** — `int SchemaVersion`, `bool IsLoaded`.

- `T Get<T>(PrefKey<T>)` — stored value or `DefaultValue`.
- `void Set<T>(PrefKey<T>, T)` — writes; fires a change only when the value actually differs.
- `void SetDeferred<T>(...)` — reserved for batched writes (currently same as `Set`).
- `void Clear<T>(PrefKey<T>)` — deletes; change reports `OldValue → DefaultValue`.
- `void Flush()` / `Task FlushAsync(CancellationToken = default)` — persist both backends.
- `IDisposable Subscribe<T>(PrefKey<T>, Action<PrefChange<T>>)` — dispose to unsubscribe.
- `void Dispose()` — flushes, detaches the lifecycle hook, drops subscribers.

`PrefChange<T>` carries `Key`, `OldValue`, `NewValue`, `Kind` (`Set` / `Cleared` / `MigratedIn`).
Calling `Get`/`Set`/`Clear`/`Subscribe` before `IsLoaded` throws `PrefsNotLoadedException`; after
`Dispose`, `ObjectDisposedException`.

**`PrefKey<T>`** — `new PrefKey<T>(string key, T defaultValue, PrefStorage storage = Auto, bool
requiresEncryption = false)`. Keys may not start with the reserved `__userprefs.` prefix (throws
`ArgumentException`).

**`PrefsBuilder`** — `Add<T>(PrefKey<T>)`, `SchemaVersion(int ≥ 1)`,
`MigrationFrom(int from, int to /* == from+1 */, Action<MigrationContext>)`,
`WithJsonStorage(string fileName)`, `WithBackend(IPrefsBackend, PrefStorage role)` (custom backend
for the `PlayerPrefs` or `JsonFile` role), `WithSettings(UserPrefsSettings)`,
`WithLogger(IPrefsLogger)`, `IPrefsStore Build()`. `Build()` may be called only once.

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

Decisions a consumer makes:

- **Lifecycle / autosave is automatic.** `Build()` spawns a hidden `DontDestroyOnLoad` MonoBehaviour
  (`PrefsLifecycleHook`, tagged `[UserPrefsLifecycle]`, `HideAndDontSave`) that `Flush()`es on
  `OnApplicationPause(true)` and `OnApplicationQuit`. You place no component yourself; `Dispose()`
  tears it down. Explicit `Flush()` remains available for deterministic save points.
- **Where to build it.** Any bootstrap object that runs once — the store is a plain object, not a
  MonoBehaviour. Keep the single instance and the static `PrefKey<T>` definitions somewhere shared.
- **Settings ScriptableObject (optional).** `UserPrefsSettings` (Create ▸ `GameSpecific/UserPrefsSettings`)
  holds `JsonFileName`, `VerboseLogging`, `EditorResetClearsPlayerPrefs`. Pass it via
  `WithSettings(...)`; when omitted the builder falls back to `"userprefs.json"` + non-verbose
  defaults. In the editor the `UserPrefsAssetProvider` (an `IGameSpecificAssetProvider`)
  auto-provisions the asset at `Assets/GameSpecific/UserPrefs/UserPrefsSettings.asset` via the
  GameSpecific guard.
- **No DI registration required** — the builder is the composition root; hand the returned
  `IPrefsStore` to consumers however you prefer. Reference the `PFound.UserPrefs` assembly from a
  consumer asmdef.

## File Structure

```
UserPrefs/
  Runtime/
    PrefKey.cs, PrefChange.cs, PrefStorage.cs
    IPrefsStore.cs, PrefsStore.cs, PrefsBuilder.cs
    UserPrefsSettings.cs, IPrefsLogger.cs, PrefsLog.cs, PrefsExceptions.cs
    Backends/     IPrefsBackend, PlayerPrefsBackend, JsonFileBackend, MemoryBackend
    Migration/    IPrefsMigration, MigrationContext, PrefsMigrationRunner
    Lifecycle/    PrefsLifecycleHook
    PFound.UserPrefs.asmdef
  Editor/
    PrefsInspectorWindow.cs, PrefsMenuItems.cs, UserPrefsAssetProvider.cs
    PFound.UserPrefs.Editor.asmdef
  Tests/EditAndPlayModes/   PrefKey/PlayerPrefsBackend/JsonFileBackend/PrefsStore/ChangeEvent/
                            Migration/EdgeCase/Allocation tests
```

## Downstream Dependents

- `PFound.GuidedOnboardingFlow` — references `PFound.UserPrefs` to persist onboarding progress.

Otherwise consumed directly by game bootstrap code.

## Threading

**Main-thread only.** All `IPrefsStore` methods must be called from the Unity main thread; the store
is not thread-safe. `FlushAsync` performs the file write on a worker thread but the call site itself
must be main-thread; the rest of the API is fully synchronous.

## Storage Layout

- **PlayerPrefs (primitives)** — `bool, int, long, float, double, string, enum`, stored under each
  `PrefKey.Key` directly. `long` and `double` use string-encoded representations (PlayerPrefs lacks
  native long/double). Zero-allocation reads.
- **JSON file (complex)** — single file at `<persistentDataPath>/userprefs.json` (configurable via
  `PrefsBuilder.WithJsonStorage(fileName)` or `UserPrefsSettings.JsonFileName`). Atomic writes via
  `*.tmp + rename`; corrupted-file load falls back to defaults with a warning.

`PrefStorage.Auto` routes by type. Override per-key with `PrefStorage.PlayerPrefs` or
`PrefStorage.JsonFile`; forcing a complex type into PlayerPrefs throws `PrefsBackendException` on Set.

### Reserved key namespace

The prefix `__userprefs.` is reserved for internal bookkeeping; a user `PrefKey<T>` with that prefix
throws `ArgumentException`. Currently reserved: `__userprefs.version` (stored schema version, int).

## Migrations

Register one step per version bump. Each `MigrationContext` exposes `PrimitiveBackend`
(`PlayerPrefs`-routed data) and `JsonData` (`IDictionary<string, object>` for JSON-routed data) plus
`TryGetJsonKey<T>`, `SetJsonKey`, `RemoveJsonKey`. Steps must form a continuous chain up to
`SchemaVersion`; a gap or a thrown step raises `PrefsMigrationException` and leaves the stored version
untouched. A stored version newer than current raises `PrefsSchemaDowngradeException`.

## Extension points

- **`IPrefsBackend`** — custom storage (cloud sync, encryption, alternate location); register via
  `WithBackend(backend, role)`.
- **`IPrefsMigration` + `MigrationContext`** — schema migrations.
- **`IPrefsLogger`** — custom logging sink via `WithLogger(...)`.
- **`PrefKey<T>.RequiresEncryption`** — flag recorded but not enforced in v1 (see Limitations).

## Scope vs. other modules

| If you want… | Use |
|---|---|
| In-memory typed entries that change every frame | `PFound.GameDataStore` |
| Persistent user preferences (settings, profile, progress) | **`PFound.UserPrefs`** |
| Generic LRU/disk caching of remote resources | `PFound.RemoteResourceCache` |
| Bundled asset distribution | `PFound.AssetSystem` |

UserPrefs and GameDataStore are intentionally independent (zero cross-dependency).

## Editor Tooling

| Menu | Action |
|---|---|
| `Window → PFound → UserPrefs Inspector` | Open inspector window |
| `Tools → PFound → UserPrefs → Open Inspector` | Same |
| `Tools → PFound → UserPrefs → Reveal JSON File` | Reveal `<persistentDataPath>/userprefs.json` in the OS file browser |
| `Tools → PFound → UserPrefs → Clear All Stored Data` | Delete JSON + `PlayerPrefs.DeleteAll()` (confirmation) |

The inspector window is informational and houses the storage-file actions. Per-key Reset/Clear UI is
intentionally omitted in v1 (no stable mechanism to discover the active `IPrefsStore` from the editor
without coupling to a specific DI container).

## Limitations / Known Gaps

These expose extension hooks but ship without a built-in implementation in v1:

- **Cloud synchronisation** — implement `IPrefsBackend` with write-through to your remote KV store.
- **Encryption** — `PrefKey<T>.RequiresEncryption` is recorded; v1 logs a warning at `Build()` and
  stores unencrypted. A future `WithEncryptionProvider(...)` will route encrypted-flagged keys.
- **Multi-user / per-account scoping** — pluggable via `IPrefsBackend` (e.g. prefix keys with the
  active account ID).
- **MVVM / bindings integration** — only the raw change-event API ships.
- **Async batched writes with concurrent guarantees** — only main-thread async flush is provided;
  `SetDeferred` is forward-compatible with a batched-write backend.

## Testing

`Tests/EditAndPlayModes/` — NUnit Edit+Play suites: `PrefKeyTests`, `PlayerPrefsBackendTests`,
`JsonFileBackendTests`, `PrefsStoreTests`, `ChangeEventTests`, `MigrationTests`, `EdgeCaseTests`,
`AllocationTests` (verifies primitive `Get<T>` is zero-alloc). `MemoryBackend` isolates the pure
logic from disk. Run via Unity Test Runner (Window → General → Test Runner).

## Version history

See [CHANGELOG.md](CHANGELOG.md).
