# PFound.HubApp

Hub-app shell services for a multi-mini-game front-end: save, profiles, badges, stickers, photo
album, audio, parent-gate, analytics, plus a `MiniGameHost` that loads/scopes/tears down one
mini-game at a time. Every service is a plain C# class with constructor injection — no container,
no singleton, no host MonoBehaviour; the consuming app owns construction and lifetime.

## Quick reference

```csharp
var signals = new SignalTracker();                       // PFound.Signaling — shared bus

var save = new SaveService(SaveService.DefaultSaveRoot); // persistentDataPath/save.json
save.Load();                                             // MUST call before any other service

var profile = new ProfileService(save, signals);
var badges  = new BadgeService(save, signals);
// ...stickers, photoAlbum, audio, parentGate, analytics — see MODULE.md

var host = new MiniGameHost(new PrefabMiniGameLoader(), save, profile, badges, stickers, photoAlbum, signals);
// keep host + services alive for the whole app; flush save yourself on pause/settings-close.
```

`ISaveService.Schema` is **null until `Load()`** — a deliberate fail-fast. Load save first.

## Dependencies

`PFound.Signaling`, `PFound.Utilities.FileSystemTools`, `PFound.ContentDelivery`, `Unity.Collections`, UniTask.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, full public API, wiring, mini-game lifecycle, file structure.
</content>
