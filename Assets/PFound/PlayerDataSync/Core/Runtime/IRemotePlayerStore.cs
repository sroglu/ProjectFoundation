using System.Threading;
using System.Threading.Tasks;

namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// The single backend seam. Everything above it — the save DTO, blob (de)serialization, extracted-field
    /// derivation, schema migration, offline queue, conflict policy, local cache — is backend-agnostic and
    /// talks only to this interface, so game code never names a backend and swapping Firestore for
    /// Postgres/Supabase is swapping the registered implementation. An implementation does exactly two
    /// things: load the stored blob for an install id, and upsert a record for an install id. The write is
    /// expected to be atomic (a single Firestore document set; a transactional upsert on a self-host), so a
    /// crash mid-write can never leave a half-document. Network failures surface as exceptions from the
    /// returned task; the sync engine treats those as "offline" and keeps the durable local copy.
    /// </summary>
    public interface IRemotePlayerStore
    {
        Task<RemoteLoadOutcome> LoadAsync(string installId, CancellationToken cancellationToken = default);

        Task SaveAsync(string installId, RemotePlayerRecord record, CancellationToken cancellationToken = default);
    }
}
