# PlayerDataSync

> **Module group — LiveOps** (`RemoteGameConfig`, `PlayerDataSync`, `Commerce`; analytics via HubApp's
> `IAnalyticsProvider`). Grouped by purpose. They are separately publishable, but the group is **not
> dependency-free**: `Commerce` uses `RemoteGameConfig` (product catalog), `PlayerDataSync` uses it
> (install-id), and `RemoteGameConfig` builds on `RemoteResourceCache`; optional/gated integrations are
> Firebase, Unity IAP, BestHTTP. See each module's **Dependencies** for exact edges.

## Purpose

Persists and syncs per-user authoritative game state to a backend and back, keyed by the stable
install id, as a **hybrid opaque blob**: the bulk of the save is one serialized JSON document the
backend stores verbatim (never interprets), while a small **fixed** set of extracted fields
(install id, schema version, level, total spend, ban status, last seen) is written as first-class
sibling fields/columns for server-side segmentation. Every extracted field is **derived from the
blob on write** — never authored independently — so they can never drift from the save they
describe. The store is backend-neutral (the same blob string is valid for any provider), schema is
versioned with an ordered migration pipeline, writes survive offline via a durable local queue with
retry/backoff, and divergent saves reconcile through a swappable conflict policy (default
last-write-wins by a monotonic counter). The backend sits behind one interface (`IRemotePlayerStore`,
Firestore first) so game code never names a backend.

## Boundary note

PlayerDataSync is the **remote / cloud-sync layer**. It sits ABOVE HubApp's LOCAL save subsystem
(HubApp owns `ISaveService`; this module deliberately does NOT reuse that name) and is **separate
from GameDataStore**, which is an in-memory runtime data model with no persistence. PlayerDataSync
persists a snapshot of the relevant game state; it does not absorb GameDataStore and adds no
persistence into it. Payment/receipt data is out of scope — the Commerce purchase ledger owns that.

## Assemblies

| Assembly | Path | Depends On |
|---|---|---|
| `PFound.PlayerDataSync.Core` | `Core/Runtime/` | (engine-free, `noEngineReferences`) |
| `PFound.PlayerDataSync.Core.Tests` | `Core/Tests/` | Core (engine-free standalone mono/csc runner) |
| `PFound.PlayerDataSync` | `Runtime/` | Core, `PFound.RemoteGameConfig.Core` (for `IInstallIdentity`) |
| `PFound.PlayerDataSync.Firebase` | `Firebase/` | Core, Runtime — `PFOUND_FIREBASE`-gated |
| `PFound.PlayerDataSync.Tests` | `Tests/` | Runtime, Core (EditMode / NUnit) |

All asmdefs are `autoReferenced:false` (side-by-side convention). Core is engine-free.

## Dependencies

- **PFound modules:** `PFound.RemoteGameConfig.Core` — Runtime consumes its `IInstallIdentity` for
  the install id (see the identity decision below). Core depends on no other module.
- **Third-party:** Firebase/Firestore SDK (`PFOUND_FIREBASE`-gated; the Firebase assembly does not
  compile until the SDK + define are present, and the module builds fine without it). A self-host
  (Supabase/Postgres over `PFOUND_BESTHTTP`) provider is a future drop-in behind the same seam.

## Public API

### Core (engine-free)

- **`PlayerSave`** — the backend-neutral save the game owns: `SchemaVersion`, `SaveCounter`
  (monotonic), `Data` (a `PlayerSaveNode` tree). `CreateDefault(schemaVersion)`.
- **`PlayerSaveNode`** — mutable JSON-like document tree with typed accessors (`GetInt/GetLong/
  GetDouble/GetString/GetBool`, `Set*`, `GetOrAddObject`, arrays) and **deterministic** serialization
  (`ToJson` / `Parse`); ordinal key order makes blobs reproducible and comparable by value.
- **`PlayerSaveCodec`** — `Serialize(save)` / `Deserialize(blob)`; the blob is the opaque unit.
- **`ExtractedFields`** + **`IPlayerFieldExtractor`** / **`CanonicalPlayerFieldExtractor`** — the
  hybrid contract. `ExtractedFields.Derive(installId, save, extractor)` is the ONLY constructor (no
  public setter), so extracted fields are always a projection of the blob. The extractor is a
  game-supplied seam locating the segmentation values (and stamping last-seen) in the data tree.
- **`PlayerSaveMigrator`** + **`IPlayerSaveMigration`** — ordered, pure-function migrations;
  `MigrateToCurrent(save)` upgrades on load and refuses a downgrade with `PlayerSaveVersionException`.
- **`IPlayerSaveConflictPolicy`** / **`LastWriteWinsConflictPolicy`** — swappable conflict hook.
- **`IRemotePlayerStore`** — the backend seam: `LoadAsync(installId)` → `RemoteLoadOutcome` (blob or
  Missing), `SaveAsync(installId, RemotePlayerRecord)`. `RemotePlayerRecord` = blob + `ExtractedFields`.
- **`ILocalPlayerSaveStore`** — the durable-local seam (`LocalSaveEntry` = blob + `PendingUpload`).
- **`PlayerSaveSyncEngine`** — the orchestrator: `Current` (never null), `LoadAsync` (local+remote
  reconcile), `Save()` (event-driven local commit, bumps counter, stamps last-seen, no network),
  `FlushAsync`/`SyncNowAsync` (push pending with retry/backoff, reconcile, may adopt a newer remote).
- Support: `ISyncClock`/`SystemSyncClock`, `ISyncDelay`/`TaskSyncDelay`, `RetryPolicy`, `SyncOutcome`
  (`SyncStatus`: NothingPending / Synced / AdoptedRemote / Deferred), `PlayerSavePacker`.

### Runtime (Unity glue)

- **`FilePlayerSaveStore`** — `ILocalPlayerSaveStore` under persistentDataPath with **atomic** write
  (temp + replace) and a durable pending flag.
- **`PlayerDataSyncInstaller.Build(...)`** — wires the engine from `IInstallIdentity` + an
  `IRemotePlayerStore` + the app environment (defaults: canonical extractor, LWW, system clock/delay).
- **`PlayerDataSyncBehaviour`** — thin MonoBehaviour turning app pause/quit into flush sync points
  (no `Update`, so saves stay event-driven, never per-frame).

### Firebase (`PFOUND_FIREBASE`-gated)

- **`FirestoreRemotePlayerStore`** — `IRemotePlayerStore` over Firestore: blob = one document field,
  extracted fields = sibling fields on the same document; a single doc set is atomic.

## Conflict policy

Default is **last-write-wins by the monotonic `SaveCounter`** carried in each save (higher counter =
more recent; tie keeps the local copy as this device's latest intent). It is a seam
(`IPlayerSaveConflictPolicy`) — a game with mergeable state supplies a smarter merge without touching
the rest of the module. Reconciliation runs on load and before each flush: if the backend holds a
newer save (another device / reinstall), the policy applies and the backend copy may be adopted
locally instead of overwriting it.

## Install-identity decision

Runtime consumes **`IInstallIdentity` from `PFound.RemoteGameConfig.Core`** (option a) rather than
minting a second GUID or introducing a parallel identity type. Rationale: RemoteGameConfig already
owns and persists the canonical per-install GUID (`PersistentInstallIdentity`, a PlayerPrefs GUID —
not `deviceUniqueIdentifier`); a second seam would risk two ids / drift and duplicate a public name
(CODING-STYLE §8). The coupling is confined to the Unity glue and is on the pure engine-free `.Core`
(one interface). Core itself stays identity-agnostic — it keys everything off a plain `installId`
string — so the engine-free layer has zero cross-module dependency. If the per-feature-submodule
distribution model later wants no edge between these modules, `IInstallIdentity` can be promoted to a
tiny shared identity module with no consumer change (Runtime already programs to the interface).

## Deviations from spec

1. **Offline-queue + retry + conflict logic lives in Core, not Runtime.** The spec's folder layout
   suggested these belong to the Unity layer. They are implemented as an engine-free
   `PlayerSaveSyncEngine` (POCO) in Core instead, with only the durable file IO, real clock/delay,
   install id, and lifecycle hooks in Runtime. Rationale: it makes the offline/retry/conflict/
   sync-point behavior fully unit-testable under mono/csc (the spec's §7 asks those behaviors to be
   tested; testing them without Unity is strictly better and keeps parity with the ECS/RemoteGameConfig
   verification pattern). No capability is lost — the Runtime still owns every engine-touching concern.
2. **`RemoteLoadOutcome` returns the blob only, not a full record.** Extracted fields are a write-side
   projection for the backend's own querying; the client reads back only the blob and re-derives the
   fields on its next write. A backend that does not parse the blob could not reconstruct the fields
   anyway. `SaveAsync` still takes the full `RemotePlayerRecord` (blob + derived fields).
3. **Single-slot durable queue.** Because conflict resolution is last-write-wins by counter, one
   latest pending entry is a sufficient "queue" (a newer unsynced save supersedes any older one), so
   the local store keeps one entry + a pending flag rather than an unbounded list.

## Verification

- **Core:** 50/50 tests green via the standalone mono/csc runner
  (`csc -nologo -warn:0 -out:/tmp/pf_pds.exe Assets/PFound/PlayerDataSync/Core/Runtime/*.cs Assets/PFound/PlayerDataSync/Core/Tests/*.cs && mono /tmp/pf_pds.exe`).
- **Unity:** all four assemblies (Core, Runtime, Firebase-gated-off, EditMode Tests) compile in the
  editor with a clean console; every public type loads. Firebase assembly is excluded without the
  `PFOUND_FIREBASE` define, and the module builds with the define off.
