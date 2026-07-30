# HubApp.Editor.Providers

`IGameSpecificAssetProvider` implementations that auto-create default assets at `Assets/GameSpecific/HubApp/` (via `Utilities.EditorHelpers.GameSpecificAssetGuard`):

- Default `AudioMixer` asset with Master/Music/SFX/Voice/UI buses + voice ducking snapshot
- Empty `ProductCatalog.asset` for IAP
- Default `AnalyticsConfig.asset`

See consuming project's `Assets/GameSpecific/` convention for placement rules.
