using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using UnityEditor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The apply pass: explicitly corrects the importer settings behind a set of <see cref="PolicyViolation"/>s and
    /// reimports. This is opt-in — it is NEVER a side effect of an audit; a caller (menu item / CI step) runs it on
    /// purpose. It only fixes what the policy flagged, so a clean audit is a no-op.
    /// </summary>
    public static class AssetOptimizer
    {
        /// <summary>Applies fixes for every violation in <paramref name="report"/>. Returns the number of assets changed.</summary>
        public static int Apply(AssetAuditReport report, AssetPolicy policy) => Apply(report.Violations, policy);

        /// <summary>Applies fixes for the given violations, batched per asset (one reimport each). Returns assets changed.</summary>
        public static int Apply(IReadOnlyList<PolicyViolation> violations, AssetPolicy policy)
        {
            var byAsset = new Dictionary<string, HashSet<ViolationCode>>();
            for (int i = 0; i < violations.Count; i++)
            {
                var v = violations[i];
                if (!byAsset.TryGetValue(v.AssetPath, out var codes))
                    byAsset[v.AssetPath] = codes = new HashSet<ViolationCode>();
                codes.Add(v.Code);
            }

            int changed = 0;
            foreach (var kv in byAsset)
                // An asset is backed by one importer; whichever fixer matches it does the work.
                if (FixTexture(kv.Key, kv.Value, policy) || FixMesh(kv.Key, kv.Value, policy))
                    changed++;
            return changed;
        }

        /// <summary>
        /// Corrects the texture-importer settings behind <paramref name="codes"/> on one asset; reimports if anything
        /// changed. Writes the default-platform settings via the top-level TextureImporter properties (these ARE the
        /// default platform). Per-platform overrides (iOS/Android via SetPlatformTextureSettings) are reliable too
        /// (measured) — they are a deliberately DEFERRED capability, not yet exposed by the policy (see STATUS).
        /// </summary>
        private static bool FixTexture(string assetPath, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return false;

            bool changed = false;

            if (codes.Contains(ViolationCode.TextureUncompressed) || codes.Contains(ViolationCode.TextureUncompressedRgba32))
            {
                // Let the importer pick a compressed format per platform; drop crunch (size is managed by resolution).
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureCrunched))
            {
                importer.crunchedCompression = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureExceedsMaxSize))
            {
                importer.maxTextureSize = policy.MaxTextureSize;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureNotPowerOfTwoNoNpotScale))
            {
                // Snap to the nearest power of two (actionable on non-sprite textures; sprites keep native size).
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureReadWriteEnabled))
            {
                importer.isReadable = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.SpriteMipmapsEnabled))
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        /// <summary>Corrects the model-importer settings behind <paramref name="codes"/> on one asset; reimports if anything changed.</summary>
        private static bool FixMesh(string assetPath, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter importer)) return false;

            bool changed = false;

            if (codes.Contains(ViolationCode.MeshReadWriteEnabled))
            {
                importer.isReadable = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.MeshUncompressed))
            {
                importer.meshCompression = ToModelCompression(policy.MinMeshCompression);
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        private static ModelImporterMeshCompression ToModelCompression(MeshCompressionLevel level)
        {
            switch (level)
            {
                case MeshCompressionLevel.Low: return ModelImporterMeshCompression.Low;
                case MeshCompressionLevel.Medium: return ModelImporterMeshCompression.Medium;
                case MeshCompressionLevel.High: return ModelImporterMeshCompression.High;
                default: return ModelImporterMeshCompression.Off;
            }
        }
    }
}
