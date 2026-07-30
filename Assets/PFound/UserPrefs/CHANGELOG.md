# Changelog

All notable changes to the UserPrefs module are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] — 2026-05-03

Initial release. Implements spec
[`010-user-prefs`](../../specs/010-user-prefs/spec.md).

### Added

**Public API**:
- `PrefKey<T>` — typed handle (string key + default + storage hint + encryption flag).
- `IPrefsStore` — DI-injected store with `Get`, `Set`, `Clear`, `Subscribe`,
  `Flush`, `FlushAsync`, `SetDeferred`, `IsLoaded`, `SchemaVersion`, `Dispose`.
- `PrefsBuilder` — `Add`, `SchemaVersion`, `MigrationFrom`, `WithJsonStorage`,
  `WithBackend`, `WithSettings`, `WithLogger`, `Build`.
- `PrefChange<T>` value-type event payload (Key, OldValue, NewValue, Kind).
- `PrefStorage` enum (`Auto / PlayerPrefs / JsonFile`).
- `UserPrefsSettings` ScriptableObject (auto-created at
  `Assets/GameSpecific/UserPrefs/`).

**Extension hooks**:
- `IPrefsBackend` — custom storage (cloud sync, encryption, alternate location).
- `IPrefsMigration` + `MigrationContext` — schema version migrations.
- `IPrefsLogger` — custom logging sink.
- `PrefKey<T>.RequiresEncryption` flag — recorded but not enforced in v1
  (encryption provider hook reserved for v2+).

**Storage backends (built-in)**:
- `PlayerPrefsBackend` — primitive types (`bool, int, long, float, double,
  string, enum`). Zero-allocation reads.
- `JsonFileBackend` — complex types via `UnityEngine.JsonUtility`. Single file
  with atomic write (`*.tmp + rename`), corruption recovery falls back to
  defaults with a warning log.

**Migration**:
- `PrefsMigrationRunner` — linear chain executor (`from N → to N+1`),
  fail-fast on downgrade (`PrefsSchemaDowngradeException`), version recorded
  under reserved key `__userprefs.version`.

**Lifecycle**:
- `PrefsLifecycleHook` — hidden `MonoBehaviour` on `DontDestroyOnLoad`, hooks
  `OnApplicationPause(true)` + `OnApplicationQuit` for sync flush.

**Editor tooling**:
- `PrefsInspectorWindow` — IMGUI window at `Window → PFound → UserPrefs Inspector`.
- `PrefsMenuItems` — `Tools → PFound → UserPrefs → {Open Inspector, Reveal
  JSON File, Clear All Stored Data}`.
- `UserPrefsAssetProvider` — `IGameSpecificAssetProvider` impl that auto-creates
  the settings SO at `Assets/GameSpecific/UserPrefs/`.

**Tests**:
- `PrefKeyTests`, `PlayerPrefsBackendTests`, `JsonFileBackendTests`,
  `PrefsStoreTests`, `ChangeEventTests`, `MigrationTests`, `EdgeCaseTests`,
  `AllocationTests`.

### Constraints

- Main-thread only (Unity API constraint, documented).
- Constitution Principle I (Submodule Independence): runtime asmdef has zero
  `PFound.*` references. Editor asmdef references
  `PFound.Utilities.EditorHelpers` (justified exception per spec 010 plan.md).
- Constitution Principle X (Mobile-first performance): primitive `Get<T>` is
  zero-allocation, verified by `AllocationTests`.

### Deferred to future releases

- Cloud synchronisation (`IPrefsBackend` extension hook ready).
- Encryption provider (`RequiresEncryption` flag ready).
- Multi-user / per-account scoping (`IPrefsBackend` extension hook ready).
- MVVM / two-way binding integration with framework binding module.
- Per-key Reset/Clear in editor inspector (no stable DI-discovery mechanism in v1).
