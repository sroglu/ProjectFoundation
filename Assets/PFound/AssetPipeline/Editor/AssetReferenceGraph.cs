using System;
using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// AssetDatabase-backed <see cref="IAssetReferenceGraph"/>: a one-shot snapshot of "what is reached by
    /// something" and "what is packed into a sprite atlas", built once and handed to the pure evaluator so the
    /// audit can skip unreferenced assets and stop applying per-texture format rules to atlas members.
    ///
    /// <para><b>Referenced set</b> = the reverse dependency closure: every asset that appears as a dependency of any
    /// other asset under <c>Assets/</c> (scripts and the referrers' own folders excluded). If nothing points at an
    /// asset, it does not ship, so it is treated as unreferenced.</para>
    ///
    /// <para><b>Atlas-packed set</b> = every texture/sprite path reachable from a <see cref="SpriteAtlas"/>'s
    /// packables (folders expanded to the sprites under them). Membership is by the source texture's asset path,
    /// which is exactly what the audit scans.</para>
    /// </summary>
    public sealed class AssetReferenceGraph : IAssetReferenceGraph
    {
        private readonly HashSet<string> _referenced;
        private readonly HashSet<string> _atlasPacked;

        private AssetReferenceGraph(HashSet<string> referenced, HashSet<string> atlasPacked)
        {
            _referenced = referenced;
            _atlasPacked = atlasPacked;
        }

        public bool IsReferenced(string assetPath) => !string.IsNullOrEmpty(assetPath) && _referenced.Contains(assetPath);
        public bool IsAtlasPacked(string assetPath) => !string.IsNullOrEmpty(assetPath) && _atlasPacked.Contains(assetPath);

        /// <summary>Builds the snapshot by scanning the whole project's dependency edges and every sprite atlas.</summary>
        public static AssetReferenceGraph Build() => Build(null);

        /// <summary>
        /// Builds the snapshot, additionally treating <paramref name="roots"/> as referenced. A group's authored
        /// entries ship because the group places them, not because another asset depends on them, so an audit over a
        /// group must seed its entry paths as roots — otherwise a legitimately shipped-but-otherwise-unreferenced
        /// asset would be skipped as orphan.
        /// </summary>
        public static AssetReferenceGraph Build(IEnumerable<string> roots)
        {
            var referenced = BuildReferencedSet();
            if (roots != null)
                foreach (string root in roots)
                    if (!string.IsNullOrEmpty(root)) referenced.Add(root);
            return new AssetReferenceGraph(referenced, BuildAtlasPackedSet());
        }

        private static HashSet<string> BuildReferencedSet()
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

                // Direct (non-recursive) dependencies: each is reached by `path`, so each is referenced.
                foreach (string dep in AssetDatabase.GetDependencies(path, false))
                    if (!string.Equals(dep, path, StringComparison.Ordinal))
                        referenced.Add(dep);
            }
            return referenced;
        }

        private static HashSet<string> BuildAtlasPackedSet()
        {
            var packed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                foreach (var packable in atlas.GetPackables())
                {
                    if (packable == null) continue;
                    string p = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrEmpty(p)) continue;

                    if (AssetDatabase.IsValidFolder(p))
                    {
                        // A folder packable pulls in every sprite under it.
                        foreach (string spriteGuid in AssetDatabase.FindAssets("t:Sprite", new[] { p }))
                            packed.Add(AssetDatabase.GUIDToAssetPath(spriteGuid));
                    }
                    else
                    {
                        packed.Add(p);
                    }
                }
            }
            return packed;
        }
    }
}
