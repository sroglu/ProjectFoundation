using PFound.AssetPipeline.Core;
using UnityEditor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Translates a Unity <see cref="ModelImporter"/> into engine-free <see cref="MeshImporterFacts"/> for the
    /// policy evaluator. Reads importer settings only — the <see cref="AssetOptimizer"/> is the separate, opt-in
    /// side that writes them back.
    /// </summary>
    public static class MeshImporterReader
    {
        /// <summary>Reads <paramref name="assetPath"/> as a model, or returns null when it has no <see cref="ModelImporter"/>.</summary>
        public static MeshImporterFacts Read(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter importer)) return null;

            return new MeshImporterFacts
            {
                AssetPath = assetPath,
                Source = MeshSource.ModelImporter,
                ReadWriteEnabled = importer.isReadable,
                MeshCompression = MapCompression(importer.meshCompression),
                OptimizeMesh = importer.meshOptimizationFlags != (MeshOptimizationFlags)0,
                // No per-entry "requires Read/Write" opt-out is wired yet; default to flagging Read/Write.
                ReadWriteRequired = false,
            };
        }

        private static MeshCompressionLevel MapCompression(ModelImporterMeshCompression c)
        {
            switch (c)
            {
                case ModelImporterMeshCompression.Low: return MeshCompressionLevel.Low;
                case ModelImporterMeshCompression.Medium: return MeshCompressionLevel.Medium;
                case ModelImporterMeshCompression.High: return MeshCompressionLevel.High;
                default: return MeshCompressionLevel.Off;
            }
        }
    }
}
