using System;
using System.Collections.Generic;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>The outcome of a sprite-atlas build: where it landed, its input content hash, and how many sprites went in.</summary>
    public sealed class SpriteAtlasBuildResult
    {
        public string AtlasPath;
        public string ContentHash;
        public int MemberCount;
        public SpriteAtlas Atlas;
    }

    /// <summary>
    /// Builds a <see cref="SpriteAtlas"/> from a defined set of sprites (an <see cref="AssetGroup"/>, a folder, or
    /// explicit paths) using Unity's own atlas API, and stamps a content hash over the input set so a changed set
    /// rebuilds to a changed atlas — content-addressed like the bundles. Its job ends at producing the atlas and
    /// (optionally) feeding it back into authoring as an <see cref="AssetGroup"/> entry; the runtime already loads
    /// a sprite out of an atlas via the existing <c>main[sub]</c> sub-asset address, so NO new runtime path is added.
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        /// <summary>
        /// The content hash of the input set: each sprite's path paired with its source <see cref="Hash128"/> from
        /// the AssetDatabase, hashed order-independently. Changes iff the membership or any member's content changes.
        /// </summary>
        public static string ComputeContentHash(IEnumerable<string> spriteAssetPaths)
        {
            var identities = new List<string>();
            foreach (string path in spriteAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                Hash128 sourceHash = AssetDatabase.GetAssetDependencyHash(path);
                identities.Add(path + "|" + sourceHash);
            }
            return AtlasInputHasher.Compute(identities);
        }

        /// <summary>Builds (or rebuilds) the atlas at <paramref name="atlasPath"/> from the exact set of sprite paths, then packs it.</summary>
        public static SpriteAtlasBuildResult Build(string atlasPath, IReadOnlyList<string> spriteAssetPaths)
        {
            if (string.IsNullOrEmpty(atlasPath)) throw new ArgumentException("Atlas path required.", nameof(atlasPath));
            if (spriteAssetPaths == null) throw new ArgumentNullException(nameof(spriteAssetPaths));

            var packables = new List<UnityEngine.Object>(spriteAssetPaths.Count);
            foreach (string path in spriteAssetPaths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) packables.Add(asset);
            }

            string hash = ComputeContentHash(spriteAssetPaths);

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }
            else
            {
                // Rebuild the membership exactly: drop the old packables before adding the current set.
                var existing = atlas.GetPackables();
                if (existing != null && existing.Length > 0) atlas.Remove(existing);
            }

            atlas.Add(packables.ToArray());
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

            // Persist the input hash in the importer's userData (lives in the .meta, no extra asset file).
            var importer = AssetImporter.GetAtPath(atlasPath);
            importer.userData = hash;
            AssetDatabase.WriteImportSettingsIfDirty(atlasPath);

            return new SpriteAtlasBuildResult
            {
                AtlasPath = atlasPath,
                ContentHash = hash,
                MemberCount = packables.Count,
                Atlas = atlas,
            };
        }

        /// <summary>Builds an atlas from every sprite found under <paramref name="folder"/>.</summary>
        public static SpriteAtlasBuildResult BuildFromFolder(string atlasPath, string folder)
            => Build(atlasPath, SpritePathsUnderFolder(folder));

        /// <summary>Builds an atlas from the sprites authored into <paramref name="group"/>.</summary>
        public static SpriteAtlasBuildResult BuildFromGroup(string atlasPath, AssetGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            var paths = new List<string>();
            foreach (var entry in group.Entries)
            {
                if (entry == null || entry.Asset == null) continue;
                string path = AssetDatabase.GetAssetPath(entry.Asset);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            return Build(atlasPath, paths);
        }

        /// <summary>The hash recorded on the atlas at its last build (or empty if never built by this builder).</summary>
        public static string GetStoredContentHash(string atlasPath) => AssetImporter.GetAtPath(atlasPath).userData;

        /// <summary>True when the atlas was last built from exactly this input set — lets a caller skip an unchanged rebuild.</summary>
        public static bool IsUpToDate(string atlasPath, IReadOnlyList<string> spriteAssetPaths)
            => GetStoredContentHash(atlasPath) == ComputeContentHash(spriteAssetPaths);

        /// <summary>
        /// Feeds a built atlas back into authoring: appends an <see cref="AssetEntry"/> for it to <paramref name="group"/>
        /// so it ships as a bundle. Sprites inside resolve at runtime via the existing <c>main[sub]</c> address — no new path.
        /// </summary>
        public static AssetEntry AddAtlasToGroup(AssetGroup group, string atlasPath, string address)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            var entry = new AssetEntry { Asset = atlas, Address = address };
            group.Entries.Add(entry);
            EditorUtility.SetDirty(group);
            return entry;
        }

        private static List<string> SpritePathsUnderFolder(string folder)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return paths;
        }
    }
}
