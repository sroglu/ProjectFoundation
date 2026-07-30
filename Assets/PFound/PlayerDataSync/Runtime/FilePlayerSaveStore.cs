using System.IO;
using PFound.PlayerDataSync.Core;

namespace PFound.PlayerDataSync
{
    /// <summary>
    /// The durable local save copy on disk (under the app's persistentDataPath). It writes atomically —
    /// serialize to a temp file, then replace the target in one filesystem operation — so a crash or kill
    /// mid-write can never leave a corrupt half-file; the previous good copy stays intact until the new one
    /// is fully written. Each install gets its own file. The stored form wraps the opaque blob with the
    /// pending-upload flag, so a write that never reached the backend is still known to be pending after a
    /// restart and gets retried on the next flush. Only file IO and path composition live here — the
    /// serialization is the engine-free Core's, so this class stays a thin durability shim.
    /// </summary>
    public sealed class FilePlayerSaveStore : ILocalPlayerSaveStore
    {
        const string BlobKey = "blob";
        const string PendingKey = "pending";
        const string FilePrefix = "playersave_";
        const string FileSuffix = ".json";

        readonly string _directory;

        public FilePlayerSaveStore(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public bool TryLoad(string installId, out LocalSaveEntry entry)
        {
            string path = PathFor(installId);
            if (!File.Exists(path))
            {
                entry = default;
                return false;
            }

            string text = File.ReadAllText(path);
            PlayerSaveNode envelope = PlayerSaveNode.Parse(text);
            entry = new LocalSaveEntry(envelope.GetString(BlobKey, ""), envelope.GetBool(PendingKey, false));
            return true;
        }

        public void Save(string installId, LocalSaveEntry entry)
        {
            PlayerSaveNode envelope = PlayerSaveNode.NewObject();
            envelope.SetString(BlobKey, entry.Blob);
            envelope.SetBool(PendingKey, entry.PendingUpload);

            string path = PathFor(installId);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, envelope.ToJson());

            // Replace atomically so readers never observe a partially written file.
            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
        }

        string PathFor(string installId) => Path.Combine(_directory, FilePrefix + installId + FileSuffix);
    }
}
