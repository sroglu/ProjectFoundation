# Commerce

> **Module group — LiveOps** (`RemoteGameConfig`, `PlayerDataSync`, `Commerce`; analytics via HubApp's
> `IAnalyticsProvider`). Grouped by purpose. They are separately publishable, but the group is **not
> dependency-free**: `Commerce` uses `RemoteGameConfig` (product catalog), `PlayerDataSync` uses it
> (install-id), and `RemoteGameConfig` builds on `RemoteResourceCache`; optional/gated integrations are
> Firebase, Unity IAP, BestHTTP. See each module's **Dependencies** for exact edges.

## Purpose

Runs the game economy: a remote-config-driven product catalog, a unified **cost** system (pay with
real money, soft/hard currency, by watching a rewarded ad, for free, or a player-chosen "selectable"
option), a **reward** system that grants what a product gives, and in-app purchases that are
**verified server-side before anything is granted**, recorded in an idempotent append-only purchase
ledger, and restorable. The store transaction runs through Unity IAP; Apple/Google process the
payment (no card data ever touches the client). The verification backend is pluggable behind one
interface — Firebase Cloud Function first, self-host endpoint later — and game code never names a
backend.

## Assemblies

| Assembly | Path | Engine refs | Depends on | Notes |
|---|---|---|---|---|
| `PFound.Commerce.Core` | `Core/Runtime/` | none (`noEngineReferences`) | — | Cost/reward/catalog/ledger/idempotency + verifier, ad-play, store, clock seams. Engine-free AND IAP-free; mono/csc-testable. |
| `PFound.Commerce.Core.Tests` | `Core/Tests/` | none | Core | Standalone mono/csc runner (96 tests). |
| `PFound.Commerce` | `Runtime/` | Unity | Core, RemoteGameConfig(.Core) | Catalog read from RGC, resolver wiring, `CommerceService` facade, analytics-emit event, price display, editor test grants. **No IAP, no HubApp.** |
| `PFound.Commerce.UnityIap` | `UnityIap/` | Unity | Core, Runtime, Unity.Purchasing | **`PFOUND_UNITY_IAP`-gated.** The Unity IAP store-transaction wrapper. Excluded until the package is installed + the define set. |
| `PFound.Commerce.Firebase` | `Firebase/` | Unity | Core, Runtime, Firebase SDK | **`PFOUND_FIREBASE`-gated** Cloud Function verifier. |
| `PFound.Commerce.Tests` | `Tests/` | Editor | Runtime, Core, RemoteGameConfig.Core | EditMode tests with stub store/verifier/ad-play. |

Every assembly is `autoReferenced:false`. Core has **no** third-party deps.

## Dependencies

- **RemoteGameConfig** (hard) — the product catalog is a config section (`commerce.catalogJson`), so
  LiveOps can add/price/bundle products without an app update. Commerce owns no catalog fetch.
- **Unity IAP** (`com.unity.purchasing`, `PFOUND_UNITY_IAP`-gated) — store transactions, Runtime-side
  only. Not installed by default.
- **Firebase SDK** (`PFOUND_FIREBASE`-gated) — the first verifier backend.
- **Analytics** — emitted as a plain local event; the game forwards it to its `IAnalyticsProvider`.
  Commerce takes **no** wholesale HubApp dependency.
- **BestHTTP** (`PFOUND_BESTHTTP`) — reserved for the deferred self-host verifier.

## Public API

**Cost (Core)** — `ICost` (`FreeCost`, `CurrencyCost`, `RewardedAdCost`, `RealMoneyCost`,
`SelectableCost`); `ICostResolver` + `CostResolverRegistry` (`CanPay`/`Pay`); built-in resolvers;
`CostPayment`/`CostPaymentStatus`.

**Reward (Core)** — `IReward` (`CurrencyReward`, `InventoryItemReward`, `EntitlementReward`; open
set via `RewardTypes` + game-registered resolvers); `IRewardResolver` + `RewardResolverRegistry`
(`RewardApplied` signal); `RewardSet` (`ApplyThrough`).

**Catalog (Core)** — `ProductDefinition`, `ProductCatalog` (name + reverse SKU index),
`StorePlatform`, `ProductKind`, `ProductCatalogReader.TryParse` (+ custom reward factories),
`JsonValue` (engine-free reader).

**Security core (Core)** — `PurchaseReceipt`, `PurchaseVerification`, `IPurchaseVerifier`,
`PurchaseLedgerEntry` / `IPurchaseLedger` / `InMemoryPurchaseLedger`, `PurchasePipeline`
(`ProcessReceiptAsync`, `ReconcileEntitlements`, `PurchaseGranted`/`PurchaseFailed`/`AnalyticsEmitted`),
`IStoreController` + `StoreProduct` + `StorePurchaseAttempt`, `StorePurchaseCoordinator`,
`CostCheckout` (local-cost purchases), `CommerceAnalyticsEvent`.

**Seams (Core)** — `ICurrencyWallet`, `IEntitlementStore`, `IItemInventory`, `IRewardedAdPlayer`,
`ICommerceClock`.

**Runtime** — `CommerceService` (`BuyAsync`, `RestoreAsync`, `AttachStore`, `CanAfford`, `PriceFor`,
`ReconcileEntitlements`, `AnalyticsEmitted`/`PurchaseGranted`/`RewardApplied`, editor
`EditorSimulateGrant`), `CommerceCatalogSection`, `StorePlatformResolver`, `UnityCommerceClock`.

**UnityIap (gated)** — `UnityIapStoreController : IStoreController, IStoreListener`.
**Firebase (gated)** — `FirebasePurchaseVerifier : IPurchaseVerifier`.

## Catalog document shape (in `commerce.catalogJson`)

```json
{ "products": {
  "coins_small": { "category": "currency", "kind": "consumable",
    "cost": { "kind": "realmoney" },
    "sku": { "googleplay": "com.game.coins_small", "appstore": "com.game.coins_small" },
    "rewards": [ { "type": "currency", "id": "coins", "amount": 100 } ] },
  "remove_ads": { "kind": "nonconsumable",
    "cost": { "kind": "realmoney" },
    "sku": { "googleplay": "com.game.remove_ads", "appstore": "com.game.remove_ads" },
    "rewards": [ { "type": "entitlement", "id": "no_ads" } ] },
  "double_reward": { "cost": { "kind": "rewardedad" },
    "rewards": [ { "type": "currency", "id": "coins", "amount": 20 } ] },
  "starter": { "cost": { "kind": "selectable", "options": [
        { "kind": "currency", "currencyId": "gems", "amount": 100 }, { "kind": "rewardedad" } ] },
    "rewards": [ { "type": "item", "id": "potion", "count": 3 } ] } } }
```

Cost kinds: `free`, `currency`, `rewardedad`, `realmoney`, `selectable`. Reward types: `currency`,
`item`, `entitlement` (+ game-registered). A malformed document keeps the last-good catalog (no crash).

## Security model (non-negotiable)

- **Verify before grant.** A store "success" is never sufficient. `PurchasePipeline.ProcessReceiptAsync`
  grants ONLY on `IPurchaseVerifier`'s verified-success. No code path grants otherwise.
- **Idempotent grant.** Dedup on the store order-id and, when present, the server's canonical order-id.
  A replayed / duplicated / retried receipt is recognized and NOT re-granted; the ledger holds exactly
  one entry. A second attempt short-circuits before re-verifying.
- **Interrupted-purchase recovery.** The store holds a transaction **pending** until the client has
  verified + granted it, then `FinishTransaction` confirms it. A paid-but-ungranted purchase (crash /
  offline) is redelivered via `IStoreController.PendingTransaction` on next launch and reconciled to
  exactly one grant — never lost, never doubled.
- **Restore.** Re-grants only non-consumables / active subscriptions; consumables are not restored.
- **Refund / void.** `ReconcileEntitlements(ownedFromServer)` follows the server's authoritative state
  — a revoked non-consumable is removed on the next reconcile.
- **Rewarded cost.** Succeeds only on a fully-watched ad (via the `IRewardedAdPlayer` seam);
  skip/fail/no-fill grants nothing.
- **Unknown product** fails cleanly (surfaced config/programmer error, not a silent no-op).
- **No receipt/payment data in player-save** — the ledger is its only home.
- **Editor test grants** (`EditorSimulateGrant`) compile only under `UNITY_EDITOR`.

## Analytics wiring

Commerce emits `CommerceAnalyticsEvent` (`purchase_started` / `purchase_completed {product, price,
currency}` / `purchase_failed {product, reason}`) via the local `AnalyticsEmitted` event. The game
forwards it to its provider — the shapes match `IAnalyticsProvider.Track(name, properties)`:

```csharp
commerce.AnalyticsEmitted += e => analyticsProvider.Track(e.Name, e.Properties);
```

No dependency on the HubApp analytics assembly.

## Verification

- **Core (mono/csc):** `csc -nologo -warn:0 -out:/tmp/pf_commerce.exe Assets/PFound/Commerce/Core/Runtime/*.cs Assets/PFound/Commerce/Core/Tests/*.cs && mono /tmp/pf_commerce.exe` — **96 tests green**.
- **Unity:** all four non-gated assemblies (`Core`, `Core.Tests`, `Commerce`, `Commerce.Tests`)
  compile with the console clean (0 errors); `UnityIap` + `Firebase` stay excluded (defines off, Unity
  IAP package not installed). EditMode tests (`Tests/`) exercise the full flow with stub store /
  verifier / ad-play.

## Deviations from the build spec

- **Unity IAP wrapper isolated in its own `PFOUND_UNITY_IAP`-gated assembly** (`UnityIap/`) rather than
  inline in `Runtime/` (spec §5). This mirrors the Firebase/BestHTTP gating pattern so `Runtime`
  compiles without the IAP package present. The store transaction is reached through the engine-free
  `IStoreController` seam; the Runtime `CommerceService.AttachStore(...)` wires the concrete wrapper
  when the package is installed.
- **Store flow + coordinator live in Core, not Runtime.** `IStoreController` and
  `StorePurchaseCoordinator` are engine-free, so the whole buy → verify → grant → finish and
  restore/interrupted-recovery paths are unit-tested under mono with a stub store (spec keeps Core
  IAP-free — the seam carries no Unity IAP types).
- **`FinishTransaction` added to the store seam** to implement the server-authoritative "hold pending
  until granted, then confirm" handshake (Unity IAP `PurchaseProcessingResult.Pending` →
  `ConfirmPendingPurchase`). Not spelled out in the spec but required to make interrupted-purchase
  recovery lossless.
- **Catalog JSON parsed by an engine-free reader in Core** (`JsonValue` / `ProductCatalogReader`) so the
  catalog round-trip is mono-testable; the Runtime only pulls the raw JSON string from the RGC section.
- **Reward/energy/capacity as an open set.** Only `currency` / `item` / `entitlement` are built in
  (entitlement covers no-ads unlock + subscriptions); life/energy maps to a currency, and games register
  their own reward type + resolver + catalog factory (`RegisterRewardResolver` /
  `RegisterCatalogRewardFactory`) — honoring the spec's "open set" without hardcoding every kind.

## Server-side note

The client cannot validate receipts itself. A companion backend is required (tracked separately): a
Firebase Cloud Function (or self-host endpoint) that validates the receipt against Google Play / Apple,
writes the durable ledger, returns verified/failed + canonical order-id, and a refund/void webhook
(Google RTDN / Apple Server Notifications) that drives `ReconcileEntitlements`. This module ships the
CLIENT contract (`IPurchaseVerifier`) only.
