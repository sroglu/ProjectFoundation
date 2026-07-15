using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PFound.LocalizationService.Unity
{
    /// <summary>
    /// Hash-name resolution for the on-disk localization table set. Deployed tables may be named with a
    /// trailing content-address hash (e.g. <c>LocalizationText-&lt;hash&gt;.lzma</c>) so the runtime can tell
    /// deterministically which table set is present and compare it against a remote manifest — instead of
    /// blindly taking the first file that matches the prefix.
    /// </summary>
    public static partial class TableFileLoader
    {
        /// <summary>
        /// Extracts the hash segment from a table file name: strips the extension, then returns the text
        /// after the last <see cref="LocalizationConstants.FileNameDelimiter"/>. A file with no delimiter
        /// (an un-hashed baseline such as <c>LocalizationText.txt</c>) yields its bare name.
        /// </summary>
        public static string GetFileHashFromFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            int idx = name.LastIndexOf(LocalizationConstants.FileNameDelimiter);
            return idx >= 0 ? name.Substring(idx + 1) : name;
        }

        /// <summary>
        /// Extracts the content-address hash carried by a remote table URL. A URL names a file the same way
        /// the on-disk set does (<c>.../LocalizationText-&lt;hash&gt;.lzma</c>), so the last path segment is
        /// treated as a file name and its hash segment returned.
        /// </summary>
        public static string GetHashFromUrl(string url) => GetFileHashFromFileName(url);

        // --- hash-addressed file names / paths -------------------------------------------------------

        private static string ContentFileName(string hash)
            => LocalizationConstants.ContentFilePrefix + LocalizationConstants.FileNameDelimiter + hash + LocalizationConstants.PlainExtension;

        private static string DefinitionsFileName(string hash)
            => LocalizationConstants.DefinitionsFilePrefix + LocalizationConstants.FileNameDelimiter + hash + LocalizationConstants.PlainExtension;

        /// <summary>Writable (persistent) path a downloaded content table with <paramref name="hash"/> lands at.</summary>
        public static string PersistentContentFilePath(string hash) => Path.Combine(PersistentTablesDir, ContentFileName(hash));

        /// <summary>Writable (persistent) path a downloaded definitions table with <paramref name="hash"/> lands at.</summary>
        public static string PersistentDefinitionsFilePath(string hash) => Path.Combine(PersistentTablesDir, DefinitionsFileName(hash));

        /// <summary>
        /// Remote (relative) path of the content table for <paramref name="hash"/>: the tables folder plus the
        /// hash-addressed file name, joined with URL separators. Compose against a CDN base URL to fetch.
        /// </summary>
        public static string RemoteContentPath(string hash) => LocalizationConstants.TablesFolderName + "/" + ContentFileName(hash);

        /// <summary>Remote (relative) path of the definitions table for <paramref name="hash"/>. See <see cref="RemoteContentPath"/>.</summary>
        public static string RemoteDefinitionsPath(string hash) => LocalizationConstants.TablesFolderName + "/" + DefinitionsFileName(hash);

        // --- refresh trigger + writable-dir hygiene --------------------------------------------------

        /// <summary>
        /// The update trigger: true when the <paramref name="remote"/> hash pair is valid and differs from the
        /// <paramref name="local"/> pair (or no valid local set is present) — i.e. a refresh is warranted.
        /// </summary>
        public static bool NeedsRefresh(LocalizationHashData local, LocalizationHashData remote)
        {
            if (!remote.IsValid) return false;
            if (!local.IsValid) return true;
            return !string.Equals(local.ContentHash, remote.ContentHash, StringComparison.Ordinal)
                || !string.Equals(local.DefinitionsHash, remote.DefinitionsHash, StringComparison.Ordinal);
        }

        /// <summary>
        /// Empties the writable Localizables directory (creating it if absent) so a refreshed table set never
        /// coexists with a stale one under a different hash. Call before downloading a new set.
        /// </summary>
        public static void ClearPersistentTablesDir()
        {
            string dir = PersistentTablesDir;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                return;
            }
            var info = new DirectoryInfo(dir);
            foreach (var file in info.GetFiles()) file.Delete();
            foreach (var sub in info.GetDirectories()) sub.Delete(true);
        }

        /// <summary>
        /// Resolves the content + definitions hashes for the deployed table set under the StreamingAssets
        /// Localizables folder. See <see cref="GetLocalFileHashes(string)"/>.
        /// </summary>
        public static LocalizationHashData GetLocalFileHashes()
            => GetLocalFileHashes(StreamingAssetsTablesDir);

        /// <summary>
        /// Scans <paramref name="directory"/> for exactly one definitions file and exactly one content
        /// file (matched by prefix; the plain and compressed variants of the same file share a hash and
        /// count once) and returns their hashes as a <see cref="LocalizationHashData"/>. A missing
        /// directory, a missing file, or an ambiguous match is a boundary condition: it logs a warning and
        /// returns <see cref="LocalizationHashData.Invalid"/>.
        /// </summary>
        public static LocalizationHashData GetLocalFileHashes(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Debug.LogWarning($"[Localization] Localizables directory not found: {directory}");
                    return LocalizationHashData.Invalid;
                }

                string definitionsHash = ResolveSingleHash(directory, LocalizationConstants.DefinitionsFilePrefix);
                string contentHash = ResolveSingleHash(directory, LocalizationConstants.ContentFilePrefix);
                if (definitionsHash == null || contentHash == null)
                    return LocalizationHashData.Invalid;

                return new LocalizationHashData(contentHash, definitionsHash);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Localization] Failed to read localizables directory '{directory}': {ex.Message}");
                return LocalizationHashData.Invalid;
            }
        }

        /// <summary>
        /// Finds the single hash carried by the files that start with <paramref name="prefix"/> in
        /// <paramref name="directory"/>. Both table extensions are considered; because the plain and
        /// compressed variants of the same table carry the same hash, distinct hashes must number exactly
        /// one. Returns null (with a warning) when none or more than one is present.
        /// </summary>
        private static string ResolveSingleHash(string directory, string prefix)
        {
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ext in new[] { LocalizationConstants.PlainExtension, LocalizationConstants.CompressedExtension })
                foreach (var path in Directory.GetFiles(directory, prefix + "*" + ext))
                    hashes.Add(GetFileHashFromFileName(path));

            if (hashes.Count == 0)
            {
                Debug.LogWarning($"[Localization] No '{prefix}' table file found in {directory}.");
                return null;
            }
            if (hashes.Count > 1)
            {
                Debug.LogWarning($"[Localization] Expected exactly one '{prefix}' table set in {directory}, found {hashes.Count} distinct hashes.");
                return null;
            }

            foreach (var hash in hashes) return hash;
            return null;
        }
    }
}
