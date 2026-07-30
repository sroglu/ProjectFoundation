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
        public int PageSize;
        public SpriteAtlas Atlas;
    }

    /// <summary>
    /// Builds a <see cref="SpriteAtlas"/> from a defined set of sprites (an <see cref="AssetGroup"/>, a folder, or
    /// explicit paths) using Unity's own atlas API. It stamps the packing layout and per-platform format/compression
    /// from an <see cref="AtlasBuildSettings"/>, sizes the page from the members (see
    /// <see cref="AtlasSizeCalculator"/>), and stamps a content hash over the input set so a changed set rebuilds to a
    /// changed atlas — content-addressed like the bundles. A built atlas is marked include-in-build so it renders in
    /// the editor; the <see cref="SpriteAtlasBuildPreprocessor"/> flips that off for a player build so its sprites
    /// ship once (through the bundle), never twice. Sprites resolve at runtime via the existing <c>main[sub]</c>
    /// sub-asset address, so NO new runtime path is added.
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        /// <summary>
        /// A per-atlas progress hook for the bulk regenerate paths. Called before each atlas with (0-based index,
        /// total, label); return <c>true</c> to cancel. Null = no reporting, never cancels.
        /// </summary>
        public delegate bool ProgressCallback(int index, int total, string label);

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

        /// <summary>Builds (or rebuilds) the atlas from the sprite paths with the shipped mobile atlas settings.</summary>
        public static SpriteAtlasBuildResult Build(string atlasPath, IReadOnlyList<string> spriteAssetPaths)
            => Build(atlasPath, spriteAssetPaths, AtlasBuildSettings.MobileDefaults());

        /// <summary>
        /// Builds (or rebuilds) the atlas at <paramref name="atlasPath"/> from the exact set of sprite paths, applies
        /// <paramref name="settings"/> (packing layout + per-platform format/compression + page size), packs it, and
        /// stamps the input content hash.
        /// </summary>
        public static SpriteAtlasBuildResult Build(
            string atlasPath, IReadOnlyList<string> spriteAssetPaths, AtlasBuildSettings settings)
        {
            if (string.IsNullOrEmpty(atlasPath)) throw new ArgumentException("Atlas path required.", nameof(atlasPath));
            if (spriteAssetPaths == null) throw new ArgumentNullException(nameof(spriteAssetPaths));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

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

            int pageSize = ApplyAtlasSettings(atlas, spriteAssetPaths, settings);

            // Include-in-build keeps the atlas rendering in the editor; the build preprocessor disables it for a
            // player build so its sprites ship once via the bundle, not doubly.
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);

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
                PageSize = pageSize,
                Atlas = atlas,
            };
        }

        /// <summary>Builds an atlas from every sprite found under <paramref name="folder"/>.</summary>
        public static SpriteAtlasBuildResult BuildFromFolder(string atlasPath, string folder)
            => Build(atlasPath, SpritePathsUnderFolder(folder), AtlasBuildSettings.MobileDefaults());

        /// <summary>Builds an atlas from every sprite under <paramref name="folder"/> with the given settings.</summary>
        public static SpriteAtlasBuildResult BuildFromFolder(string atlasPath, string folder, AtlasBuildSettings settings)
            => Build(atlasPath, SpritePathsUnderFolder(folder), settings);

        /// <summary>Builds an atlas from the sprites authored into <paramref name="group"/>.</summary>
        public static SpriteAtlasBuildResult BuildFromGroup(string atlasPath, AssetGroup group)
            => BuildFromGroup(atlasPath, group, AtlasBuildSettings.MobileDefaults());

        /// <summary>Builds an atlas from the sprites authored into <paramref name="group"/> with the given settings.</summary>
        public static SpriteAtlasBuildResult BuildFromGroup(string atlasPath, AssetGroup group, AtlasBuildSettings settings)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            return Build(atlasPath, GroupSpritePaths(group), settings);
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

        /// <summary>Every atlas path this builder manages (its importer carries a stored input content hash).</summary>
        public static List<string> ManagedAtlasPaths()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && !string.IsNullOrEmpty(importer.userData)) paths.Add(path);
            }
            return paths;
        }

        /// <summary>Recomputes a managed atlas's hash from its current members and rebuilds iff it changed. Returns whether it rebuilt.</summary>
        public static bool RegenerateIfOutOfDate(string atlasPath, AtlasBuildSettings settings)
        {
            var members = CurrentMemberPaths(atlasPath);
            if (IsUpToDate(atlasPath, members)) return false;
            Build(atlasPath, members, settings);
            return true;
        }

        /// <summary>
        /// Rebuilds every out-of-date managed atlas (all of them), reporting progress and honoring cancellation.
        /// Returns the number rebuilt. This is the bulk "regenerate all" API behind the menu command.
        /// </summary>
        public static int RegenerateAllManagedAtlases(AtlasBuildSettings settings, ProgressCallback progress = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var atlases = ManagedAtlasPaths();
            int rebuilt = 0;
            for (int i = 0; i < atlases.Count; i++)
            {
                if (progress != null && progress(i, atlases.Count, atlases[i])) break;
                if (RegenerateIfOutOfDate(atlases[i], settings)) rebuilt++;
            }
            return rebuilt;
        }

        /// <summary>The distinct sprite asset paths authored into <paramref name="group"/>.</summary>
        public static List<string> GroupSpritePaths(AssetGroup group)
        {
            var paths = new List<string>();
            if (group == null || group.Entries == null) return paths;
            foreach (var entry in group.Entries)
            {
                if (entry == null || entry.Asset == null) continue;
                string path = AssetDatabase.GetAssetPath(entry.Asset);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            return paths;
        }

        /// <summary>
        /// Applies the packing layout and per-platform format/compression, and (when auto-sizing) the page size
        /// computed from the members' rects. Returns the page size it stamped.
        /// </summary>
        private static int ApplyAtlasSettings(SpriteAtlas atlas, IReadOnlyList<string> spriteAssetPaths, AtlasBuildSettings settings)
        {
            var packing = atlas.GetPackingSettings();
            packing.padding = settings.Padding;
            packing.enableRotation = settings.EnableRotation;
            packing.enableTightPacking = settings.EnableTightPacking;
            atlas.SetPackingSettings(packing);

            int pageSize = settings.AutoSize
                ? ComputeAutoSize(spriteAssetPaths, settings.MaxSizeClamp)
                : settings.FixedMaxTextureSize;

            var def = atlas.GetPlatformSettings("DefaultTexturePlatform");
            def.textureCompression = settings.DefaultCompression;
            def.crunchedCompression = settings.DefaultCrunched;
            def.compressionQuality = settings.DefaultCompressionQuality;
            def.maxTextureSize = pageSize;
            atlas.SetPlatformSettings(def);

            var ios = atlas.GetPlatformSettings("iPhone");
            ios.overridden = true;
            ios.format = settings.IOSFormat;
            ios.compressionQuality = settings.IOSCompressionQuality;
            ios.maxTextureSize = pageSize;
            atlas.SetPlatformSettings(ios);

            var android = atlas.GetPlatformSettings("Android");
            android.overridden = true;
            android.format = settings.AndroidFormat;
            android.compressionQuality = settings.AndroidCompressionQuality;
            android.maxTextureSize = pageSize;
            atlas.SetPlatformSettings(android);

            return pageSize;
        }

        /// <summary>Sums the members' pixel area and finds the largest dimension, then delegates the page-size math to Core.</summary>
        private static int ComputeAutoSize(IReadOnlyList<string> spriteAssetPaths, int clamp)
        {
            double totalArea = 0;
            int largestDim = 0;
            foreach (string path in spriteAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(obj is Sprite sprite)) continue;
                    int w = Mathf.CeilToInt(sprite.rect.width);
                    int h = Mathf.CeilToInt(sprite.rect.height);
                    totalArea += (double)w * h;
                    if (w > largestDim) largestDim = w;
                    if (h > largestDim) largestDim = h;
                }
            }
            int size = AtlasSizeCalculator.ComputeMaxTextureSize(totalArea, largestDim, out bool clamped, clamp);
            if (clamped)
                Debug.LogWarning($"[AssetPipeline] Atlas members exceed the {clamp} page cap; clamped to {size} (sprites may not all fit).");
            return size;
        }

        /// <summary>The current member sprite paths of a built atlas (folder packables expanded to their sprites).</summary>
        private static List<string> CurrentMemberPaths(string atlasPath)
        {
            var paths = new List<string>();
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null) return paths;

            foreach (var packable in atlas.GetPackables())
            {
                if (packable == null) continue;
                string p = AssetDatabase.GetAssetPath(packable);
                if (string.IsNullOrEmpty(p)) continue;
                if (AssetDatabase.IsValidFolder(p)) paths.AddRange(SpritePathsUnderFolder(p));
                else paths.Add(p);
            }
            return paths;
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
