using System;
using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The apply pass: explicitly corrects the importer settings behind a set of <see cref="PolicyViolation"/>s and
    /// reimports. This is opt-in — it is NEVER a side effect of an audit; a caller (menu item / CI step) runs it on
    /// purpose. It only fixes what the policy flagged, so a clean audit is a no-op.
    ///
    /// <para>The whole batch runs inside a single <c>StartAssetEditing</c>/<c>StopAssetEditing</c> scope so the
    /// reimports coalesce (one refresh, not one per asset), with an optional cancelable progress callback.</para>
    /// </summary>
    public static class AssetOptimizer
    {
        /// <summary>
        /// A per-asset progress hook. Called before each asset with (0-based index, total, asset path); return
        /// <c>true</c> to cancel the remaining work. Null = no reporting, never cancels.
        /// </summary>
        public delegate bool ProgressCallback(int index, int total, string assetPath);

        /// <summary>Applies fixes for every violation in <paramref name="report"/>. Returns the number of assets changed.</summary>
        public static int Apply(AssetAuditReport report, AssetPolicy policy) => Apply(report.Violations, policy, null);

        /// <summary>Applies fixes for every violation in <paramref name="report"/>, with a cancelable progress hook.</summary>
        public static int Apply(AssetAuditReport report, AssetPolicy policy, ProgressCallback progress)
            => Apply(report.Violations, policy, progress);

        /// <summary>Applies fixes for the given violations, batched (one asset-editing scope). Returns assets changed.</summary>
        public static int Apply(IReadOnlyList<PolicyViolation> violations, AssetPolicy policy)
            => Apply(violations, policy, null);

        /// <summary>
        /// Applies fixes for the given violations grouped per asset, inside one begin/end asset-editing scope, with a
        /// cancelable <paramref name="progress"/> hook. The default-platform properties are written via the top-level
        /// importer; any platform-tagged violation additionally rewrites that overridden platform.
        /// </summary>
        public static int Apply(IReadOnlyList<PolicyViolation> violations, AssetPolicy policy, ProgressCallback progress)
        {
            if (violations == null) throw new ArgumentNullException(nameof(violations));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            // Group per asset: the default-platform codes and, separately, the codes tagged to each overridden platform.
            var byAsset = new Dictionary<string, AssetFixes>(StringComparer.Ordinal);
            for (int i = 0; i < violations.Count; i++)
            {
                var v = violations[i];
                if (!byAsset.TryGetValue(v.AssetPath, out var fixes))
                    byAsset[v.AssetPath] = fixes = new AssetFixes();
                if (string.IsNullOrEmpty(v.Platform)) fixes.DefaultCodes.Add(v.Code);
                else
                {
                    if (!fixes.PlatformCodes.TryGetValue(v.Platform, out var set))
                        fixes.PlatformCodes[v.Platform] = set = new HashSet<ViolationCode>();
                    set.Add(v.Code);
                }
            }

            int changed = 0;
            int index = 0;
            int total = byAsset.Count;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in byAsset)
                {
                    if (progress != null && progress(index, total, kv.Key)) break;
                    index++;

                    // An asset is backed by one importer; whichever fixer matches it does the work.
                    if (FixTexture(kv.Key, kv.Value, policy) || FixMesh(kv.Key, kv.Value.DefaultCodes, policy))
                        changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            return changed;
        }

        private sealed class AssetFixes
        {
            public readonly HashSet<ViolationCode> DefaultCodes = new HashSet<ViolationCode>();
            public readonly Dictionary<string, HashSet<ViolationCode>> PlatformCodes =
                new Dictionary<string, HashSet<ViolationCode>>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Corrects the texture-importer settings behind the default-platform codes AND each overridden platform's
        /// codes on one asset; reimports if anything changed. Default-platform writes go through the top-level
        /// properties; per-platform writes go through <c>SetPlatformTextureSettings</c> on the overridden target.
        /// </summary>
        private static bool FixTexture(string assetPath, AssetFixes fixes, AssetPolicy policy)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return false;

            bool changed = FixDefaultTexture(importer, fixes.DefaultCodes, policy);

            foreach (var kv in fixes.PlatformCodes)
                changed |= FixPlatformTexture(importer, kv.Key, kv.Value, policy);

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        private static bool FixDefaultTexture(TextureImporter importer, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
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

            return changed;
        }

        private static bool FixPlatformTexture(
            TextureImporter importer, string platform, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
            var ps = importer.GetPlatformTextureSettings(platform);
            bool changed = false;

            if (codes.Contains(ViolationCode.TextureUncompressed) || codes.Contains(ViolationCode.TextureUncompressedRgba32))
            {
                ps.textureCompression = TextureImporterCompression.Compressed;
                ps.crunchedCompression = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureCrunched))
            {
                ps.crunchedCompression = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.TextureExceedsMaxSize))
            {
                ps.maxTextureSize = policy.MaxTextureSize;
                changed = true;
            }

            if (changed)
            {
                ps.overridden = true;
                importer.SetPlatformTextureSettings(ps);
            }
            return changed;
        }

        /// <summary>
        /// Corrects mesh Read/Write and compression from the default-platform codes: a model-backed mesh via the
        /// <see cref="ModelImporter"/>; a raw Mesh asset via its own readable flag (serialized on the mesh object).
        /// </summary>
        private static bool FixMesh(string assetPath, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter importer)
                return FixModelMesh(importer, codes, policy);

            if (codes.Contains(ViolationCode.MeshReadWriteEnabled))
                return DisableMeshAssetReadWrite(assetPath);

            return false;
        }

        private static bool FixModelMesh(ModelImporter importer, HashSet<ViolationCode> codes, AssetPolicy policy)
        {
            bool changed = false;

            if (codes.Contains(ViolationCode.MeshReadWriteEnabled))
            {
                importer.isReadable = false;
                changed = true;
            }

            if (codes.Contains(ViolationCode.MeshUncompressed))
            {
                // The mesh-asset reader reports Off, so this code only reaches a real ModelImporter.
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

        /// <summary>Clears the readable flag on every Mesh at a raw Mesh-asset path via its serialized property.</summary>
        private static bool DisableMeshAssetReadWrite(string assetPath)
        {
            bool changed = false;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(obj is Mesh mesh) || !mesh.isReadable) continue;
                var so = new SerializedObject(mesh);
                var prop = so.FindProperty("m_IsReadable");
                if (prop == null) continue;
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mesh);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
            return changed;
        }
    }
}
