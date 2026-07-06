namespace PFound.AssetPipeline.Core
{
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

        /// <summary>True when BOTH source dimensions are an exact power of two.</summary>
        public bool IsPowerOfTwo => IsPow2(Width) && IsPow2(Height);

        private static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;
    }

    /// <summary>
    /// The importer settings of one model/mesh, as plain data — read off a <c>ModelImporter</c> by the editor
    /// reader, then handed to the policy evaluator. Engine-free, same rationale as <see cref="TextureImporterFacts"/>.
    /// </summary>
    public sealed class MeshImporterFacts
    {
        public string AssetPath;

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
