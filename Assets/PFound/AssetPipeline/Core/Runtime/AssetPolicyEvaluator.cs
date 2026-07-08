using System;
using System.Collections.Generic;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// The pure decision layer: given an asset's importer <see cref="TextureImporterFacts">facts</see> and a
    /// <see cref="AssetPolicy"/>, it returns the <see cref="PolicyViolation">violations</see> — and nothing else.
    /// It reads no files and mutates nothing, so the whole rule set is unit-testable with synthetic facts and no
    /// Unity. The editor audit pass feeds it real importer data; the apply pass acts on what it returns.
    /// </summary>
    public static class AssetPolicyEvaluator
    {
        private const string Rgba32 = "RGBA32";

        /// <summary>Appends every texture rule this set of facts breaks under <paramref name="policy"/> to <paramref name="into"/>.</summary>
        public static void EvaluateTexture(TextureImporterFacts f, AssetPolicy policy, List<PolicyViolation> into)
            => EvaluateTexture(f, policy, into, null);

        /// <summary>
        /// Appends every texture rule this set of facts breaks to <paramref name="into"/>, gated by an optional
        /// reference <paramref name="graph"/>: an unreferenced asset is skipped entirely (it never ships), and an
        /// atlas member is checked only for the CPU-copy rule — its format/size/compression are the atlas's job, not
        /// this importer's. When <paramref name="graph"/> is null, no gating applies (every asset is evaluated).
        /// </summary>
        public static void EvaluateTexture(
            TextureImporterFacts f, AssetPolicy policy, List<PolicyViolation> into, IAssetReferenceGraph graph)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (into == null) throw new ArgumentNullException(nameof(into));

            // Unreferenced assets never reach a build; auditing their importer settings is noise.
            if (graph != null && !graph.IsReferenced(f.AssetPath)) return;

            // Read/Write keeps a CPU copy regardless of how the pixels are packed, so it is the one rule that still
            // applies to an atlas member; everything else about an atlas member is governed by the atlas.
            if (policy.DisallowTextureReadWrite && f.ReadWriteEnabled && !f.ReadWriteRequired)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureReadWriteEnabled,
                    "Read/Write enabled (keeps a CPU copy alongside the GPU one); disable unless a script samples pixels"));

            if (graph != null && graph.IsAtlasPacked(f.AssetPath)) return;

            // Default-platform format/crunch/size rules.
            EvaluateCompressionRules(f.AssetPath, f.Compression, f.Crunched, f.MaxTextureSize, f.FormatName, "", policy, into);

            // Sprites are exempt: Unity locks their NPOT scaling off and atlases them into a power-of-two sheet, so
            // the npotScale lever only applies to non-sprite textures.
            if (policy.RequirePowerOfTwoOrNpotScale && !f.IsSprite &&
                !f.IsPowerOfTwo && f.NpotScale == NpotScale.None)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureNotPowerOfTwoNoNpotScale,
                    $"texture {f.Width}x{f.Height} is non-power-of-two with NPOT scaling off; can't block-compress cleanly"));

            // Mipmaps only waste memory for sprites drawn at native resolution; 3D textures legitimately need them.
            if (policy.DisallowSpriteMipmaps && f.IsSprite && f.MipmapsEnabled)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.SpriteMipmapsEnabled,
                    "sprite has mipmaps enabled (~33% dead weight for 2D/UI at native resolution); disable mipmaps"));

            // Per-platform overrides (iOS/Android/…): the same format/crunch/size rules, scoped to each overridden
            // target, so a platform that ships uncompressed while the default is compressed is still caught.
            if (f.PlatformOverrides != null)
                for (int i = 0; i < f.PlatformOverrides.Count; i++)
                {
                    var p = f.PlatformOverrides[i];
                    if (p == null || !p.Overridden) continue;
                    EvaluateCompressionRules(f.AssetPath, p.Compression, p.Crunched, p.MaxTextureSize, p.FormatName, p.Platform, policy, into);
                }
        }

        /// <summary>The format/crunch/size sub-rules, applied to either the default platform ("") or one override.</summary>
        private static void EvaluateCompressionRules(
            string assetPath, TextureCompressionLevel compression, bool crunched, int maxTextureSize, string formatName,
            string platform, AssetPolicy policy, List<PolicyViolation> into)
        {
            bool uncompressed = compression == TextureCompressionLevel.Uncompressed;
            bool isRgba32 = !string.IsNullOrEmpty(formatName) &&
                            formatName.IndexOf(Rgba32, StringComparison.OrdinalIgnoreCase) >= 0;

            // Compression: RequireCompressed catches every uncompressed texture; otherwise the narrower
            // RGBA32-only rule still vetoes the worst case. The two never double-report the same asset.
            if (uncompressed)
            {
                if (policy.RequireCompressed)
                    into.Add(new PolicyViolation(assetPath, ViolationCode.TextureUncompressed,
                        $"stored uncompressed ({Describe(formatName)}); use a compressed format", platform));
                else if (policy.DisallowUncompressedRgba32 && isRgba32)
                    into.Add(new PolicyViolation(assetPath, ViolationCode.TextureUncompressedRgba32,
                        "stored uncompressed RGBA32; use a compressed format", platform));
            }

            if (policy.DisallowCrunch && crunched)
                into.Add(new PolicyViolation(assetPath, ViolationCode.TextureCrunched,
                    "crunch compression on; manage size by maxTextureSize instead", platform));

            if (maxTextureSize > policy.MaxTextureSize)
                into.Add(new PolicyViolation(assetPath, ViolationCode.TextureExceedsMaxSize,
                    $"maxTextureSize {maxTextureSize} > policy cap {policy.MaxTextureSize}", platform));
        }

        /// <summary>Appends every mesh rule this set of facts breaks under <paramref name="policy"/> to <paramref name="into"/>.</summary>
        public static void EvaluateMesh(MeshImporterFacts f, AssetPolicy policy, List<PolicyViolation> into)
            => EvaluateMesh(f, policy, into, null);

        /// <summary>Mesh evaluation gated by an optional reference <paramref name="graph"/> (unreferenced meshes are skipped).</summary>
        public static void EvaluateMesh(MeshImporterFacts f, AssetPolicy policy, List<PolicyViolation> into, IAssetReferenceGraph graph)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (into == null) throw new ArgumentNullException(nameof(into));

            if (graph != null && !graph.IsReferenced(f.AssetPath)) return;

            if (policy.DisallowMeshReadWrite && f.ReadWriteEnabled && !f.ReadWriteRequired)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.MeshReadWriteEnabled,
                    "Read/Write enabled (doubles mesh memory); disable unless runtime mesh access is required"));

            if (policy.MinMeshCompression != MeshCompressionLevel.Off &&
                f.MeshCompression < policy.MinMeshCompression)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.MeshUncompressed,
                    $"mesh compression {f.MeshCompression} < policy minimum {policy.MinMeshCompression}"));
        }

        /// <summary>Convenience over the two appenders: evaluates a batch and returns one combined violation list.</summary>
        public static List<PolicyViolation> Evaluate(
            IEnumerable<TextureImporterFacts> textures,
            IEnumerable<MeshImporterFacts> meshes,
            AssetPolicy policy)
            => Evaluate(textures, meshes, policy, null);

        /// <summary>Batch evaluation gated by an optional reference <paramref name="graph"/> (see the texture overload).</summary>
        public static List<PolicyViolation> Evaluate(
            IEnumerable<TextureImporterFacts> textures,
            IEnumerable<MeshImporterFacts> meshes,
            AssetPolicy policy,
            IAssetReferenceGraph graph)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var violations = new List<PolicyViolation>();
            if (textures != null)
                foreach (var t in textures) if (t != null) EvaluateTexture(t, policy, violations, graph);
            if (meshes != null)
                foreach (var m in meshes) if (m != null) EvaluateMesh(m, policy, violations, graph);
            return violations;
        }

        private static string Describe(string formatName) =>
            string.IsNullOrEmpty(formatName) ? "uncompressed" : formatName;
    }
}
