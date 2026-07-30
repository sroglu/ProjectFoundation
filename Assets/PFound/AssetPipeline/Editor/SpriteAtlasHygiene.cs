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
    /// Two atlas health checks that keep managed atlases lean and catch the classic double-load mistake:
    /// <list type="bullet">
    /// <item><b>Clear obsolete members</b> — drop packables that no longer resolve to a sprite the project references
    /// (deleted or orphaned), so a stale atlas does not carry dead pixels.</item>
    /// <item><b>List problematic sprites</b> — a sprite that is BOTH packed into an atlas AND directly referenced by
    /// something else loads twice (once from the atlas, once standalone); this lists them so an author can pick one.</item>
    /// </list>
    /// </summary>
    public static class SpriteAtlasHygiene
    {
        /// <summary>Removes packables that no longer resolve to a referenced sprite from every managed atlas. Returns members removed.</summary>
        public static int ClearObsoleteMembers()
        {
            return ClearObsoleteMembers(AssetReferenceGraph.Build());
        }

        /// <summary>Removes obsolete packables using a supplied reference <paramref name="graph"/> (the seam for tests).</summary>
        public static int ClearObsoleteMembers(IAssetReferenceGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            int removed = 0;
            bool anyChange = false;
            foreach (string atlasPath in SpriteAtlasBuilder.ManagedAtlasPaths())
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                var obsolete = new List<UnityEngine.Object>();
                foreach (var packable in atlas.GetPackables())
                {
                    if (packable == null) continue;
                    string p = AssetDatabase.GetAssetPath(packable);
                    // A member is obsolete if its source no longer exists, or nothing references it any more.
                    if (string.IsNullOrEmpty(p) || !graph.IsReferenced(p))
                        obsolete.Add(packable);
                }

                if (obsolete.Count > 0)
                {
                    atlas.Remove(obsolete.ToArray());
                    EditorUtility.SetDirty(atlas);
                    removed += obsolete.Count;
                    anyChange = true;
                    Debug.Log($"[AssetPipeline] Removed {obsolete.Count} obsolete member(s) from atlas '{atlasPath}'.");
                }
            }

            if (anyChange) AssetDatabase.SaveAssets();
            return removed;
        }

        /// <summary>
        /// The sprite paths that are BOTH atlas-packed AND reached by some non-atlas referrer — candidates for a
        /// double load. Computed from the project's direct dependency edges: a sprite is "directly referenced" when a
        /// non-SpriteAtlas asset depends on it.
        /// </summary>
        public static List<string> FindPackedAndDirectlyReferencedSprites()
        {
            var atlasPacked = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
                if (atlas == null) continue;
                foreach (var packable in atlas.GetPackables())
                {
                    if (packable == null) continue;
                    string p = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrEmpty(p)) continue;
                    if (AssetDatabase.IsValidFolder(p))
                        foreach (string sg in AssetDatabase.FindAssets("t:Sprite", new[] { p }))
                            atlasPacked.Add(AssetDatabase.GUIDToAssetPath(sg));
                    else
                        atlasPacked.Add(p);
                }
            }

            var directlyReferenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (string referrer in AssetDatabase.GetAllAssetPaths())
            {
                if (string.IsNullOrEmpty(referrer) || !referrer.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (referrer.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase)) continue; // atlas refs are the packing, not a "direct" ref
                if (AssetDatabase.IsValidFolder(referrer)) continue;

                foreach (string dep in AssetDatabase.GetDependencies(referrer, false))
                    if (atlasPacked.Contains(dep) && !string.Equals(dep, referrer, StringComparison.Ordinal))
                        directlyReferenced.Add(dep);
            }

            var result = new List<string>(directlyReferenced);
            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }
}
