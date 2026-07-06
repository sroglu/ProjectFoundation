using System;
using System.IO;

namespace PFound.RemoteResourceCache.Core
{
    /// <summary>
    /// <see cref="IBlobStore"/> over a directory. Each key maps to one file named by the xxHash3 of the key
    /// (fast non-crypto — filename derivation, not integrity), so arbitrary keys/URLs become collision-resistant,
    /// filesystem-safe names. Writes publish atomically (unique temp file + rename) so a crash or cancelled fetch
    /// never leaves a partial blob at the final path — a present file is therefore always complete, which is what
    /// makes the offline-first read safe.
    /// </summary>
    public sealed class FileBlobStore : IBlobStore
    {
        private readonly string _root;

        public FileBlobStore(string rootDirectory)
        {
            _root = rootDirectory;
            Directory.CreateDirectory(_root);
        }

        /// <summary>The bare filename (no directory) a key maps to — the disk index reconciles files by this name.</summary>
        public string FileNameFor(string key) => XxHash3.HashToHex(key);

        public bool TryRead(string key, out byte[] bytes)
        {
            string path = PathFor(key);
            if (!File.Exists(path))
            {
                bytes = null;
                return false;
            }
            bytes = File.ReadAllBytes(path);
            return true;
        }

        public void Write(string key, byte[] bytes)
        {
            string path = PathFor(key);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temp, bytes);
            // Rename over any prior blob: Move won't overwrite, so clear the old file first. The window is
            // harmless because the temp already holds the complete new bytes.
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public bool Exists(string key) => File.Exists(PathFor(key));

        public void Delete(string key)
        {
            string path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }

        public DateTime GetTimestampUtc(string key) => File.GetLastWriteTimeUtc(PathFor(key));

        private string PathFor(string key) => Path.Combine(_root, FileNameFor(key));
    }
}
