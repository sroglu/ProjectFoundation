using System.IO;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Editor entry points for the build-prep asset pipeline: an audit that only reports, and an explicit apply that
    /// fixes what the audit flagged. Both run over the authoring <see cref="AssetGroup"/>s (the same source the
    /// content build reads). The policy is the shipped mobile baseline; a project overrides it in code.
    /// </summary>
    public static class AssetPipelineMenu
    {
        private const string OutputFolderName = "AssetAudit";

        [MenuItem("PFound/Asset Pipeline/Audit Assets (All Groups)")]
        public static void AuditAllGroups()
        {
            var groups = ContentDeliveryMenu.LoadAllGroups();
            var result = AssetAuditor.AuditAll(groups, AssetPolicy.MobileDefaults());
            string reportPath = AssetAuditReportExporter.Write(result.Policy, result.Duplicates, OutputDir(), result.DuplicateTextures);

            string dup = result.DuplicateCount > 0
                ? $"\n⚠ {result.DuplicateCount} duplicate dependenc(ies) copied across bundles — see the report."
                : "";
            string dupTex = result.DuplicateTextureCount > 0
                ? $"\n⚠ {result.DuplicateTextureCount} content-identical texture group(s) (same image, different files) — see the report."
                : "";

            if (result.IsClean)
                Debug.Log($"[AssetPipeline] Audit clean — {result.Policy.AssetsScanned} asset(s), 0 violations, 0 duplicates.\nReport: {reportPath}");
            else
                Debug.LogWarning($"[AssetPipeline] Audit found {result.Policy.ViolationCount} violation(s) across {result.Policy.AssetsScanned} asset(s).{dup}{dupTex}\n{result.Policy}\nReport: {reportPath}");
        }

        [MenuItem("PFound/Asset Pipeline/Optimize Assets (Apply Fixes — All Groups)")]
        public static void OptimizeAllGroups()
        {
            var policy = AssetPolicy.MobileDefaults();
            var report = AssetAuditor.Audit(ContentDeliveryMenu.LoadAllGroups(), policy);
            if (report.IsClean)
            {
                Debug.Log($"[AssetPipeline] Nothing to optimize — audit clean ({report.AssetsScanned} asset(s)).");
                return;
            }

            if (!EditorUtility.DisplayDialog("Optimize Assets",
                    $"Apply importer fixes for {report.ViolationCount} violation(s) across the audited assets? " +
                    "This rewrites importer settings and reimports.",
                    "Apply", "Cancel"))
                return;

            try
            {
                int changed = AssetOptimizer.Apply(report, policy, (i, total, path) =>
                    EditorUtility.DisplayCancelableProgressBar("Optimize Assets", $"Fixing '{path}' ({i + 1}/{total})",
                        total == 0 ? 1f : (float)(i + 1) / total));
                Debug.Log($"[AssetPipeline] Optimized {changed} asset(s) for {report.ViolationCount} violation(s). Re-run the audit to confirm.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("PFound/Asset Pipeline/Build Sprite Atlas (Selected Asset Group)")]
        public static void BuildAtlasFromSelectedGroup()
        {
            var group = Selection.activeObject as AssetGroup;
            if (group == null)
            {
                Debug.LogWarning("[AssetPipeline] Select an AssetGroup in the Project window first.");
                return;
            }

            string groupDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(group));
            string atlasPath = $"{groupDir}/{group.ResolveBundleName()}_atlas.spriteatlas";
            var result = SpriteAtlasBuilder.BuildFromGroup(atlasPath, group);
            Debug.Log($"[AssetPipeline] Built atlas {result.AtlasPath} from {result.MemberCount} sprite(s) at page {result.PageSize} " +
                      $"[hash {result.ContentHash}]. Add it to an AssetGroup to ship it as a bundle.");
        }

        [MenuItem("PFound/Asset Pipeline/Regenerate All Sprite Atlases")]
        public static void RegenerateAllAtlases()
        {
            int rebuilt;
            try
            {
                rebuilt = SpriteAtlasBuilder.RegenerateAllManagedAtlases(
                    AtlasBuildSettings.MobileDefaults(),
                    (i, total, label) => EditorUtility.DisplayCancelableProgressBar(
                        "Regenerate Sprite Atlases", $"'{label}' ({i + 1}/{total})",
                        total == 0 ? 1f : (float)(i + 1) / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[AssetPipeline] Regenerated {rebuilt} out-of-date sprite atlas(es).");
        }

        [MenuItem("PFound/Asset Pipeline/Clear Obsolete Atlas Sprites")]
        public static void ClearObsoleteAtlasSprites()
        {
            int removed = SpriteAtlasHygiene.ClearObsoleteMembers();
            Debug.Log(removed > 0
                ? $"[AssetPipeline] Removed {removed} obsolete member(s) from managed sprite atlases."
                : "[AssetPipeline] No obsolete members in managed sprite atlases.");
        }

        [MenuItem("PFound/Asset Pipeline/List Packed + Directly-Referenced Sprites")]
        public static void ListPackedAndDirectlyReferencedSprites()
        {
            var sprites = SpriteAtlasHygiene.FindPackedAndDirectlyReferencedSprites();
            if (sprites.Count == 0)
            {
                Debug.Log("[AssetPipeline] No sprites are both atlas-packed and directly referenced.");
                return;
            }
            foreach (string path in sprites)
                Debug.LogWarning($"[AssetPipeline] Sprite is BOTH atlas-packed and directly referenced (double load): {path}");
            Debug.Log($"[AssetPipeline] {sprites.Count} sprite(s) both atlas-packed and directly referenced — see warnings above.");
        }

        [MenuItem("PFound/Asset Pipeline/Find Content-Identical Duplicate Textures")]
        public static void FindDuplicateTextures()
        {
            var groups = TextureContentDuplicateAnalyzer.AnalyzeProject();
            if (groups.Count == 0)
            {
                Debug.Log("[AssetPipeline] No content-identical duplicate textures in the project.");
                return;
            }
            foreach (var g in groups)
                Debug.LogWarning($"[AssetPipeline] {g.Paths.Length} content-identical textures (hash {g.ContentHash}):\n  {string.Join("\n  ", g.Paths)}");
            Debug.Log($"[AssetPipeline] Found {groups.Count} content-identical texture group(s) — see warnings above.");
        }

        private static string OutputDir() =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);
    }
}
