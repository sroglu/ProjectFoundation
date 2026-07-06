using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// The full build-prep audit: the per-asset policy <see cref="AssetAuditReport"/> plus the cross-bundle
    /// <see cref="DuplicateDependency"/> findings from the (reused) ContentDelivery duplicate analyzer. The two
    /// are kept distinct because they answer different questions — "is this asset imported well?" vs. "is this
    /// asset wastefully copied into several bundles?" — and the duplicate finding is a property of the bundle
    /// layout, not of any single importer.
    /// </summary>
    public sealed class AssetAuditResult
    {
        public AssetAuditReport Policy;
        public IReadOnlyList<DuplicateDependency> Duplicates;

        public AssetAuditResult(AssetAuditReport policy, IReadOnlyList<DuplicateDependency> duplicates)
        {
            Policy = policy;
            Duplicates = duplicates;
        }

        public int DuplicateCount => Duplicates.Count;
        public bool IsClean => Policy.IsClean && Duplicates.Count == 0;
    }
}
