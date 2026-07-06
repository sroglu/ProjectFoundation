# UserPrefs Module

## Overview
Persistent, change-notifiable user preferences store for the PFound
framework. DI-injectable (no singleton). Hybrid storage: primitives via Unity's
`PlayerPrefs` (zero-allocation fast path), complex types via a single JSON file
under `Application.persistentDataPath` (flexible path).

**Assembly:** `PFound.UserPrefs` (runtime), `PFound.UserPrefs.Editor` (editor)
**Layer:** Foundation
**Namespace:** `PFound.UserPrefs` (and `PFound.UserPrefs.Editor`)

## Status
v1.0.0 — implemented per spec
[`specs/010-user-prefs`](../../specs/010-user-prefs/spec.md).

## Quick Use

```csharp
public static class GamePrefs {
    public static readonly PrefKey<float> MasterVolume = new("audio.master", 1f);
    public static readonly PrefKey<int>   Level        = new("progress.level", 1);
}

// Register once
serviceRegistry.Register<IPrefsStore>(_ => new PrefsBuilder()
    .Add(GamePrefs.MasterVolume)
    .Add(GamePrefs.Level)
    .SchemaVersion(1)
    .Build());

// Use anywhere
float vol = store.Get(GamePrefs.MasterVolume);
store.Set(GamePrefs.MasterVolume, 0.8f);

// React
using var sub = store.Subscribe(GamePrefs.MasterVolume, c => Apply(c.NewValue));
```

See [`specs/010-user-prefs/quickstart.md`](../../specs/010-user-prefs/quickstart.md)
for the full integration walkthrough.

## Public API Surface

| Type | Purpose |
|---|---|
| `PrefKey<T>` | Strongly-typed handle (string key + default + storage hint). |
| `IPrefsStore` | DI-injected store. `Get/Set/Clear/Subscribe/Flush/FlushAsync`. |
| `PrefsBuilder` | Construction-time configuration (Add keys, SchemaVersion, Migrations, backends). |
| `PrefChange<T>` | Event payload (Key, OldValue, NewValue, Kind). Value type, zero-alloc dispatch. |
| `PrefStorage` | `Auto / PlayerPrefs / JsonFile` routing hint. |
| `IPrefsBackend` | Extension hook for custom storage. |
| `IPrefsMigration` + `MigrationContext` | Extension hook for schema migrations. |
| `IPrefsLogger` | Extension hook for custom logging sink. |
| `UserPrefsSettings` | Project-level config SO (auto-created at `Assets/GameSpecific/UserPrefs/`). |

## Dependencies

| Asmdef | References | Reason |
|---|---|---|
| `PFound.UserPrefs` (runtime) | _(none)_ | Constitution Principle I — Submodule Independence. |
| `PFound.UserPrefs.Editor` | `PFound.UserPrefs`, `PFound.Utilities.EditorHelpers` | Editor-only. The `EditorHelpers` reference is a **justified exception** — it's the framework convention for `IGameSpecificAssetProvider` (Principle III). Documented in spec 010 plan.md → Constitution Check. |
| `PFound.UserPrefs.Tests.EditAndPlayModes` | `PFound.UserPrefs`, NUnit, TestRunner | Tests. |

**Third-party precompiled refs (runtime):** none. Uses only `UnityEngine.PlayerPrefs`,
`UnityEngine.JsonUtility`, `Application.persistentDataPath`, and `OnApplicationPause`/`OnApplicationQuit`.

## Reserved Key Namespace

The prefix `__userprefs.` is reserved for internal bookkeeping. User code that
constructs a `PrefKey<T>` with that prefix throws `ArgumentException`. Currently
reserved keys:

| Key | Purpose |
|---|---|
| `__userprefs.version` | Stored schema version (int). |

Future internal keys may be added under this prefix; existing user keys will
not be affected.

## Threading

**Main-thread only.** All `IPrefsStore` methods must be called from the Unity
main thread. The store is not thread-safe; calling from background threads has
undefined behaviour (Unity API exceptions in practice). This is the documented
framework convention (Constitution Principle II — DI exception clause).

`FlushAsync` performs the file write on a worker thread but the call site itself
must be main-thread; the rest of the API is fully synchronous.

## Lifecycle

A hidden `MonoBehaviour` (`[UserPrefsLifecycle]`, `DontDestroyOnLoad`) is auto-spawned
during `PrefsBuilder.Build()`. It hooks `OnApplicationPause(true)` and
`OnApplicationQuit` to flush pending writes. Disposing the store destroys the
GameObject.

For deterministic durability outside lifecycle events, call `store.Flush()` after
critical writes.

## Storage Layout

- **PlayerPrefs (primitives)** — `bool, int, long, float, double, string, enum`.
  Stored under each `PrefKey.Key` directly. `long` and `double` use string-encoded
  representations (PlayerPrefs lacks native long/double).
- **JSON file (complex)** — single file at `<persistentDataPath>/userprefs.json`
  (configurable via `PrefsBuilder.WithJsonStorage(fileName)` or
  `UserPrefsSettings.JsonFileName`). Atomic writes via `*.tmp + rename`.

`PrefStorage.Auto` routes by type. Override per-key with `PrefStorage.PlayerPrefs`
or `PrefStorage.JsonFile` if you want a primitive in the JSON file or vice versa
(complex types in PlayerPrefs are unsupported and throw `PrefsBackendException`
on Set).

## Scope vs. Other Modules

| If you want… | Use |
|---|---|
| In-memory typed entries that change every frame | `PFound.GameDataStore` |
| Persistent user preferences (settings, profile, progress) | **`PFound.UserPrefs`** (this module) |
| Generic LRU/disk caching of remote resources | `PFound.RemoteResourceCache` |
| Bundled asset distribution | `PFound.AssetSystem` |

UserPrefs and GameDataStore are intentionally independent (zero cross-dependency).

## Known Gaps / Deferred (v1)

These features expose extension hooks but ship without a built-in implementation
in v1:

- **Cloud synchronisation** — implement `IPrefsBackend` with write-through to
  your remote KV store.
- **Encryption** — `PrefKey<T>.RequiresEncryption` flag is recorded; v1 logs a
  warning at `Build()` and stores unencrypted. A future
  `PrefsBuilder.WithEncryptionProvider(...)` API will route encrypted-flagged
  keys through a provider.
- **Multi-user / per-account scoping** — pluggable via `IPrefsBackend` (e.g.,
  prefix all keys with the active account ID).
- **MVVM / Bindings integration** — only the raw change-event API ships.
  Integration with the framework's binding system is a separate future feature.
- **Async batched writes with concurrent guarantees** — only main-thread async
  flush is provided; the API surface (`SetDeferred`) is forward-compatible with
  a batched-write backend.

## Testing

Tests live at `Tests/EditAndPlayModes/`. Coverage:

- `PrefKeyTests` — equality, validation, reserved-prefix rejection
- `PlayerPrefsBackendTests` — round-trip per primitive type
- `JsonFileBackendTests` — complex object round-trip, file write, corrupted-file fallback
- `PrefsStoreTests` — get/set/clear, defaults, builder constraints, disposal
- `ChangeEventTests` — subscribe/unsubscribe, dedup, multi-subscriber, Cleared event
- `MigrationTests` — version detection, ordered execution, downgrade rejection, exception aborts
- `EdgeCaseTests` — corruption recovery, schema add/remove, lifecycle re-construct
- `AllocationTests` — verifies primitive `Get<T>` is zero-alloc

Run via Unity Test Runner (Window → General → Test Runner).

## Editor Tooling

| Menu | Action |
|---|---|
| `Window → PFound → UserPrefs Inspector` | Open inspector window |
| `Tools → PFound → UserPrefs → Open Inspector` | Same |
| `Tools → PFound → UserPrefs → Reveal JSON File` | Reveal `<persistentDataPath>/userprefs.json` in OS file browser |
| `Tools → PFound → UserPrefs → Clear All Stored Data` | Delete JSON + `PlayerPrefs.DeleteAll()` (confirmation) |

The inspector window is informational + houses the storage-file actions.
Per-key Reset/Clear UI is intentionally omitted in v1 (no stable mechanism to
discover the active `IPrefsStore` from the editor without coupling to a
specific DI container).
