# PFound.GameDataStore

A runtime in-game data-modeling layer, in two parts: **Entries** — a union-style value type
(`Entry`) that holds any of many primitive and custom types in one struct — and **Storage** —
strongly-typed singleton databases plus a keyed in-memory store. This is a runtime data model, not
an app-level save/persistence system (no JSON schema, atomic write, or migration here).

## Quick reference

Define a typed database by subclassing `DataStoreClass<T>` (T = your own type) and tagging it
`[DataStoreConfig]`; `Initialize()` it once, then reach it through `.Instance`:

```csharp
using PFound.GameDataStore.Storage;

[DataStoreConfig(Initialization = DataStoreDatabaseInitializationMethod.InitializeAtStart)]
public class PlayerDatabase : DataStoreClass<PlayerDatabase>
{
    public int    Health { get; set; }
    public string Name   { get; set; }
}

PlayerDatabase.Initialize();          // registers the singleton with DataStoreManager
PlayerDatabase.Instance.Health = 100; // read/write anywhere after
DataStoreManager.Instance.Dispose();  // disposes every registered database
```

For a keyed `Entry` store instead of a typed database, use `LocalDataStore.Create(...)`.

## Dependencies

Entries: Unity.Mathematics, ZString. Storage: Entries + PFound `Utilities` + Unity.Mathematics, ZString. No PFound-module deps.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, full public API, setup/wiring, `Entry`
`[FieldOffset]` model, adding a new `EntryType`, scope vs. an app save layer, and limitations.
