# GameDataStore

## Purpose
Two-layer data system: **Entries** defines a union-style value type (`Entry`) that can hold many primitive and custom types in a single struct; **DataStore** provides runtime data persistence and a database registry pattern.

## Assemblies

| Assembly | Path | Depends On |
|---|---|---|
| `PFound.GameDataStore.Entries` | `DataEntry/PFound.GameDataStore.Entries.asmdef` | Unity.Mathematics, ZString |
| `PFound.GameDataStore.Entries.Editor` | `DataEntry/Editor/PFound.GameDataStore.Entries.Editor.asmdef` | GameDataStore.Entries (Editor only) |
| `PFound.GameDataStore.Storage` | `DataStorage/PFound.GameDataStore.Storage.asmdef` | GameDataStore.Entries, Utilities.DataType, Utilities, ZString, Utilities.DataTypeTools, Unity.Mathematics |

## Key Classes

### Entries (`PFound.GameDataStore.Entries`)
- **`Entry`** — `[StructLayout.Explicit]` union struct holding int, float, bool, string, int2/int3, float2/float3, double, char, Entity, EntityLevel, EntityType, WeekDay. Implements `IEquatable`, `IComparable`, `IConvertible`
- **`EntryType`** — Enum of all supported Entry value types
- **`EntryTypeInfo`** / **`EntryComponentInfo`** — Metadata records describing an EntryType's fields
- **`ProcessedKey`** — String-based key for DataStore lookups
- **`GenericGameData`** — Pairs a `ProcessedKey` with an `Entry`; used for data injection into bindings
- **`EntryManager`** — Factory/converter for Entry values
- **`ITypeId`** / **`TypeIdTable`** — Type identity abstraction
- **`DataDefinition`**, **`DataDefinitionConfig`**, **`DataDefinitionList`**, **`DataDefinitionMap`** — ScriptableObject-based type definition configuration
- **Custom types**: `Entity`, `EntityType`, `EntityLevel`, `WeekDay`

### DataStore (`PFound.GameDataStore.Storage`)
- **`DataStoreManager`** — Process singleton (`DataStoreManager.Instance`) holding the registered `IDataStoreDatabase` instances. Methods: `CreateDatabase<T>(T type)`, `RegisterDatabase(IDataStoreDatabase)`, `GetDatabase(Type)`, `Destroy(IDataStoreDatabase)`, `Dispose()` (disposes every registered database)
- **`IDataStoreDatabase`** / **`IDataStoreDatabase<T>`** — Database interfaces (`IDataStoreDatabase : IDisposable`); the generic one carries the static `Instance` / `Initialize()` / `InitializeWithInstance(T)` / `Destroy()` plumbing
- **`DataStoreClass<T>`** / **`DataStoreRecord<T>`** — Abstract base types for a strongly-typed singleton database. Subclass as `class X : DataStoreClass<X>` (or the `record` variant); exposes static `X.Instance`, `X.Initialize()`, `X.InitializeWithInstance(x)`, `X.IsInitialized`, and instance `Dispose()`
- **`LocalDataStore`** — Independent keyed `Entry` store (NOT a database subclass, NOT a singleton — you own the instance). Created via `LocalDataStore.Create(bool addConstants)`; `Set(ProcessedKey, Entry, bool markAsReadOnly, ReadOnlyWriteOption, UnityEngine.Object source)`, `TryGetEntry(ProcessedKey, out Entry)`, `Exists(ProcessedKey)`, `GetEntryEnsured(ProcessedKey)`, `DeleteIfExists(...)`, `DeleteAllStartsWith(...)`, `Clone()`
- **DataStore attributes** (`DataStoreAttributes.cs`) — `[DataStoreConfig]` (class/struct; fields `Alias` and `Initialization` of `DataStoreDatabaseInitializationMethod` = `Default` / `InitializeAtStart` / `ManualRegistration`), plus `[DataStoreSetup]`, `[DataStoreElement]`, `[NotDataStoreElement]`, `[RequestParameter]`, `[ExternallyExecutable]`

## Public API

```csharp
// Entry — create and compare typed values
Entry intEntry = new Entry(42);
Entry strEntry = new Entry("hello");
bool same = intEntry == new Entry(42); // true

// ProcessedKey — lightweight string key (implicit from string)
ProcessedKey key = "player/health";

// DataStore — strongly-typed singleton database
[DataStoreConfig(Initialization = DataStoreDatabaseInitializationMethod.InitializeAtStart)]
public class PlayerDatabase : DataStoreClass<PlayerDatabase>
{
    public int    Health { get; set; }
    public string Name   { get; set; }
}

PlayerDatabase.Initialize();            // registers the singleton with DataStoreManager
PlayerDatabase.Instance.Health = 100;
int hp = PlayerDatabase.Instance.Health;
DataStoreManager.Instance.Dispose();    // disposes every registered database

// LocalDataStore — keyed Entry store (not a singleton; you own the instance)
var store = LocalDataStore.Create(addConstants: true);
store.Set(key, new Entry(100), markAsReadOnly: false,
          ReadOnlyWriteOption.FailIfReadOnly, source: null);
if (store.TryGetEntry(key, out var e))
    int health = e.ValueInt; // read the accessor matching the written variant
```

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
  DataStoreAttributes.cs
  DataStoreManager.cs
  DataStoreDatabase.cs       # IDataStoreDatabase, DataStoreClass<T>, DataStoreRecord<T>
  LocalDataStore.cs
Test/
  PFound.GameDataStore.Test.asmdef
  DataStoreTest.cs
  BookDataStore.cs
Tests/
  PFound.GameDataStore.Tests.asmdef
  EntryComparisonTests.cs
```

## Downstream Dependents

`PFound.Bindings`, `PFound.Presenter`, `PFound.ScreenManagement` (PFound reusable-layer modules) consume `Entries` and/or `Storage` via GUID asmdef refs — those refs survived the rename so consumer code only needs the namespace update (`using PFound.DataManagement.*` → `using PFound.GameDataStore.*`).

## Naming History

Originally named `DataManagement` (PFound.DataManagement.Entries / .DataStore). Renamed to `GameDataStore` in April 2026 because "DataManagement" was too generic and overlapped semantically with `AssetManagement`. The DataStore asmdef is now `Storage` (not `GameDataStore.DataStore`) to avoid the awkward name doubling.

## Scope Clarification

GameDataStore is a **runtime in-game data modeling layer** — NOT an app-level save/persistence system. Understand the distinction before picking where to write things:

- **Use GameDataStore when:** modeling typed game data at runtime (player stats as `Entry` values, item definitions as `DataDefinition` SOs, databases indexed by `ProcessedKey`), or when you need a typed Entry-union for bindings/UI.
- **Do NOT use GameDataStore when:** writing JSON save files with schema versioning, atomic file writes, migrations, or multi-namespace profile data. These concerns belong to an app-level save layer (e.g., a hub-app `Save` module built on `Utilities/FileSystemTools` atomic write).

The two can coexist: an app's save layer serializes a `GameDataStore` database snapshot into its own JSON schema on save, and rehydrates the database on load. GameDataStore does not own the file format, the atomic write, or the migration pipeline.

## Adding a New EntryType

When adding a new value type to the `Entry` struct, update these locations:

1. **`EntryType.cs`** — Add enum value
2. **`Entry.cs`** — Add `[FieldOffset(4)]` field, constructor, implicit operator, accessor properties (Value/TryGet/Get), and cases in:
   - `Equals()`, `GetHashCode()`, `CompareTo()`, `TryParse()`, `ToString()` (both overloads), `GetTypePrefix()`, `ToObject()`, `FromObject()`, `GetTypeCode()`
3. **`EntryType.cs` → `EntryTypeTools`** — Add to `EntryTypeInfoList`, `EntryTypeToSystemTypeMap`, `SystemTypeToEntryTypeMap`, `TryParse()`, `Parse()`, `ToString()`
4. **`EntryComparisonTests.cs`** — Add to `TestPairs`, `ParseTestCases`, and `CreateTestEntry()` switch

The **extensibility guard tests** (`AllEntryTypes_CoveredBy*`) iterate all `EntryType` enum values and call each method. If you add a new type but miss any method, these tests fail with a clear message indicating the missing case.

## Limitations / Known Gaps

- **No JSON persistence or atomic-write built in.** `LocalDataStore` holds keyed `Entry` state in memory, but the file I/O semantics (atomic write, fsync, .bak fallback, schema version migration) are NOT part of this module. To persist a typed database, serialize a snapshot yourself and rehydrate it via `DataStoreClass<T>.InitializeWithInstance(...)`. Build the file I/O at the app-save layer.
- **`Entry` uses `[FieldOffset]` explicit overlay.** Reference fields (e.g., `string`) overlap value fields (e.g., `int`, `float`). Reading an overlapping slot after writing a different variant yields undefined results. Always read the variant that matches what was last written — `EntryType` tracks this.
- **`DataStoreManager` is a process singleton.** Initialize once (typically via a SubSystem) before any consumer touches `DataStoreManager.Instance`. There is no built-in per-profile scoping — if you need isolation (e.g., multi-user on one device), wrap the manager per profile yourself or namespace keys in `ProcessedKey`.
- **`ProcessedKey` is a flat string.** No hierarchical namespace built in — keys are opaque strings. Convention is slash-delimited (`player/health`, `inventory/potion_small`) but no API enforces it.
- **No built-in change notifications.** Mutating a database's Entry does not emit a signal or event. If downstream UI needs to react to data changes, wire Signaling (`SignalTracker.Emit<DataChangedSignal>()`) at the mutation call site, or wrap the database.
- **GUID asmdef refs survive the rename.** Consumers (`Bindings`, `Presenter`, `ScreenManagement`) reference GameDataStore via asmdef GUIDs, so the DataManagement → GameDataStore rename does not break references. Only `using` directives in source code need updating.

## Notes
- `Entry` uses `[FieldOffset]` overlapping — be aware when holding reference types (string) alongside value types
- `DataStoreManager` is a **singleton** — initialize in a SubSystem before consumers
- `ProcessedKey` is a simple string wrapper used as the flat key type
