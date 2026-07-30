using System.Text;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// The set of import rules the audit checks assets against. Every field is a knob: the defaults are sensible
    /// MOBILE settings (compressed, non-crunched, resolution-capped), but the whole policy is meant to be
    /// overridden by the consuming project (e.g. from a settings ScriptableObject). The audit reports the policy
    /// it ran against via <see cref="Describe"/>, so a report is self-documenting.
    /// </summary>
    public sealed class AssetPolicy
    {
        // ---- texture rules -------------------------------------------------------------------------

        /// <summary>Resolution cap: the importer's max size must not exceed this (manage size by resolution, not crunch).</summary>
        public int MaxTextureSize = 2048;

        /// <summary>Content textures must not be stored uncompressed.</summary>
        public bool RequireCompressed = true;

        /// <summary>
        /// Even if <see cref="RequireCompressed"/> is off, never allow an uncompressed RGBA32 texture — the
        /// single costliest common import mistake. Subsumed by <see cref="RequireCompressed"/> when that is on.
        /// </summary>
        public bool DisallowUncompressedRgba32 = true;

        /// <summary>Disallow crunch compression — the policy manages download size by resolution, not by crunching quality away.</summary>
        public bool DisallowCrunch = true;

        /// <summary>
        /// Non-sprite textures must be power-of-two or have NPOT scaling enabled, so they block-compress cleanly.
        /// Sprites are exempt: Unity keeps a sprite at its native size (NPOT scaling is locked off) and packs it into
        /// a power-of-two atlas, so sprite efficiency is the atlas builder's job, not this importer lever.
        /// </summary>
        public bool RequirePowerOfTwoOrNpotScale = true;

        /// <summary>Textures must not keep a CPU-side Read/Write copy unless a script samples their pixels (symmetric with the mesh rule).</summary>
        public bool DisallowTextureReadWrite = true;

        /// <summary>
        /// Sprites must not have mipmaps — dead weight (~33%) for 2D/UI drawn at native resolution. Scoped to sprites
        /// ONLY: 3D textures need mipmaps to avoid shimmering, so non-sprite textures are never flagged by this rule.
        /// </summary>
        public bool DisallowSpriteMipmaps = true;

        // ---- mesh rules ----------------------------------------------------------------------------

        /// <summary>Meshes must not keep a CPU-side Read/Write copy unless explicitly marked as requiring it.</summary>
        public bool DisallowMeshReadWrite = true;

        /// <summary>Require at least some mesh compression (set to <see cref="MeshCompressionLevel.Off"/> to disable this rule).</summary>
        public MeshCompressionLevel MinMeshCompression = MeshCompressionLevel.Off;

        /// <summary>The shipped sensible-mobile baseline (this is just the default-constructed policy).</summary>
        public static AssetPolicy MobileDefaults() => new AssetPolicy();

        /// <summary>A one-line, human-readable summary of the active rules — embedded in the audit report.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("maxTextureSize<=").Append(MaxTextureSize);
            if (RequireCompressed) sb.Append(", require-compressed");
            else if (DisallowUncompressedRgba32) sb.Append(", disallow-uncompressed-rgba32");
            if (DisallowCrunch) sb.Append(", disallow-crunch");
            if (RequirePowerOfTwoOrNpotScale) sb.Append(", pot-or-npot-scale");
            if (DisallowTextureReadWrite) sb.Append(", disallow-texture-readwrite");
            if (DisallowSpriteMipmaps) sb.Append(", disallow-sprite-mipmaps");
            if (DisallowMeshReadWrite) sb.Append(", disallow-mesh-readwrite");
            if (MinMeshCompression != MeshCompressionLevel.Off) sb.Append(", min-mesh-compression>=").Append(MinMeshCompression);
            return sb.ToString();
        }
    }
}
