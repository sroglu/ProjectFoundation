using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;
using UnityEditor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The audit pass: enumerate the assets authored into <see cref="AssetGroup"/>s — the same source the bundle
    /// build reads — read each one's importer settings, run the <see cref="AssetPolicy"/>, and REPORT the
    /// violations. It never mutates an importer; fixing is the separate, opt-in <see cref="AssetOptimizer"/>.
    /// </summary>
    public static class AssetAuditor
    {
        /// <summary>Audits every asset reachable from <paramref name="groups"/> against <paramref name="policy"/>.</summary>
        public static AssetAuditReport Audit(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)
            => AuditPaths(CollectAssetPaths(groups), policy);

        /// <summary>
        /// The full audit: the per-asset policy report plus the cross-bundle duplicate-dependency findings, the
        /// latter delegated to ContentDelivery's <see cref="BundleDuplicateAnalyzer"/> (reused, not reimplemented).
        /// </summary>
        public static AssetAuditResult AuditAll(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)
            => new AssetAuditResult(Audit(groups, policy), BundleDuplicateAnalyzer.Analyze(groups));

        /// <summary>Audits a specific set of asset paths — the testable seam (no AssetGroup plumbing required).</summary>
        public static AssetAuditReport AuditPaths(IEnumerable<string> assetPaths, AssetPolicy policy)
        {
            var textures = new List<TextureImporterFacts>();
            var meshes = new List<MeshImporterFacts>();
            var seen = new HashSet<string>();
            int scanned = 0;

            foreach (string path in assetPaths)
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                scanned++;

                // An asset is backed by exactly one importer, so at most one of these is non-null.
                var texture = TextureImporterReader.Read(path);
                if (texture != null) { textures.Add(texture); continue; }

                var mesh = MeshImporterReader.Read(path);
                if (mesh != null) meshes.Add(mesh);
            }

            var violations = AssetPolicyEvaluator.Evaluate(textures, meshes, policy);
            return new AssetAuditReport(policy.Describe(), scanned, violations);
        }

        /// <summary>Distinct asset paths authored across the groups (entries with a resolvable asset).</summary>
        public static List<string> CollectAssetPaths(IReadOnlyList<AssetGroup> groups)
        {
            var paths = new List<string>();
            if (groups == null) return paths;

            foreach (var group in groups)
            {
                if (group == null || group.Entries == null) continue;
                foreach (var entry in group.Entries)
                {
                    if (entry == null || entry.Asset == null) continue;
                    string path = AssetDatabase.GetAssetPath(entry.Asset);
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
            }
            return paths;
        }
    }
}
