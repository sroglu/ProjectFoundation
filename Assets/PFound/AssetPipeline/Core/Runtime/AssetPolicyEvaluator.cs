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
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (into == null) throw new ArgumentNullException(nameof(into));

            bool uncompressed = f.Compression == TextureCompressionLevel.Uncompressed;
            bool isRgba32 = !string.IsNullOrEmpty(f.FormatName) &&
                            f.FormatName.IndexOf(Rgba32, StringComparison.OrdinalIgnoreCase) >= 0;

            // Compression: RequireCompressed catches every uncompressed texture; otherwise the narrower
            // RGBA32-only rule still vetoes the worst case. The two never double-report the same asset.
            if (uncompressed)
            {
                if (policy.RequireCompressed)
                    into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureUncompressed,
                        $"stored uncompressed ({Describe(f.FormatName)}); use a compressed format"));
                else if (policy.DisallowUncompressedRgba32 && isRgba32)
                    into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureUncompressedRgba32,
                        "stored uncompressed RGBA32; use a compressed format"));
            }

            if (policy.DisallowCrunch && f.Crunched)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureCrunched,
                    "crunch compression on; manage size by maxTextureSize instead"));

            if (f.MaxTextureSize > policy.MaxTextureSize)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureExceedsMaxSize,
                    $"maxTextureSize {f.MaxTextureSize} > policy cap {policy.MaxTextureSize}"));

            // Sprites are exempt: Unity locks their NPOT scaling off and atlases them into a power-of-two sheet, so
            // the npotScale lever only applies to non-sprite textures.
            if (policy.RequirePowerOfTwoOrNpotScale && !f.IsSprite &&
                !f.IsPowerOfTwo && f.NpotScale == NpotScale.None)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureNotPowerOfTwoNoNpotScale,
                    $"texture {f.Width}x{f.Height} is non-power-of-two with NPOT scaling off; can't block-compress cleanly"));

            if (policy.DisallowTextureReadWrite && f.ReadWriteEnabled && !f.ReadWriteRequired)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.TextureReadWriteEnabled,
                    "Read/Write enabled (keeps a CPU copy alongside the GPU one); disable unless a script samples pixels"));

            // Mipmaps only waste memory for sprites drawn at native resolution; 3D textures legitimately need them.
            if (policy.DisallowSpriteMipmaps && f.IsSprite && f.MipmapsEnabled)
                into.Add(new PolicyViolation(f.AssetPath, ViolationCode.SpriteMipmapsEnabled,
                    "sprite has mipmaps enabled (~33% dead weight for 2D/UI at native resolution); disable mipmaps"));
        }

        /// <summary>Appends every mesh rule this set of facts breaks under <paramref name="policy"/> to <paramref name="into"/>.</summary>
        public static void EvaluateMesh(MeshImporterFacts f, AssetPolicy policy, List<PolicyViolation> into)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (into == null) throw new ArgumentNullException(nameof(into));

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
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var violations = new List<PolicyViolation>();
            if (textures != null)
                foreach (var t in textures) if (t != null) EvaluateTexture(t, policy, violations);
            if (meshes != null)
                foreach (var m in meshes) if (m != null) EvaluateMesh(m, policy, violations);
            return violations;
        }

        private static string Describe(string formatName) =>
            string.IsNullOrEmpty(formatName) ? "uncompressed" : formatName;
    }
}
