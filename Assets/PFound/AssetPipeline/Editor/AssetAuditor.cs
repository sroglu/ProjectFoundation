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
        /// <summary>
        /// Audits every asset reachable from <paramref name="groups"/> against <paramref name="policy"/>, gated by a
        /// reference graph: atlas members skip the per-texture format rules and truly-orphan assets are skipped (the
        /// group's own entries are seeded as roots, so they are never mistaken for orphans).
        /// </summary>
        public static AssetAuditReport Audit(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)
        {
            var paths = CollectAssetPaths(groups);
            return AuditPaths(paths, policy, AssetReferenceGraph.Build(paths));
        }

        /// <summary>
        /// The full audit: the per-asset policy report, the cross-bundle duplicate-DEPENDENCY findings (delegated to
        /// ContentDelivery's <see cref="BundleDuplicateAnalyzer"/>, reused), and the content-identical duplicate-
        /// TEXTURE findings over the audited textures.
        /// </summary>
        public static AssetAuditResult AuditAll(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)
        {
            var paths = CollectAssetPaths(groups);
            var report = AuditPaths(paths, policy, AssetReferenceGraph.Build(paths));
            var duplicateDeps = BundleDuplicateAnalyzer.Analyze(groups);
            var duplicateTextures = TextureContentDuplicateAnalyzer.Analyze(paths);
            return new AssetAuditResult(report, duplicateDeps, duplicateTextures);
        }

        /// <summary>Audits a specific set of asset paths — the testable seam (no AssetGroup plumbing, no gating).</summary>
        public static AssetAuditReport AuditPaths(IEnumerable<string> assetPaths, AssetPolicy policy)
            => AuditPaths(assetPaths, policy, null);

        /// <summary>
        /// Audits a set of asset paths, optionally gated by a reference <paramref name="graph"/>. Each path is read
        /// as a texture, a model mesh, OR a raw Mesh asset — so the mesh Read/Write rule reaches ALL mesh sources,
        /// not only <c>ModelImporter</c>-backed ones.
        /// </summary>
        public static AssetAuditReport AuditPaths(IEnumerable<string> assetPaths, AssetPolicy policy, IAssetReferenceGraph graph)
        {
            var textures = new List<TextureImporterFacts>();
            var meshes = new List<MeshImporterFacts>();
            var seen = new HashSet<string>();
            int scanned = 0;

            foreach (string path in assetPaths)
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                scanned++;

                var texture = TextureImporterReader.Read(path);
                if (texture != null) { textures.Add(texture); continue; }

                // A model-backed mesh, or (failing that) a raw Mesh asset carrying its own readable flag.
                var mesh = MeshImporterReader.Read(path) ?? MeshAssetReader.Read(path);
                if (mesh != null) meshes.Add(mesh);
            }

            var violations = AssetPolicyEvaluator.Evaluate(textures, meshes, policy, graph);
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
