#if PFOUND_FIREBASE
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Firestore;
using PFound.PlayerDataSync.Core;

namespace PFound.PlayerDataSync.Firebase
{
    /// <summary>
    /// The Firestore backing for <see cref="IRemotePlayerStore"/>. Each install is one document in a
    /// collection, keyed by install id. The hybrid contract maps directly onto a Firestore document: the
    /// opaque blob is a single string field, and the extracted fields are sibling first-class fields on the
    /// same document, so the Firestore console / an admin query can filter players by level, spend, ban
    /// status or last-seen without ever reading the blob. A single document set is atomic, satisfying the
    /// all-or-nothing write requirement for free. This whole assembly is gated by the <c>PFOUND_FIREBASE</c>
    /// scripting define (and the asmdef define constraint), so it does not compile — and the module builds
    /// fine — until the Firebase SDK is present. Network faults propagate as exceptions from the returned
    /// task; the sync engine treats those as offline and keeps the durable local copy.
    /// </summary>
    public sealed class FirestoreRemotePlayerStore : IRemotePlayerStore
    {
        public const string DefaultCollection = "playerSaves";

        // Field names on the Firestore document. The blob is one field; the rest are the queryable projection.
        const string BlobField = "blob";
        const string SchemaVersionField = "schemaVersion";
        const string LevelField = "level";
        const string TotalSpendField = "totalSpend";
        const string BanStatusField = "banStatus";
        const string LastSeenField = "lastSeen";

        readonly FirebaseFirestore _firestore;
        readonly string _collection;

        public FirestoreRemotePlayerStore(string collection = DefaultCollection)
            : this(FirebaseFirestore.DefaultInstance, collection) { }

        public FirestoreRemotePlayerStore(FirebaseFirestore firestore, string collection = DefaultCollection)
        {
            _firestore = firestore;
            _collection = collection;
        }

        public async Task<RemoteLoadOutcome> LoadAsync(string installId, CancellationToken cancellationToken = default)
        {
            DocumentReference document = _firestore.Collection(_collection).Document(installId);
            DocumentSnapshot snapshot = await document.GetSnapshotAsync(cancellationToken);

            if (!snapshot.Exists || !snapshot.TryGetValue(BlobField, out string blob))
            {
                return RemoteLoadOutcome.Missing;
            }
            return RemoteLoadOutcome.Of(blob);
        }

        public Task SaveAsync(string installId, RemotePlayerRecord record, CancellationToken cancellationToken = default)
        {
            DocumentReference document = _firestore.Collection(_collection).Document(installId);
            var fields = new Dictionary<string, object>
            {
                { BlobField, record.Blob },
                { SchemaVersionField, record.Fields.SchemaVersion },
                { LevelField, record.Fields.Level },
                { TotalSpendField, record.Fields.TotalSpend },
                { BanStatusField, record.Fields.BanStatus },
                { LastSeenField, record.Fields.LastSeenUnixMs }
            };
            // A single document set is atomic — no half-written state is ever visible.
            return document.SetAsync(fields, SetOptions.Overwrite);
        }
    }
}
#endif
