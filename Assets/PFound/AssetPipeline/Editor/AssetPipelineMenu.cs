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
            string reportPath = AssetAuditReportExporter.Write(result.Policy, result.Duplicates, OutputDir());

            string dup = result.DuplicateCount > 0
                ? $"\n⚠ {result.DuplicateCount} duplicate dependenc(ies) copied across bundles — see the report."
                : "";

            if (result.IsClean)
                Debug.Log($"[AssetPipeline] Audit clean — {result.Policy.AssetsScanned} asset(s), 0 violations, 0 duplicates.\nReport: {reportPath}");
            else
                Debug.LogWarning($"[AssetPipeline] Audit found {result.Policy.ViolationCount} violation(s) across {result.Policy.AssetsScanned} asset(s).{dup}\n{result.Policy}\nReport: {reportPath}");
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

            int changed = AssetOptimizer.Apply(report, policy);
            Debug.Log($"[AssetPipeline] Optimized {changed} asset(s) for {report.ViolationCount} violation(s). Re-run the audit to confirm.");
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
            Debug.Log($"[AssetPipeline] Built atlas {result.AtlasPath} from {result.MemberCount} sprite(s) [hash {result.ContentHash}]. " +
                      "Add it to an AssetGroup to ship it as a bundle.");
        }

        private static string OutputDir() =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);
    }
}
