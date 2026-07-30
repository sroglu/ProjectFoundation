using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PFound.PlayerDataSync.Core.Tests
{
    /// <summary>
    /// Standalone mono/csc runner for the engine-free PlayerDataSync core: blob round-trip, extracted-field
    /// derivation + drift-proofing, schema migration (upgrade + downgrade refusal), conflict policy (LWW +
    /// custom merge hook), forward-compat field preservation, and the full sync engine path (fresh default,
    /// save+flush, offline queue + retry + reconnect, sync-point write counting, cross-device conflict).
    /// </summary>
    internal static class Program
    {
        const string Install = "install-A";
        const int CurrentSchema = 2;

        static int Main()
        {
            RoundTrip_IsLossless();
            ExtractedFields_DeriveFromBlob_AndTrackChanges();
            Migration_UpgradesOldDocument();
            Migration_RefusesDowngrade();
            Conflict_LastWriteWins_ByCounter();
            Conflict_CustomMergeHookOverrides();
            ForwardCompat_UnknownFieldsPreserved();
            Blob_IsBackendNeutral_AndDeterministic();

            Engine_LoadWithNothing_YieldsFreshDefault().GetAwaiter().GetResult();
            Engine_SaveThenFlush_PushesAndReloads().GetAwaiter().GetResult();
            Engine_OfflineSave_QueuesThenSyncsOnReconnect().GetAwaiter().GetResult();
            Engine_Flush_NoPending_IsNoOp().GetAwaiter().GetResult();
            Engine_Save_DoesNotHitNetwork().GetAwaiter().GetResult();
            Engine_Flush_AdoptsNewerRemote().GetAwaiter().GetResult();
            Engine_Flush_RetriesThenDefers_WhenOffline().GetAwaiter().GetResult();

            return TestKit.Summary("PlayerDataSync.Core");
        }

        // ---- pure DTO / codec / hybrid ----

        static void RoundTrip_IsLossless()
        {
            PlayerSave save = PlayerSave.CreateDefault(CurrentSchema);
            save.SaveCounter = 7;
            save.Data.SetInt("level", 12);
            save.Data.SetDouble("totalSpend", 4.99);
            save.Data.SetString("name", "hero \"one\"");
            save.Data.SetBool("tutorialDone", true);
            PlayerSaveNode inv = PlayerSaveNode.NewArray();
            inv.Add(PlayerSaveNode.Of("sword"));
            inv.Add(PlayerSaveNode.Of("shield"));
            save.Data.Set("inventory", inv);

            string blob = PlayerSaveCodec.Serialize(save);
            PlayerSave back = PlayerSaveCodec.Deserialize(blob);

            TestKit.Check(back.SchemaVersion == CurrentSchema, "round-trip schema version");
            TestKit.Check(back.SaveCounter == 7, "round-trip counter");
            TestKit.Check(back.Data.GetInt("level", 0) == 12, "round-trip int");
            TestKit.Check(Math.Abs(back.Data.GetDouble("totalSpend", 0) - 4.99) < 0.0001, "round-trip double");
            TestKit.Check(back.Data.GetString("name", "") == "hero \"one\"", "round-trip escaped string");
            TestKit.Check(back.Data.GetBool("tutorialDone", false), "round-trip bool");
            TestKit.Check(back.Data.TryGet("inventory", out PlayerSaveNode a) && a.Count == 2 && a.Item(0).AsString() == "sword", "round-trip array");
            TestKit.Check(PlayerSaveCodec.Serialize(back) == blob, "round-trip re-serializes identically");
        }

        static void ExtractedFields_DeriveFromBlob_AndTrackChanges()
        {
            var extractor = new CanonicalPlayerFieldExtractor();
            PlayerSave save = PlayerSave.CreateDefault(CurrentSchema);
            save.Data.SetInt("level", 5);
            save.Data.SetDouble("totalSpend", 19.98);
            save.Data.SetString("banStatus", "shadow");
            save.Data.SetLong("lastSeen", 1234);

            ExtractedFields fields = ExtractedFields.Derive(Install, save, extractor);
            TestKit.Check(fields.InstallId == Install, "extracted install id is the key");
            TestKit.Check(fields.SchemaVersion == CurrentSchema, "extracted schema from envelope");
            TestKit.Check(fields.Level == 5, "extracted level from blob");
            TestKit.Check(Math.Abs(fields.TotalSpend - 19.98) < 0.0001, "extracted spend from blob");
            TestKit.Check(fields.BanStatus == "shadow", "extracted ban status from blob");
            TestKit.Check(fields.LastSeenUnixMs == 1234, "extracted last-seen from blob");

            // Changing the blob changes the derived fields (single source of truth = blob).
            save.Data.SetInt("level", 9);
            ExtractedFields after = ExtractedFields.Derive(Install, save, extractor);
            TestKit.Check(after.Level == 9, "changing blob changes derived field");

            // ExtractedFields exposes no public setter/constructor, so it cannot be authored independently of
            // a save — Derive is the only way to build one. That invariant is enforced at compile time.
        }

        static void Migration_UpgradesOldDocument()
        {
            // A v1 document stored with the old "hp" key.
            PlayerSave v1 = new PlayerSave(1, 3, PlayerSaveNode.NewObject());
            v1.Data.SetLong("hp", 42);
            string blob = PlayerSaveCodec.Serialize(v1);

            var migrator = new PlayerSaveMigrator(CurrentSchema, new IPlayerSaveMigration[] { new RenameHpToHealthMigration() });
            var packer = new PlayerSavePacker(new CanonicalPlayerFieldExtractor(), migrator);
            PlayerSave upgraded = packer.UnpackBlob(blob);

            TestKit.Check(upgraded.SchemaVersion == CurrentSchema, "migrated to current schema");
            TestKit.Check(!upgraded.Data.Has("hp"), "old key removed by migration");
            TestKit.Check(upgraded.Data.GetLong("health", 0) == 42, "value moved to new key by migration");
        }

        static void Migration_RefusesDowngrade()
        {
            PlayerSave future = new PlayerSave(CurrentSchema + 5, 1, PlayerSaveNode.NewObject());
            var migrator = PlayerSaveMigrator.None(CurrentSchema);
            bool refused = false;
            try { migrator.MigrateToCurrent(future); }
            catch (PlayerSaveVersionException) { refused = true; }
            TestKit.Check(refused, "downgrade (newer stored schema) is refused, not mangled");
        }

        static void Conflict_LastWriteWins_ByCounter()
        {
            var policy = new LastWriteWinsConflictPolicy();
            PlayerSave low = new PlayerSave(CurrentSchema, 4, PlayerSaveNode.NewObject());
            PlayerSave high = new PlayerSave(CurrentSchema, 9, PlayerSaveNode.NewObject());
            TestKit.Check(ReferenceEquals(policy.Resolve(low, high), high), "higher remote counter wins");
            TestKit.Check(ReferenceEquals(policy.Resolve(high, low), high), "higher local counter wins");

            PlayerSave tieLocal = new PlayerSave(CurrentSchema, 5, PlayerSaveNode.NewObject());
            PlayerSave tieRemote = new PlayerSave(CurrentSchema, 5, PlayerSaveNode.NewObject());
            TestKit.Check(ReferenceEquals(policy.Resolve(tieLocal, tieRemote), tieLocal), "tie keeps local");
        }

        static void Conflict_CustomMergeHookOverrides()
        {
            var policy = new AdditiveMergeConflictPolicy();
            PlayerSave local = new PlayerSave(CurrentSchema, 2, PlayerSaveNode.NewObject());
            local.Data.SetLong("coins", 100);
            PlayerSave remote = new PlayerSave(CurrentSchema, 5, PlayerSaveNode.NewObject());
            remote.Data.SetLong("coins", 30);

            PlayerSave merged = policy.Resolve(local, remote);
            TestKit.Check(merged.Data.GetLong("coins", 0) == 130, "custom merge hook sums instead of picking one side");
            TestKit.Check(merged.SaveCounter == 6, "merge advances the counter past both inputs");
        }

        static void ForwardCompat_UnknownFieldsPreserved()
        {
            // A document written by a newer client carrying a field this build does not know about.
            const string blob = "{\"schemaVersion\":2,\"saveCounter\":1,\"data\":{\"level\":3,\"futureFeature\":{\"x\":1}}}";
            PlayerSave save = PlayerSaveCodec.Deserialize(blob);
            TestKit.Check(save.Data.Has("futureFeature"), "unknown field survives parse");
            string reserialized = PlayerSaveCodec.Serialize(save);
            PlayerSave again = PlayerSaveCodec.Deserialize(reserialized);
            TestKit.Check(again.Data.TryGet("futureFeature", out PlayerSaveNode f) && f.GetInt("x", 0) == 1, "unknown field survives re-serialize");
        }

        static void Blob_IsBackendNeutral_AndDeterministic()
        {
            // Same logical save authored in a different key order must yield the same blob string, so the
            // blob is a stable value any backend can store and compare.
            PlayerSave a = PlayerSave.CreateDefault(CurrentSchema);
            a.Data.SetInt("b", 2); a.Data.SetInt("a", 1);
            PlayerSave b = PlayerSave.CreateDefault(CurrentSchema);
            b.Data.SetInt("a", 1); b.Data.SetInt("b", 2);
            TestKit.Check(PlayerSaveCodec.Serialize(a) == PlayerSaveCodec.Serialize(b), "blob is deterministic regardless of authoring order");
        }

        // ---- sync engine ----

        static PlayerSaveSyncEngine BuildEngine(InMemoryRemotePlayerStore remote, InMemoryLocalPlayerSaveStore local, FixedClock clock, ImmediateSyncDelay delay)
        {
            var migrator = new PlayerSaveMigrator(CurrentSchema, new IPlayerSaveMigration[] { new RenameHpToHealthMigration() });
            return new PlayerSaveSyncEngine(
                Install, remote, local, new CanonicalPlayerFieldExtractor(), migrator,
                new LastWriteWinsConflictPolicy(), clock, delay, RetryPolicy.Default);
        }

        static async Task Engine_LoadWithNothing_YieldsFreshDefault()
        {
            var remote = new InMemoryRemotePlayerStore();
            var local = new InMemoryLocalPlayerSaveStore();
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), new ImmediateSyncDelay());

            PlayerSave loaded = await engine.LoadAsync();
            TestKit.Check(loaded.SchemaVersion == CurrentSchema, "no-save load is a fresh default at current schema");
            TestKit.Check(loaded.SaveCounter == 0, "fresh default has zero counter");
            TestKit.Check(local.WriteCount == 0, "fresh default is not persisted until first save");
        }

        static async Task Engine_SaveThenFlush_PushesAndReloads()
        {
            var remote = new InMemoryRemotePlayerStore();
            var local = new InMemoryLocalPlayerSaveStore();
            var clock = new FixedClock { NowUnixMs = 5000 };
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, clock, new ImmediateSyncDelay());

            await engine.LoadAsync();
            engine.Current.Data.SetInt("level", 8);
            engine.Save();
            SyncOutcome outcome = await engine.FlushAsync();

            TestKit.Check(outcome.Status == SyncStatus.Synced, "flush of a pending save reports Synced");
            TestKit.Check(remote.Has(Install), "backend now holds the record");
            TestKit.Check(remote.Get(Install).Fields.Level == 8, "backend record carries derived extracted level");
            TestKit.Check(remote.Get(Install).Fields.LastSeenUnixMs == 5000, "last-seen stamped from the clock into the blob");

            // A fresh engine reloading from the backend sees the same data.
            var local2 = new InMemoryLocalPlayerSaveStore();
            PlayerSaveSyncEngine engine2 = BuildEngine(remote, local2, clock, new ImmediateSyncDelay());
            PlayerSave reloaded = await engine2.LoadAsync();
            TestKit.Check(reloaded.Data.GetInt("level", 0) == 8, "reload from backend restores the same data");
        }

        static async Task Engine_OfflineSave_QueuesThenSyncsOnReconnect()
        {
            var remote = new InMemoryRemotePlayerStore { Online = false };
            var local = new InMemoryLocalPlayerSaveStore();
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), new ImmediateSyncDelay());

            await engine.LoadAsync();
            engine.Current.Data.SetInt("level", 3);
            engine.Save();
            SyncOutcome offline = await engine.FlushAsync();
            TestKit.Check(offline.Status == SyncStatus.Deferred, "offline flush defers the write");
            TestKit.Check(!remote.Has(Install), "nothing reached the backend while offline");
            TestKit.Check(local.TryLoad(Install, out LocalSaveEntry queued) && queued.PendingUpload, "write stays durably queued");

            remote.Online = true;
            SyncOutcome back = await engine.FlushAsync();
            TestKit.Check(back.Status == SyncStatus.Synced, "queued write syncs on reconnect");
            TestKit.Check(remote.Get(Install).Fields.Level == 3, "reconnected sync carries the queued data");
            TestKit.Check(local.TryLoad(Install, out LocalSaveEntry cleared) && !cleared.PendingUpload, "pending flag cleared after sync");
        }

        static async Task Engine_Flush_NoPending_IsNoOp()
        {
            var remote = new InMemoryRemotePlayerStore();
            var local = new InMemoryLocalPlayerSaveStore();
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), new ImmediateSyncDelay());

            await engine.LoadAsync();
            SyncOutcome outcome = await engine.FlushAsync();
            TestKit.Check(outcome.Status == SyncStatus.NothingPending, "flush with nothing pending is a no-op");
            TestKit.Check(remote.SaveCalls == 0, "no backend write when nothing pending");
        }

        static async Task Engine_Save_DoesNotHitNetwork()
        {
            var remote = new InMemoryRemotePlayerStore();
            var local = new InMemoryLocalPlayerSaveStore();
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), new ImmediateSyncDelay());

            await engine.LoadAsync();
            engine.Current.Data.SetInt("level", 1);
            engine.Save();
            engine.Current.Data.SetInt("level", 2);
            engine.Save();

            TestKit.Check(remote.SaveCalls == 0, "event-driven Save never touches the backend (no per-frame network)");
            TestKit.Check(local.WriteCount == 2, "each Save writes the durable local copy once");
        }

        static async Task Engine_Flush_AdoptsNewerRemote()
        {
            var remote = new InMemoryRemotePlayerStore();
            var local = new InMemoryLocalPlayerSaveStore();
            var packer = new PlayerSavePacker(new CanonicalPlayerFieldExtractor(), PlayerSaveMigrator.None(CurrentSchema));

            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), new ImmediateSyncDelay());
            await engine.LoadAsync();               // nothing anywhere -> fresh default (counter 0)
            engine.Current.Data.SetInt("level", 4);
            engine.Save();                          // local pending, counter 1

            // Meanwhile another device wrote a much-newer save to the backend.
            PlayerSave otherDevice = new PlayerSave(CurrentSchema, 999, PlayerSaveNode.NewObject());
            otherDevice.Data.SetInt("level", 77);
            remote.Seed(Install, packer.Pack(Install, otherDevice));

            SyncOutcome outcome = await engine.FlushAsync();
            TestKit.Check(outcome.Status == SyncStatus.AdoptedRemote, "flush adopts a newer backend save instead of overwriting it");
            TestKit.Check(engine.Current.Data.GetInt("level", 0) == 77, "adopted the backend's data");
            TestKit.Check(local.TryLoad(Install, out LocalSaveEntry e) && !e.PendingUpload, "adopting clears the local pending write");
        }

        static async Task Engine_Flush_RetriesThenDefers_WhenOffline()
        {
            var remote = new InMemoryRemotePlayerStore { Online = false };
            var local = new InMemoryLocalPlayerSaveStore();
            var delay = new ImmediateSyncDelay();
            PlayerSaveSyncEngine engine = BuildEngine(remote, local, new FixedClock(), delay);

            await engine.LoadAsync();
            engine.Current.Data.SetInt("level", 6);
            engine.Save();
            SyncOutcome outcome = await engine.FlushAsync();

            // Offline: the reconcile read already fails, so the write defers without spending retries on push.
            TestKit.Check(outcome.Status == SyncStatus.Deferred, "offline flush defers");
            TestKit.Check(local.TryLoad(Install, out LocalSaveEntry e) && e.PendingUpload, "write remains queued after a deferred flush");
        }
    }
}
