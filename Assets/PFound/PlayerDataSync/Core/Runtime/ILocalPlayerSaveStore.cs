namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// One durable local save copy for an install, kept on the device so the game reads instantly and keeps
    /// working offline. It holds the serialized blob plus a <see cref="LocalSaveEntry.PendingUpload"/> flag
    /// that marks a write which has not yet reached the backend — the flag is itself durable, so a crash or
    /// force-quit between a local save and a successful sync still leaves the write queued to retry. Because
    /// conflict resolution is last-write-wins by counter, a single latest entry is a sufficient queue: an
    /// unsynced newer save supersedes any older unsynced one. The engine-free interface lives in Core so the
    /// whole offline/retry/conflict path is testable with an in-memory store; the Unity layer supplies the
    /// persistentDataPath-backed, atomically-written implementation.
    /// </summary>
    public interface ILocalPlayerSaveStore
    {
        bool TryLoad(string installId, out LocalSaveEntry entry);

        /// <summary>Persist the entry for the install. Implementations MUST write atomically (temp + replace) so a crash never yields a corrupt half-file.</summary>
        void Save(string installId, LocalSaveEntry entry);
    }

    /// <summary>The durable local copy: the serialized blob and whether it still needs to be pushed to the backend.</summary>
    public readonly struct LocalSaveEntry
    {
        public readonly string Blob;
        public readonly bool PendingUpload;

        public LocalSaveEntry(string blob, bool pendingUpload)
        {
            Blob = blob;
            PendingUpload = pendingUpload;
        }
    }
}
