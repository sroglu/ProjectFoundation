using PFound.AssetPipeline.Core;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Reads the Read/Write flag off a <see cref="Mesh"/> asset directly — the case a <see cref="ModelImporter"/>
    /// cannot cover, because a standalone <c>.asset</c> mesh (created in code / by another tool, or a Mesh sub-asset)
    /// carries its readable flag on the mesh object, not on any importer. This closes the "R/W across ALL mesh
    /// sources" gap: <see cref="MeshImporterReader"/> handles model-backed meshes, this handles the rest. Paths that
    /// resolve to a <see cref="ModelImporter"/> are deliberately skipped here (the model reader owns them, so a mesh
    /// is never double-audited).
    /// </summary>
    public static class MeshAssetReader
    {
        /// <summary>
        /// Reads <paramref name="assetPath"/> as a Mesh asset, or returns null when it is model-backed (owned by the
        /// model reader) or contains no <see cref="Mesh"/>. Reports Read/Write enabled iff any Mesh at the path is
        /// readable. Mesh compression is a model-only setting, so it is reported as Off (not flagged) here.
        /// </summary>
        public static MeshImporterFacts Read(string assetPath)
        {
            // Model-backed meshes belong to MeshImporterReader; do not double-audit them.
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter) return null;

            bool anyReadable = false;
            bool anyMesh = false;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(obj is Mesh mesh)) continue;
                anyMesh = true;
                if (mesh.isReadable) { anyReadable = true; break; }
            }

            if (!anyMesh) return null;

            return new MeshImporterFacts
            {
                AssetPath = assetPath,
                Source = MeshSource.MeshAsset,
                ReadWriteEnabled = anyReadable,
                MeshCompression = MeshCompressionLevel.Off, // not an authorable setting on a raw Mesh asset
                OptimizeMesh = false,
                ReadWriteRequired = false,
            };
        }
    }
}
