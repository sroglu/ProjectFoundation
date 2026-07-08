using System;
using System.Collections.Generic;

namespace PFound.AssetPipeline.Core
{
    /// <summary>A set of two or more distinct texture files whose pixel content is byte-for-byte identical.</summary>
    public sealed class DuplicateTextureGroup
    {
        /// <summary>The shared content-hash the members collide on.</summary>
        public string ContentHash;

        /// <summary>The asset paths of the identical textures (sorted), one wasted copy per extra entry.</summary>
        public string[] Paths;
    }

    /// <summary>
    /// Groups distinct texture files by identical pixel content: given each texture's path and a hash OF ITS PIXELS
    /// (not its import settings), it returns the sets that share a hash — i.e. the same image shipped under several
    /// paths. This is orthogonal to the cross-bundle duplicate-DEPENDENCY check (one asset pulled into many bundles):
    /// here the FILES differ but the CONTENT is the same. Pure — the editor supplies the pixel hashes — so the
    /// grouping is unit-testable without Unity.
    /// </summary>
    public static class DuplicateTextureFinder
    {
        /// <summary>
        /// Every content hash mapped to two or more paths, as sorted <see cref="DuplicateTextureGroup"/>s (groups
        /// sorted by hash, paths sorted within each). Entries with an empty path or hash are ignored.
        /// </summary>
        public static List<DuplicateTextureGroup> Find(IEnumerable<KeyValuePair<string, string>> pathToContentHash)
        {
            if (pathToContentHash == null) throw new ArgumentNullException(nameof(pathToContentHash));

            var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in pathToContentHash)
            {
                if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value)) continue;
                if (!byHash.TryGetValue(kv.Value, out var paths))
                    byHash[kv.Value] = paths = new List<string>();
                if (!paths.Contains(kv.Key)) paths.Add(kv.Key);
            }

            var groups = new List<DuplicateTextureGroup>();
            foreach (var kv in byHash)
            {
                if (kv.Value.Count < 2) continue;
                kv.Value.Sort(StringComparer.Ordinal);
                groups.Add(new DuplicateTextureGroup { ContentHash = kv.Key, Paths = kv.Value.ToArray() });
            }
            groups.Sort((a, b) => string.CompareOrdinal(a.ContentHash, b.ContentHash));
            return groups;
        }
    }
}
