namespace PFound.AssetPipeline.Core
{
    /// <summary>The specific rule a <see cref="PolicyViolation"/> breaks. Stable codes so reports and tests key off them.</summary>
    public enum ViolationCode
    {
        /// <summary>A content texture is stored uncompressed (RequireCompressed).</summary>
        TextureUncompressed = 0,

        /// <summary>A content texture is uncompressed AND in RGBA32 specifically (the costliest common case).</summary>
        TextureUncompressedRgba32 = 1,

        /// <summary>A texture uses crunch compression while the policy manages size by resolution instead.</summary>
        TextureCrunched = 2,

        /// <summary>The importer's max size cap is above the policy's resolution cap.</summary>
        TextureExceedsMaxSize = 3,

        /// <summary>A non-sprite texture has non-power-of-two dimensions with NPOT scaling off (can't block-compress cleanly).</summary>
        TextureNotPowerOfTwoNoNpotScale = 4,

        /// <summary>A mesh has Read/Write enabled without being marked as requiring it.</summary>
        MeshReadWriteEnabled = 5,

        /// <summary>A mesh is stored with no mesh compression while the policy requires some.</summary>
        MeshUncompressed = 6,

        /// <summary>A texture has Read/Write enabled without being marked as requiring it (a CPU copy alongside the GPU one).</summary>
        TextureReadWriteEnabled = 7,

        /// <summary>A sprite has mipmaps enabled (dead weight for 2D/UI drawn at native resolution).</summary>
        SpriteMipmapsEnabled = 8,
    }

    /// <summary>
    /// One policy breach found by the evaluator: the offending asset, which rule, an optional build target the
    /// breach is scoped to (empty = the default platform), and a human-readable detail (actual-vs-expected). Pure
    /// value type — the audit pass collects these; the apply pass fixes them. A violation is a finding, never a
    /// mutation.
    /// </summary>
    public readonly struct PolicyViolation
    {
        public readonly string AssetPath;
        public readonly ViolationCode Code;
        public readonly string Detail;

        /// <summary>
        /// The build target this breach is scoped to (e.g. "iOS", "Android"); empty for the default-platform
        /// settings. Lets the same rule report once per overridden platform and the apply pass fix the right target.
        /// </summary>
        public readonly string Platform;

        public PolicyViolation(string assetPath, ViolationCode code, string detail)
            : this(assetPath, code, detail, null) { }

        public PolicyViolation(string assetPath, ViolationCode code, string detail, string platform)
        {
            AssetPath = assetPath;
            Code = code;
            Detail = detail;
            Platform = string.IsNullOrEmpty(platform) ? "" : platform;
        }

        public override string ToString() =>
            string.IsNullOrEmpty(Platform)
                ? $"{Code} [{AssetPath}] — {Detail}"
                : $"{Code} [{AssetPath}] ({Platform}) — {Detail}";
    }
}
