using System.Collections.Generic;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// The compression-relevant settings of one texture for a single build target (iOS/Android/…), as plain data.
    /// Read off a <c>TextureImporter</c>'s per-platform override by the editor reader; the evaluator applies the
    /// same format/crunch/size rules to each OVERRIDDEN platform as it does to the default one. Engine-free.
    /// </summary>
    public sealed class PlatformTextureFacts
    {
        /// <summary>The build target name (e.g. "iOS", "Android"); reported on any violation this platform raises.</summary>
        public string Platform;

        /// <summary>True when the importer actually overrides the default settings for this platform (else it inherits them).</summary>
        public bool Overridden;

        public TextureCompressionLevel Compression;
        public bool Crunched;
        public int MaxTextureSize;

        /// <summary>The effective format name for this platform (e.g. "ASTC_4x4", "RGBA32").</summary>
        public string FormatName;

        /// <summary>0..100 compression quality; carried so the apply pass can restore a sane value when it compresses.</summary>
        public int CompressionQuality;
    }

    /// <summary>
    /// The importer settings of one texture, as plain data — read off a <c>TextureImporter</c> by the editor
    /// reader, then handed to the policy evaluator. Engine-free so the policy can be unit-tested with synthetic
    /// facts and no Unity. <see cref="Width"/>/<see cref="Height"/> are the source image dimensions (used for the
    /// power-of-two check); <see cref="MaxTextureSize"/> is the importer's resolution cap (the size lever the
    /// policy prefers over crunch).
    /// </summary>
    public sealed class TextureImporterFacts
    {
        public string AssetPath;
        public int Width;
        public int Height;

        /// <summary>Effective compression level for the inspected platform.</summary>
        public TextureCompressionLevel Compression;

        /// <summary>Crunch (DXT/ETC crunch) on top of the format — trades quality for a smaller download.</summary>
        public bool Crunched;

        /// <summary>The importer's max output dimension cap (the resolution lever).</summary>
        public int MaxTextureSize;

        /// <summary>What the importer does with non-power-of-two source dimensions.</summary>
        public NpotScale NpotScale;

        /// <summary>True when imported as a Sprite (an atlas-bound candidate the POT rule cares about).</summary>
        public bool IsSprite;

        public bool MipmapsEnabled;

        /// <summary>Read/Write enabled — keeps a CPU-side (RAM) copy alongside the GPU one; waste unless a script reads pixels.</summary>
        public bool ReadWriteEnabled;

        /// <summary>
        /// Marks a texture that legitimately needs Read/Write (a script samples its pixels at runtime), so the policy
        /// skips the Read/Write violation. Mirrors the mesh fact: the editor would set this from a per-entry opt-out;
        /// the default (false) treats Read/Write as a violation.
        /// </summary>
        public bool ReadWriteRequired;

        /// <summary>Effective texture format name (e.g. "RGBA32"); lets the policy flag the uncompressed-RGBA32 case by name.</summary>
        public string FormatName;

        /// <summary>
        /// The per-target overrides (iOS/Android/…) the importer carries. The evaluator applies the format/crunch/size
        /// rules to each OVERRIDDEN entry, so a platform that silently ships uncompressed while the default is
        /// compressed is still caught. Null/empty = default-platform settings only.
        /// </summary>
        public List<PlatformTextureFacts> PlatformOverrides;

        /// <summary>True when BOTH source dimensions are an exact power of two.</summary>
        public bool IsPowerOfTwo => IsPow2(Width) && IsPow2(Height);

        private static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;
    }

    /// <summary>Where a mesh's Read/Write flag lives — which decides how the apply pass turns it off.</summary>
    public enum MeshSource
    {
        /// <summary>A model asset (FBX/OBJ/…) whose Read/Write is a <c>ModelImporter</c> setting.</summary>
        ModelImporter = 0,

        /// <summary>A standalone <c>Mesh</c> asset (or a Mesh sub-asset) whose readable flag lives on the mesh object itself.</summary>
        MeshAsset = 1,
    }

    /// <summary>
    /// The settings of one mesh, as plain data — read either off a <c>ModelImporter</c> or directly off a
    /// <c>Mesh</c> asset by the editor reader, then handed to the policy evaluator. Engine-free, same rationale as
    /// <see cref="TextureImporterFacts"/>. <see cref="Source"/> records which reader produced it so the apply pass
    /// disables Read/Write on the right handle (an importer knob vs. the mesh's own readable flag).
    /// </summary>
    public sealed class MeshImporterFacts
    {
        public string AssetPath;

        /// <summary>Where the Read/Write flag lives (importer vs. the mesh object). The R/W rule is identical for both.</summary>
        public MeshSource Source;

        /// <summary>Read/Write enabled — keeps a CPU-side copy of the mesh (double memory); should be off unless required.</summary>
        public bool ReadWriteEnabled;

        public MeshCompressionLevel MeshCompression;

        /// <summary>Whether the importer reorders vertices/indices for GPU cache efficiency.</summary>
        public bool OptimizeMesh;

        /// <summary>
        /// Marks a mesh that legitimately needs Read/Write (runtime mesh edits, NavMesh/collider baking, etc.), so
        /// the policy skips the Read/Write violation for it. The editor sets this from a per-entry opt-out; the
        /// default (false) treats Read/Write as a violation.
        /// </summary>
        public bool ReadWriteRequired;
    }
}
