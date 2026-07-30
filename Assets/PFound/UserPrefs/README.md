# PFound.UserPrefs

A typed, schema-versioned player-preferences store: strongly-typed `PrefKey<T>`, automatic backend
routing (Unity `PlayerPrefs` for primitives, atomic JSON file for complex types), change
subscriptions, and a migration chain. Built once via a fluent `PrefsBuilder`; autosaves via a hidden
`DontDestroyOnLoad` lifecycle hook.

## Quick reference

```csharp
static readonly PrefKey<float> Volume = new PrefKey<float>("audio.volume", 0.8f);

IPrefsStore store = new PrefsBuilder()
    .Add(Volume)
    .SchemaVersion(1)
    .Build();                                 // store.IsLoaded == true here

float v = store.Get(Volume);
store.Set(Volume, 1.0f);                      // fires PrefChange<float> only if changed
using var sub = store.Subscribe(Volume, c => AudioMixer.Set(c.NewValue));
```

| Type | Purpose |
|---|---|
| `PrefKey<T>` | Typed handle (key + default + storage hint + encryption flag). |
| `IPrefsStore` | `Get/Set/Clear/Subscribe/Flush/FlushAsync`. |
| `PrefsBuilder` | Composition root — add keys, schema version, migrations, backends. |
| `PrefChange<T>` | Zero-alloc change payload (`Key`, `OldValue`, `NewValue`, `Kind`). |
| `PrefStorage` | `Auto / PlayerPrefs / JsonFile` routing hint. |

## Dependencies

None (runtime). Editor asmdef references `PFound.Utilities.EditorHelpers` for the GameSpecific asset provider.

## Docs

- Deep reference: [MODULE.md](MODULE.md) — API, wiring, storage layout, migrations, lifecycle.
- Version history: [CHANGELOG.md](CHANGELOG.md).
