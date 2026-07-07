# PFound.GameDataStore

A runtime in-game data-modeling layer, in two parts: **Entries** — a union-style value type
(`Entry`) that holds any of many primitive and custom types in one struct — and **Storage** —
strongly-typed singleton databases plus a keyed in-memory store. This is a runtime data model, not
an app-level save/persistence system (no JSON schema, atomic write, or migration here).

## Model

- **`Entry`** — a `[StructLayout(Explicit)]` union struct holding one of int/uint/long/short/byte/
  float(2/3)/double/bool/char/string/int2/int3/`Entity`/`EntityLevel`/`EntityType`/`WeekDay`. A
  `Type` discriminant (an `EntryType`) records which variant is live; read the accessor that matches
  what was written (e.g. `entry.ValueInt`, `entry.ValueString`, `entry.TryGetInt(out var i)`).
  Implements `IEquatable`/`IComparable`/`IConvertible`.
- **`ProcessedKey`** — a lightweight string-key struct; `new ProcessedKey("player/health")` or the
  implicit `ProcessedKey k = "player/health"`. Keys are flat opaque strings (slash convention, not
  enforced).
- **Typed database** — a `DataStoreClass<T>` (or `DataStoreRecord<T>`) subclass with plain
  properties, registered as a process singleton through `DataStoreManager`.
- **`LocalDataStore`** — an independent keyed `Entry` store (get/set by `ProcessedKey`, read-only
  flags), separate from the singleton databases.

## Public API

**Entries (`PFound.GameDataStore.Entries`):** `Entry` (per-type constructors, implicit operators,
`Value*` accessors, `TryGet*`, `IsValid`), `EntryType`, `EntryTypeInfo`, `EntryManager`,
`ProcessedKey`, `GenericGameData`, and custom value types `Entity` / `EntityType` / `EntityLevel` /
`WeekDay`. `DataDefinitionConfig` (ScriptableObject) + `DataDefinition<T>` / `DataDefinitionList` /
`DataDefinitionMap<T>` (plain supporting types).

**Storage (`PFound.GameDataStore.Storage`):**
- `DataStoreClass<T>` / `DataStoreRecord<T>` (base classes) — static `T Instance`,
  `void Initialize()`, `void InitializeWithInstance(T)`, `bool IsInitialized`.
- `DataStoreManager` — `static DataStoreManager Instance`; `CreateDatabase<T>(T type)`,
  `RegisterDatabase(IDataStoreDatabase)`, `GetDatabase(Type)`, `Destroy(IDataStoreDatabase)`,
  `Dispose()` (disposes all).
- `LocalDataStore` — `static LocalDataStore Create(bool addConstants)`;
  `Set(ProcessedKey, Entry, bool markAsReadOnly, ReadOnlyWriteOption, Object source)`,
  `bool TryGetEntry(ProcessedKey, out Entry)`, `bool Exists(ProcessedKey)`,
  `Entry GetEntryEnsured(ProcessedKey)`, `DeleteIfExists(...)`, `DeleteAllStartsWith(...)`.
- Attributes: `[DataStoreConfig(Alias, Initialization)]` (class-level; `Initialization` is
  `Default` / `InitializeAtStart` / `ManualRegistration`), plus `[DataStoreElement]`,
  `[NotDataStoreElement]`, `[RequestParameter]`, `[ExternallyExecutable]`, `[DataStoreSetup]`.

## Setup / wiring

**No scene object, MonoBehaviour host, or DI registration.** Databases are process singletons
reached through static members; `DataStoreManager.Instance` is created on first access. The only
wiring is: **define a database type, then `Initialize()` it once before consumers touch
`.Instance`.**

Define a typed database by subclassing `DataStoreClass<T>` (T = your own type) with plain
properties, and tag it with `[DataStoreConfig]`:

```csharp
using PFound.GameDataStore.Storage;

[DataStoreConfig(Initialization = DataStoreDatabaseInitializationMethod.InitializeAtStart)]
public class PlayerDatabase : DataStoreClass<PlayerDatabase>
{
    public int    Health { get; set; }
    public string Name   { get; set; }
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

`Instance` also lazily calls `Initialize()` on first access, so an explicit `Initialize()` at a
known point is about *when* creation happens, not *whether*. `InitializeWithInstance(existing)` lets
you register a pre-built instance (e.g. one rehydrated by your save layer).

For a keyed `Entry` store instead of a typed database, use `LocalDataStore` directly (it is not a
singleton — you own the instance):

```csharp
var store = LocalDataStore.Create(addConstants: true);
store.Set("player/health", new Entry(100), markAsReadOnly: false,
          ReadOnlyWriteOption.FailIfReadOnly, source: null);
if (store.TryGetEntry("player/health", out var e))
    int health = e.ValueInt;          // read the accessor matching the written variant
```

**Authored `DataDefinitionConfig` (optional).** `DataDefinitionConfig` is a ScriptableObject
(`Assets > Create > GameConfigs/DataDefinitionConfig`) mapping named definitions to `EntityType`
values; call `config.CreateDataDefinitionList()` to materialize the lookup at runtime. It is a
supporting authoring asset, not required to use `Entry` or the databases — reference it (inspector
field / your own loader) only where you need authored type definitions.

Save/load is out of scope: serialize a database snapshot into your own save format on write and
rehydrate on load (e.g. via `InitializeWithInstance`); GameDataStore owns neither the file format
nor the migration pipeline.

## Testing

`EntryComparisonTests` (under `Tests/`) drives extensibility-guard tests that iterate every
`EntryType` and fail with a clear message if a new type misses a method. `Test/DataStoreTest.cs` +
`Test/BookDataStore.cs` are the canonical usage example (define → `Initialize()` → set/read
`.Instance`).

## Layout

- `DataEntry/` — `Entry`, `EntryType`, `EntryManager`, `ProcessedKey`, custom types, and the
  `DataDefinition*` types. Assembly `PFound.GameDataStore.Entries` (Unity.Mathematics, ZString);
  `Editor/` drawer assembly `PFound.GameDataStore.Entries.Editor`.
- `DataStorage/` — `DataStoreManager`, `DataStoreClass`/`DataStoreRecord`, `LocalDataStore`,
  attributes. Assembly `PFound.GameDataStore.Storage`.

Part of the PFound modular Unity foundation.
</content>
