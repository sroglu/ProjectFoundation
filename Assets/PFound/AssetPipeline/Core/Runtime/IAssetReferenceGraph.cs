namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// The project's reference relationships, as the pure evaluator needs them: is an asset reached by anything,
    /// and is it packed into a sprite atlas. Injected (never built) by the evaluator so the decision layer stays
    /// engine-free and unit-testable with a hand-authored fake graph. The editor supplies a real implementation
    /// backed by the AssetDatabase; passing <c>null</c> disables gating (every asset is evaluated as authored).
    /// </summary>
    public interface IAssetReferenceGraph
    {
        /// <summary>
        /// True when <paramref name="assetPath"/> is reached by at least one other asset (a scene, prefab, group
        /// entry, atlas, material…). An unreferenced asset does not ship, so the audit skips it rather than flagging
        /// import settings that never affect a build.
        /// </summary>
        bool IsReferenced(string assetPath);

        /// <summary>
        /// True when <paramref name="assetPath"/> is a member of a sprite atlas. An atlas member's format, size and
        /// compression are governed by the atlas, not by its own importer, so the per-texture format rules must not
        /// be applied to it.
        /// </summary>
        bool IsAtlasPacked(string assetPath);
    }
}
