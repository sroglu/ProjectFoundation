using UnityEditor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The knobs the <see cref="SpriteAtlasBuilder"/> stamps onto a built atlas: its packing layout, per-platform
    /// format/compression for the default target plus iOS and Android, and whether the page size is computed from
    /// the members (see <see cref="PFound.AssetPipeline.Core.AtlasSizeCalculator"/>) or left fixed. Plain object with
    /// sensible mobile defaults — a caller overrides a field or passes its own instance. Editor-only because the
    /// format identifiers are Unity importer enums; the page-size math itself lives engine-free in Core.
    /// </summary>
    public sealed class AtlasBuildSettings
    {
        // ---- packing layout ----------------------------------------------------------------------

        /// <summary>Transparent gutter (in pixels) between packed sprites; guards against bleeding when sampled.</summary>
        public int Padding = 8;

        /// <summary>Whether the packer may rotate sprites to fit tighter (off by default: predictable UVs).</summary>
        public bool EnableRotation = false;

        /// <summary>Whether the packer uses tight (mesh) packing rather than rectangle packing (off by default).</summary>
        public bool EnableTightPacking = false;

        // ---- page size ---------------------------------------------------------------------------

        /// <summary>When true, the page size is computed from the members' area and largest dimension; else <see cref="FixedMaxTextureSize"/> is used.</summary>
        public bool AutoSize = true;

        /// <summary>The page size used when <see cref="AutoSize"/> is off.</summary>
        public int FixedMaxTextureSize = 2048;

        /// <summary>The hard cap the computed size is clamped to (Unity's atlas ceiling).</summary>
        public int MaxSizeClamp = PFound.AssetPipeline.Core.AtlasSizeCalculator.MaxAtlasSize;

        // ---- per-platform format / compression ---------------------------------------------------

        /// <summary>Default-target compression (applies to platforms with no override).</summary>
        public TextureImporterCompression DefaultCompression = TextureImporterCompression.CompressedHQ;

        /// <summary>Default-target compression quality (0..100).</summary>
        public int DefaultCompressionQuality = 50;

        /// <summary>Default-target crunch on top of the format (off by default; size is managed by page resolution).</summary>
        public bool DefaultCrunched = false;

        /// <summary>iOS explicit format override (a block-compressed ASTC variant by default).</summary>
        public TextureImporterFormat IOSFormat = TextureImporterFormat.ASTC_4x4;

        /// <summary>iOS compression quality (0..100).</summary>
        public int IOSCompressionQuality = 50;

        /// <summary>Android explicit format override (ETC2 with alpha by default).</summary>
        public TextureImporterFormat AndroidFormat = TextureImporterFormat.ETC2_RGBA8;

        /// <summary>Android compression quality (0..100).</summary>
        public int AndroidCompressionQuality = 50;

        /// <summary>The shipped mobile baseline (the default-constructed settings).</summary>
        public static AtlasBuildSettings MobileDefaults() => new AtlasBuildSettings();
    }
}
