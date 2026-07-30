namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// Engine-free mirror of a texture importer's compression level (Unity's TextureImporterCompression).
    /// Kept here so the policy layer evaluates pure data with no UnityEditor dependency; the editor reader
    /// translates the engine enum into this one.
    /// </summary>
    public enum TextureCompressionLevel
    {
        /// <summary>Stored uncompressed (e.g. RGBA32) — large on disk and in memory.</summary>
        Uncompressed = 0,

        /// <summary>Platform default compressed format (normal quality).</summary>
        Normal = 1,

        /// <summary>Compressed, lower quality / smaller.</summary>
        LowQuality = 2,

        /// <summary>Compressed, higher quality / larger.</summary>
        HighQuality = 3,
    }

    /// <summary>
    /// Engine-free mirror of a texture's non-power-of-two scaling rule (Unity's TextureImporterNPOTScale):
    /// what the importer does when the source dimensions are not a power of two.
    /// </summary>
    public enum NpotScale
    {
        /// <summary>Left as-is — a non-power-of-two texture stays NPOT (cannot go into a compressed atlas cleanly).</summary>
        None = 0,

        /// <summary>Scaled to the nearest power of two.</summary>
        ToNearest = 1,

        /// <summary>Scaled up to the next larger power of two.</summary>
        ToLarger = 2,

        /// <summary>Scaled down to the next smaller power of two.</summary>
        ToSmaller = 3,
    }

    /// <summary>
    /// Engine-free mirror of a model importer's mesh compression level (Unity's ModelImporterMeshCompression).
    /// </summary>
    public enum MeshCompressionLevel
    {
        /// <summary>No compression — vertex data stored at full precision.</summary>
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }
}
