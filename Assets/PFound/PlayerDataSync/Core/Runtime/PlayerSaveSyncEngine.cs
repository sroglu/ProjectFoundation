using System;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The backend-agnostic sync orchestrator. It holds the current save in memory, keeps a durable local
    /// copy so the game reads instantly and works offline, and drives the explicit sync points against an
    /// <see cref="IRemotePlayerStore"/>. It is deliberately engine-free (no UnityEngine): the whole
    /// load/reconcile/commit/flush/retry/conflict path is unit-testable under mono with an in-memory local
    /// store and a stub remote — the Unity layer only supplies the persistentDataPath-backed local store, a
    /// real clock/delay, the install id, and the lifecycle hooks that call <see cref="FlushAsync"/>.
    ///
    /// Design points that mirror the spec's edge rules:
    /// - <see cref="Current"/> is valid immediately after construction (a fresh default), so the game never
    ///   sees null and a first launch / offline start just reads defaults.
    /// - Local writes (<see cref="Save"/>) are event-driven and cheap — they never touch the network; the
    ///   network is only reached at explicit flushes, never per frame.
    /// - A backend failure is treated as offline: the write stays queued in the durable local copy and is
    ///   retried on the next flush, so a transient network fault never loses a write.
    /// - Before overwriting the backend, a flush reconciles with it; if another device wrote a newer save the
    ///   conflict policy applies (default last-write-wins by counter).
    /// </summary>
    public sealed class PlayerSaveSyncEngine
    {
        readonly string _installId;
        readonly IRemotePlayerStore _remote;
        readonly ILocalPlayerSaveStore _local;
        readonly IPlayerFieldExtractor _extractor;
        readonly PlayerSavePacker _packer;
        readonly IPlayerSaveConflictPolicy _conflict;
        readonly ISyncClock _clock;
        readonly ISyncDelay _delay;
        readonly RetryPolicy _retry;
        readonly int _currentVersion;

        /// <summary>The save the game reads and mutates. Never null after construction.</summary>
        public PlayerSave Current { get; private set; }

        public PlayerSaveSyncEngine(
            string installId,
            IRemotePlayerStore remote,
            ILocalPlayerSaveStore local,
            IPlayerFieldExtractor extractor,
            PlayerSaveMigrator migrator,
            IPlayerSaveConflictPolicy conflict,
            ISyncClock clock,
            ISyncDelay delay,
            RetryPolicy retry)
        {
            _installId = installId;
            _remote = remote;
            _local = local;
            _extractor = extractor;
            _packer = new PlayerSavePacker(extractor, migrator);
            _conflict = conflict;
            _clock = clock;
            _delay = delay;
            _retry = retry;
            _currentVersion = migrator.CurrentVersion;

            // Fully usable on a fresh default before any load (first launch / offline), like the config service.
            Current = PlayerSave.CreateDefault(_currentVersion);
        }

        /// <summary>
        /// Load the player's save: reconcile the durable local copy with the backend and set
        /// <see cref="Current"/>. With no local and no remote, the result is a fresh default (not persisted
        /// until the first <see cref="Save"/>). Offline, the last local copy is returned. When both exist,
        /// the conflict policy decides; if the backend copy wins it is adopted locally.
        /// </summary>
        public async Task<PlayerSave> LoadAsync(CancellationToken cancellationToken = default)
        {
            bool haveLocal = _local.TryLoad(_installId, out LocalSaveEntry localEntry);
            PlayerSave localSave = haveLocal ? _packer.UnpackBlob(localEntry.Blob) : null;

            RemoteReadResult read = await ReadRemote(cancellationToken);

            if (read.Reached && read.Outcome.Found)
            {
                PlayerSave remoteSave = _packer.UnpackBlob(read.Outcome.Blob);
                if (localSave == null)
                {
                    Current = remoteSave;
                    PersistLocal(remoteSave, pendingUpload: false);
                }
                else
                {
                    PlayerSave winner = _conflict.Resolve(localSave, remoteSave);
                    bool remoteWon = ReferenceEquals(winner, remoteSave);
                    Current = winner;
                    // If the backend copy did not win, the local (or merged) result must still reach the
                    // backend to converge it, so it stays queued.
                    PersistLocal(winner, pendingUpload: !remoteWon);
                }
            }
            else if (read.Reached)
            {
                // Backend reachable but empty: local is authoritative and must be pushed up.
                if (localSave != null)
                {
                    Current = localSave;
                    PersistLocal(localSave, pendingUpload: true);
                }
                else
                {
                    Current = PlayerSave.CreateDefault(_currentVersion);
                }
            }
            else
            {
                // Offline: keep the durable local copy exactly as it is (its pending flag is preserved).
                Current = localSave ?? PlayerSave.CreateDefault(_currentVersion);
            }

            return Current;
        }

        /// <summary>
        /// Commit the current save to memory and the durable local copy, and queue it for the backend. This
        /// is the event-driven save (level end, purchase, pause) — it stamps last-seen, bumps the monotonic
        /// counter, and writes locally; it does NOT touch the network. Call <see cref="FlushAsync"/> to push.
        /// </summary>
        public void Save()
        {
            _extractor.Stamp(Current, _clock.NowUnixMs);
            Current.SaveCounter += 1;
            PersistLocal(Current, pendingUpload: true);
        }

        /// <summary>Alias for <see cref="FlushAsync"/> — an explicit "sync to backend now" sync point.</summary>
        public Task<SyncOutcome> SyncNowAsync(CancellationToken cancellationToken = default) => FlushAsync(cancellationToken);

        /// <summary>
        /// Push the pending local write to the backend, reconciling first. Nothing pending ⇒ no-op. Offline
        /// or exhausted retries ⇒ the write stays queued (deferred), never lost. If the backend meanwhile
        /// holds a newer save, the conflict policy applies and the backend copy may be adopted locally.
        /// </summary>
        public async Task<SyncOutcome> FlushAsync(CancellationToken cancellationToken = default)
        {
            if (!_local.TryLoad(_installId, out LocalSaveEntry entry) || !entry.PendingUpload)
            {
                return new SyncOutcome(SyncStatus.NothingPending, Current);
            }

            RemoteReadResult read = await ReadRemote(cancellationToken);
            if (!read.Reached)
            {
                return new SyncOutcome(SyncStatus.Deferred, Current);
            }

            PlayerSave toPush = _packer.UnpackBlob(entry.Blob);

            if (read.Outcome.Found)
            {
                PlayerSave remoteSave = _packer.UnpackBlob(read.Outcome.Blob);
                PlayerSave winner = _conflict.Resolve(toPush, remoteSave);
                if (ReferenceEquals(winner, remoteSave))
                {
                    Current = remoteSave;
                    PersistLocal(remoteSave, pendingUpload: false);
                    return new SyncOutcome(SyncStatus.AdoptedRemote, Current);
                }
                toPush = winner;
            }

            RemotePlayerRecord record = _packer.Pack(_installId, toPush);
            bool pushed = await PushWithRetry(record, cancellationToken);
            if (pushed)
            {
                Current = toPush;
                PersistLocal(toPush, pendingUpload: false);
                return new SyncOutcome(SyncStatus.Synced, Current);
            }

            return new SyncOutcome(SyncStatus.Deferred, Current);
        }

        void PersistLocal(PlayerSave save, bool pendingUpload)
        {
            string blob = PlayerSaveCodec.Serialize(save);
            _local.Save(_installId, new LocalSaveEntry(blob, pendingUpload));
        }

        async Task<bool> PushWithRetry(RemotePlayerRecord record, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < _retry.MaxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    await _delay.Wait(_retry.DelayForAttempt(attempt - 1), cancellationToken);
                }
                if (await TrySaveRemote(record, cancellationToken)) return true;
            }
            return false;
        }

        // Remote calls are the external IO boundary: a network failure is translated into a reachability
        // result so the engine keeps the durable local copy instead of throwing. Cancellation propagates.
        async Task<RemoteReadResult> ReadRemote(CancellationToken cancellationToken)
        {
            try
            {
                RemoteLoadOutcome outcome = await _remote.LoadAsync(_installId, cancellationToken);
                return new RemoteReadResult(true, outcome);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new RemoteReadResult(false, RemoteLoadOutcome.Missing);
            }
        }

        async Task<bool> TrySaveRemote(RemotePlayerRecord record, CancellationToken cancellationToken)
        {
            try
            {
                await _remote.SaveAsync(_installId, record, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        readonly struct RemoteReadResult
        {
            public readonly bool Reached;
            public readonly RemoteLoadOutcome Outcome;

            public RemoteReadResult(bool reached, RemoteLoadOutcome outcome)
            {
                Reached = reached;
                Outcome = outcome;
            }
        }
    }
}
