using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The full build-prep audit, three distinct findings kept apart because they answer different questions:
    /// the per-asset policy <see cref="AssetAuditReport"/> ("is this asset imported well?"); the cross-bundle
    /// <see cref="DuplicateDependency"/> findings ("is this asset wastefully copied into several bundles?", reused
    /// from ContentDelivery); and the content-identical <see cref="DuplicateTextureGroup"/> findings ("are these
    /// distinct files the same image?").
    /// </summary>
    public sealed class AssetAuditResult
    {
        public AssetAuditReport Policy;
        public IReadOnlyList<DuplicateDependency> Duplicates;
        public IReadOnlyList<DuplicateTextureGroup> DuplicateTextures;

        public AssetAuditResult(
            AssetAuditReport policy,
            IReadOnlyList<DuplicateDependency> duplicates,
            IReadOnlyList<DuplicateTextureGroup> duplicateTextures = null)
        {
            Policy = policy;
            Duplicates = duplicates;
            DuplicateTextures = duplicateTextures ?? System.Array.Empty<DuplicateTextureGroup>();
        }

        public int DuplicateCount => Duplicates.Count;
        public int DuplicateTextureCount => DuplicateTextures.Count;
        public bool IsClean => Policy.IsClean && Duplicates.Count == 0 && DuplicateTextures.Count == 0;
    }
}
