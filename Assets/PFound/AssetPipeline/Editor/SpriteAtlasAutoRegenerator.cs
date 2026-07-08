using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Keeps builder-managed sprite atlases current: when a texture/sprite that is a member of a managed atlas is
    /// (re)imported, this postprocessor re-runs the atlas build so a member's content change is picked up
    /// automatically — using the SAME per-member content hash the builder stamps, so an atlas whose members are
    /// unchanged is never needlessly repacked. Managed atlases are recognized by their stored input hash
    /// (importer userData). Skips batch mode / player builds so a headless build or CI run is not perturbed.
    /// </summary>
    public sealed class SpriteAtlasAutoRegenerator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Application.isBatchMode || BuildPipeline.isBuildingPlayer) return;
            if (importedAssets == null || importedAssets.Length == 0) return;

            var imported = new HashSet<string>(importedAssets);

            // The managed atlases whose current membership includes any just-imported asset AND whose stamped hash
            // no longer matches the current member content.
            var stale = new List<string>();
            foreach (string atlasPath in SpriteAtlasBuilder.ManagedAtlasPaths())
            {
                if (imported.Contains(atlasPath)) continue; // building the atlas itself triggered us; not a member change

                var members = MemberPaths(atlasPath);
                bool touchesMember = false;
                foreach (string m in members)
                    if (imported.Contains(m)) { touchesMember = true; break; }

                if (touchesMember && !SpriteAtlasBuilder.IsUpToDate(atlasPath, members))
                    stale.Add(atlasPath);
            }

            if (stale.Count == 0) return;

            var settings = AtlasBuildSettings.MobileDefaults();
            try
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "AssetPipeline",
                            $"Regenerating sprite atlas '{stale[i]}' ({i + 1}/{stale.Count})",
                            (float)(i + 1) / stale.Count))
                    {
                        Debug.LogWarning("[AssetPipeline] Atlas auto-regeneration canceled. Run " +
                                         "'PFound/Asset Pipeline/Regenerate All Sprite Atlases' to finish.");
                        break;
                    }
                    SpriteAtlasBuilder.RegenerateIfOutOfDate(stale[i], settings);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<string> MemberPaths(string atlasPath)
        {
            var paths = new List<string>();
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null) return paths;

            foreach (var packable in atlas.GetPackables())
            {
                if (packable == null) continue;
                string p = AssetDatabase.GetAssetPath(packable);
                if (string.IsNullOrEmpty(p)) continue;
                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { p }))
                        paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
                else
                {
                    paths.Add(p);
                }
            }
            return paths;
        }
    }
}
