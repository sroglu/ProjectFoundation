# EditorHelpers
Editor-time asset provisioning: auto-create missing GameSpecific `ScriptableObject` assets and add an inspector Import button to importable assets.

**Key classes:** `GameSpecificAssetGuard`, `IGameSpecificAssetProvider`, `GameSpecificAssetRegistration`, `ImportableAsset`, `ImportableAssetEditor`
**Assembly:** `PFound.Utilities.EditorHelpers`
**Tier:** editor
**Depends on:** none

## Current API
- `IGameSpecificAssetProvider` — implement to declare `GameSpecificAssetRegistration.For<T>(assetPath, resourcesName, factory)` entries.
- `GameSpecificAssetGuard` — `[InitializeOnLoad]`; discovers all providers on editor load and creates any missing assets. Also invokable via `GameSpecificAssetGuard.EnsureRegisteredAssets()`.
- `ImportableAsset` — `ScriptableObject` base; subclass to get an inspector Import button (via `ImportableAssetEditor`).

## Limitations
- Editor-only (`includePlatforms: [Editor]`); no runtime footprint.
