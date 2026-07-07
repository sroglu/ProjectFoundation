# GameDataStore

## Purpose
Two-layer runtime in-game data-modeling layer: **Entries** defines a union-style value type (`Entry`) that can hold many primitive and custom types in a single struct; **Storage** provides strongly-typed singleton databases plus a keyed in-memory store and a database registry. This is a runtime data model, NOT an app-level save/persistence system (no JSON schema, atomic write, or migration here).

## Assemblies

| Assembly | Path | Depends On |
|---|---|---|
| `PFound.GameDataStore.Entries` | `DataEntry/PFound.GameDataStore.Entries.asmdef` | Unity.Mathematics, ZString |
| `PFound.GameDataStore.Entries.Editor` | `DataEntry/Editor/PFound.GameDataStore.Entries.Editor.asmdef` | GameDataStore.Entries (Editor only) |
| `PFound.GameDataStore.Storage` | `DataStorage/PFound.GameDataStore.Storage.asmdef` | GameDataStore.Entries, Utilities.DataType, Utilities, Utilities.DataTypeTools, ZString, Unity.Mathematics |
| `PFound.GameDataStore.Test` | `Test/PFound.GameDataStore.Test.asmdef` | Storage, Entries (canonical usage example) |
| `PFound.GameDataStore.Tests` | `Tests/PFound.GameDataStore.Tests.asmdef` | Entries (extensibility-guard tests) |

## Dependencies

- **PFound modules:** none — GameDataStore is a leaf data layer.
- **Utilities (PFound):** `Storage` references `Utilities`, `Utilities.DataType`, `Utilities.DataTypeTools`.
- **Third-party:** Unity.Mathematics (`int2`/`int3`/`float2`/`float3` in `Entry`), ZString.
- **Scripting defines:** none.

## Key Types

### Entries (`PFound.GameDataStore.Entries`)
- **`Entry`** — `[StructLayout(Explicit)]` union struct holding one of int/uint/long/short/byte, float/float2/float3, double, bool, char, string, int2/int3, and the custom types `Entity` / `EntityLevel` / `EntityType` / `WeekDay`. A `Type` discriminant (an `EntryType`) records which variant is live. Implements `IEquatable`, `IComparable`, `IConvertible`.
- **`EntryType`** — enum of all supported `Entry` value types; `EntryTypeTools` carries the type maps and parse/format helpers.
- **`EntryTypeInfo`** / **`EntryComponentInfo`** — metadata records describing an `EntryType`'s fields.
- **`ProcessedKey`** — lightweight string-key struct for store lookups (implicit from `string`).
- **`GenericGameData`** — pairs a `ProcessedKey` with an `Entry`; used for data injection into bindings.
- **`EntryManager`** — factory/converter for `Entry` values.
- **`ITypeId`** / **`TypeIdTable`** — type identity abstraction.
- **`DataDefinition`**, **`DataDefinitionConfig`**, **`DataDefinitionList`**, **`DataDefinitionMap`** — ScriptableObject-based type-definition configuration.
- **Custom value types:** `Entity`, `EntityType`, `EntityLevel`, `WeekDay`.

### Storage (`PFound.GameDataStore.Storage`)
- **`DataStoreClass<T>`** / **`DataStoreRecord<T>`** — abstract base types for a strongly-typed singleton database. Subclass as `class X : DataStoreClass<X>` (or the `record` variant); each exposes static `X.Instance`, `X.Initialize()`, `X.InitializeWithInstance(x)`, `X.IsInitialized`, and instance `Dispose()`. (`where T : IDataStoreDatabase<T>, new()`.)
- **`IDataStoreDatabase`** / **`IDataStoreDatabase<T>`** — database interfaces (`IDataStoreDatabase : IDisposable`); the generic one carries the static `Instance` / `Initialize()` / `InitializeWithInstance(T)` / `Destroy()` plumbing that the base classes forward to.
- **`DataStoreManager`** — process singleton (`DataStoreManager.Instance`) holding the registered `IDataStoreDatabase` instances.
- **`LocalDataStore`** — independent keyed `Entry` store (NOT a database subclass, NOT a singleton — you own the instance).
- **DataStore attributes** (`DataStoreAttributes.cs`) — `[DataStoreConfig]` plus `[DataStoreSetup]`, `[DataStoreElement]`, `[NotDataStoreElement]`, `[RequestParameter]`, `[ExternallyExecutable]`; enum `DataStoreDatabaseInitializationMethod` = `Default` / `InitializeAtStart` / `ManualRegistration`.

## Public API

### Entry values
```csharp
Entry intEntry = new Entry(42);
Entry strEntry = new Entry("hello");
bool same = intEntry == new Entry(42);   // true
int v = intEntry.ValueInt;               // read the accessor matching the written variant
ProcessedKey key = "player/health";      // implicit from string
```

### `DataStoreClass<T>` / `DataStoreRecord<T>` (base classes)
- `static T Instance` — lazily calls `Initialize()` on first access.
- `static void Initialize()` — creates the singleton and registers it with `DataStoreManager`.
- `static void InitializeWithInstance(T instance)` — registers a pre-built instance (e.g. one rehydrated by a save layer).
- `static bool IsInitialized`.
- `void Dispose()`.

### `DataStoreManager`
- `static DataStoreManager Instance`.
- `IDataStoreDatabase CreateDatabase<T>(T type) where T : Type`, `RegisterDatabase(IDataStoreDatabase)`, `GetDatabase(Type)`, `Destroy(IDataStoreDatabase)`, `Dispose()` (disposes every registered database).

### `LocalDataStore`
- `static LocalDataStore Create(bool addConstants)`.
- `Set(ProcessedKey, Entry, bool markAsReadOnly, ReadOnlyWriteOption, UnityEngine.Object source)`.
- `bool TryGetEntry(ProcessedKey, out Entry)`, `bool Exists(ProcessedKey)`, `Entry GetEntryEnsured(ProcessedKey)`.
- `DeleteIfExists(ProcessedKey, ReadOnlyDeleteOption)`, `DeleteAllStartsWith(ProcessedKey, ReadOnlyDeleteOption)`, `LocalDataStore Clone()`.

### Attributes
- `[DataStoreConfig(Alias = ..., Initialization = ...)]` (class/struct; `Initialization` of `DataStoreDatabaseInitializationMethod` = `Default` / `InitializeAtStart` / `ManualRegistration`).
- `[DataStoreSetup]`, `[DataStoreElement]`, `[NotDataStoreElement]`, `[RequestParameter]`, `[ExternallyExecutable]`.

## Setup / wiring

**No scene object, MonoBehaviour host, or DI registration.** Databases are process singletons reached through static members; `DataStoreManager.Instance` is created on first access. The only wiring is: **define a database type, then `Initialize()` it once before consumers touch `.Instance`.**

Define a typed database by subclassing `DataStoreClass<T>` (T = your own type) with plain properties, and tag it with `[DataStoreConfig]`:

```csharp
using PFound.GameDataStore.Storage;

[DataStoreConfig(Initialization = DataStoreDatabaseInitializationMethod.InitializeAtStart)]
public class PlayerDatabase : DataStoreClass<PlayerDatabase>
{
    public int    Health     { get; set; }
    public string Name       { get; set; }
    public float  Experience { get; set; }
}

// Once, at startup (e.g. from your bootstrap flow):
PlayerDatabase.Initialize();          // registers the singleton with DataStoreManager

// Anywhere after:
PlayerDatabase.Instance.Health = 100;
int hp = PlayerDatabase.Instance.Health;

// Teardown:
DataStoreManager.Instance.Dispose();  // disposes every registered database
```

`Instance` also lazily calls `Initialize()` on first access, so an explicit `Initialize()` at a known point controls *when* creation happens, not *whether*. `InitializeWithInstance(existing)` registers a pre-built instance (e.g. one rehydrated by your save layer).

For a keyed `Entry` store instead of a typed database, use `LocalDataStore` directly (it is not a singleton — you own the instance):

```csharp
var store = LocalDataStore.Create(addConstants: true);
store.Set("player/health", new Entry(100), markAsReadOnly: false,
          ReadOnlyWriteOption.FailIfReadOnly, source: null);
if (store.TryGetEntry("player/health", out var e))
    int health = e.ValueInt;          // read the accessor matching the written variant
```

**Authored `DataDefinitionConfig` (optional).** `DataDefinitionConfig` is a ScriptableObject (`Assets > Create > GameConfigs/DataDefinitionConfig`) mapping named definitions to `EntityType` values; call `config.CreateDataDefinitionList()` to materialize the lookup at runtime. It is a supporting authoring asset, not required to use `Entry` or the databases — reference it (inspector field / your own loader) only where you need authored type definitions.

Save/load is out of scope: serialize a database snapshot into your own save format on write and rehydrate on load (e.g. via `InitializeWithInstance`); GameDataStore owns neither the file format nor the migration pipeline.

## File Structure
```
DataEntry/
  PFound.GameDataStore.Entries.asmdef
  EntryManager.cs
  Entry/EntryTypeInfo.cs
  Custom/TypeId/ITypeId.cs
  Custom/TypeId/TypeIdTable.cs
  Custom/TypeId/DataDefinitions/DataDefinition.cs
  Custom/TypeId/DataDefinitions/DataDefinitionConfig.cs
  Custom/TypeId/DataDefinitions/DataDefinitionList.cs
  Custom/TypeId/DataDefinitions/DataDefinitionMap.cs
  Custom/Types/DateTime/WeekDay.cs
  Custom/Types/Entity/Entity.cs
  Custom/Types/Entity/EntityLevel.cs
  Custom/Types/Entity/EntityType.cs
  Editor/
    PFound.GameDataStore.Entries.Editor.asmdef
    EntryPropertyDrawer.cs
DataStorage/
  PFound.GameDataStore.Storage.asmdef
  DataStoreAttributes.cs        # [DataStoreConfig] + sibling attributes, init enum
  DataStoreManager.cs
  DataStoreDatabase.cs          # IDataStoreDatabase, DataStoreClass<T>, DataStoreRecord<T>
  LocalDataStore.cs
Test/
  PFound.GameDataStore.Test.asmdef
  DataStoreTest.cs              # canonical usage example
  BookDataStore.cs             # [DataStoreConfig] class : DataStoreClass<T> example
Tests/
  PFound.GameDataStore.Tests.asmdef
  EntryComparisonTests.cs       # extensibility-guard tests
```

## Downstream Dependents

`PFound.Bindings`, `PFound.Presenter`, `PFound.ScreenManagement` (PFound reusable-layer modules) consume `Entries` and/or `Storage` via GUID asmdef refs — those refs survived the rename, so consumer code only needs the namespace update (`using PFound.DataManagement.*` → `using PFound.GameDataStore.*`).

## Limitations / Known Gaps

- **No JSON persistence or atomic-write built in.** `LocalDataStore` holds keyed `Entry` state in memory, but file I/O semantics (atomic write, fsync, .bak fallback, schema version migration) are NOT part of this module. To persist a typed database, serialize a snapshot yourself and rehydrate it via `DataStoreClass<T>.InitializeWithInstance(...)`. Build the file I/O at the app-save layer.
- **`Entry` uses `[FieldOffset]` explicit overlay.** Reference fields (e.g. `string`) overlap value fields (e.g. `int`, `float`). Reading an overlapping slot after writing a different variant yields undefined results. Always read the variant that matches what was last written — `EntryType` tracks this.
- **`DataStoreManager` is a process singleton.** Initialize once (typically via a SubSystem) before any consumer touches `DataStoreManager.Instance`. There is no built-in per-profile scoping — if you need isolation (e.g. multi-user on one device), wrap the manager per profile yourself or namespace keys in `ProcessedKey`.
- **`ProcessedKey` is a flat string.** No hierarchical namespace built in — keys are opaque strings. Convention is slash-delimited (`player/health`, `inventory/potion_small`) but no API enforces it.
- **No built-in change notifications.** Mutating a database's `Entry` does not emit a signal or event. If downstream UI needs to react to data changes, wire Signaling at the mutation call site, or wrap the database.
- **GUID asmdef refs survive the rename.** Consumers (`Bindings`, `Presenter`, `ScreenManagement`) reference GameDataStore via asmdef GUIDs, so the rename does not break references — only `using` directives in source code need updating.

## Scope Clarification

GameDataStore is a **runtime in-game data modeling layer** — NOT an app-level save/persistence system. Understand the distinction before picking where to write things:

- **Use GameDataStore when:** modeling typed game data at runtime (player stats as `Entry` values, item definitions as `DataDefinition` SOs, databases indexed by `ProcessedKey`), or when you need a typed `Entry`-union for bindings/UI.
- **Do NOT use GameDataStore when:** writing JSON save files with schema versioning, atomic file writes, migrations, or multi-namespace profile data. These concerns belong to an app-level save layer (e.g. a hub-app `Save` module built on `Utilities/FileSystemTools` atomic write).

The two coexist: an app's save layer serializes a `GameDataStore` database snapshot into its own JSON schema on save, and rehydrates the database on load. GameDataStore does not own the file format, the atomic write, or the migration pipeline.

## Adding a New EntryType

When adding a new value type to the `Entry` struct, update these locations:

1. **`EntryType.cs`** — add enum value.
2. **`Entry.cs`** — add `[FieldOffset(4)]` field, constructor, implicit operator, accessor properties (Value/TryGet/Get), and cases in `Equals()`, `GetHashCode()`, `CompareTo()`, `TryParse()`, `ToString()` (both overloads), `GetTypePrefix()`, `ToObject()`, `FromObject()`, `GetTypeCode()`.
3. **`EntryType.cs` → `EntryTypeTools`** — add to `EntryTypeInfoList`, `EntryTypeToSystemTypeMap`, `SystemTypeToEntryTypeMap`, `TryParse()`, `Parse()`, `ToString()`.
4. **`EntryComparisonTests.cs`** — add to `TestPairs`, `ParseTestCases`, and `CreateTestEntry()` switch.

The **extensibility-guard tests** (`AllEntryTypes_CoveredBy*`) iterate all `EntryType` enum values and call each method. If you add a new type but miss any method, these tests fail with a clear message indicating the missing case.

## Naming History

Originally named `DataManagement` (PFound.DataManagement.Entries / .DataStore). Renamed to `GameDataStore` in April 2026 because "DataManagement" was too generic and overlapped semantically with `AssetManagement`. The Storage asmdef is now `Storage` (not `GameDataStore.DataStore`) to avoid the awkward name doubling.
