using System;
using System.Collections.Generic;
using System.IO;
using PFound.AssetPipeline.Core;
using PFound.ContentDelivery.Editor;
using UnityEngine;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Serializes an <see cref="AssetAuditReport"/> (policy violations) plus any <see cref="DuplicateDependency"/>
    /// findings to JSON, so a build-prep audit is auditable from CI or by hand — the same shape as ContentDelivery's
    /// build-report exporter. The report records the policy it ran against, the scan count, per-rule roll-ups, every
    /// violation, and the cross-bundle duplicate dependencies. Editor-only.
    /// </summary>
    public static class AssetAuditReportExporter
    {
        public const string ReportFileName = "asset-audit.json";

        [Serializable]
        private struct ViolationDto { public string asset; public string code; public string detail; }

        [Serializable]
        private struct CountDto { public string code; public int count; }

        [Serializable]
        private struct DuplicateDto { public string asset; public string[] bundles; }

        [Serializable]
        private struct ReportDto
        {
            public string policy;
            public int assetsScanned;
            public int violationCount;
            public CountDto[] byCode;
            public ViolationDto[] violations;
            public int duplicateCount;
            public DuplicateDto[] duplicateDependencies;
        }

        /// <summary>Serializes the policy report and the (optional) duplicate-dependency findings to JSON.</summary>
        public static string ToJson(
            AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates = null, bool prettyPrint = true)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var violations = report.Violations;
            var dtos = new ViolationDto[violations.Count];
            var perCode = new Dictionary<ViolationCode, int>();
            for (int i = 0; i < violations.Count; i++)
            {
                var v = violations[i];
                dtos[i] = new ViolationDto { asset = v.AssetPath, code = v.Code.ToString(), detail = v.Detail };
                perCode.TryGetValue(v.Code, out int n);
                perCode[v.Code] = n + 1;
            }

            var byCode = new List<CountDto>(perCode.Count);
            foreach (var kv in perCode) byCode.Add(new CountDto { code = kv.Key.ToString(), count = kv.Value });
            byCode.Sort((a, b) => string.CompareOrdinal(a.code, b.code));

            var dupDtos = Array.Empty<DuplicateDto>();
            if (duplicates != null && duplicates.Count > 0)
            {
                dupDtos = new DuplicateDto[duplicates.Count];
                for (int i = 0; i < duplicates.Count; i++)
                    dupDtos[i] = new DuplicateDto { asset = duplicates[i].Asset, bundles = duplicates[i].Bundles };
            }

            var dto = new ReportDto
            {
                policy = report.PolicyDescription,
                assetsScanned = report.AssetsScanned,
                violationCount = report.ViolationCount,
                byCode = byCode.ToArray(),
                violations = dtos,
                duplicateCount = dupDtos.Length,
                duplicateDependencies = dupDtos,
            };
            return JsonUtility.ToJson(dto, prettyPrint);
        }

        /// <summary>Writes the audit JSON into <paramref name="directory"/> as <see cref="ReportFileName"/>; returns its path.</summary>
        public static string Write(
            AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates, string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Report directory required.", nameof(directory));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ReportFileName);
            File.WriteAllText(path, ToJson(report, duplicates));
            return path;
        }
    }
}
