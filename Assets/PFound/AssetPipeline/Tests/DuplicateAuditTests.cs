using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PFound.AssetPipeline.Core;
using PFound.AssetPipeline.Editor;
using PFound.ContentDelivery.Editor;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Tests
{
    /// <summary>
    /// The duplicate concern of the audit, reusing ContentDelivery's <see cref="BundleDuplicateAnalyzer"/>: a shared
    /// material referenced by two prefabs in two separate bundles is an implicit dependency copied into each, and the
    /// full audit surfaces it alongside the policy report. Also pins the exporter's duplicate serialization.
    /// </summary>
    public sealed class DuplicateAuditTests
    {
        private const string AssetFolder = "Assets/__pf_ap_dup";
        private const string FolderLeaf = "__pf_ap_dup";

        private string _matPath;
        private AssetGroup _groupA;
        private AssetGroup _groupB;

        [OneTimeSetUp]
        public void CreateProjectAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", FolderLeaf);

            var mat = new Material(Shader.Find("Unlit/Color")) { name = "Shared" };
            _matPath = AssetFolder + "/Shared.mat";
            AssetDatabase.CreateAsset(mat, _matPath);

            _groupA = MakeGroupWithPrefab("a", AssetFolder + "/A.prefab", mat);
            _groupB = MakeGroupWithPrefab("b", AssetFolder + "/B.prefab", mat);
            AssetDatabase.SaveAssets();
        }

        [OneTimeTearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(AssetFolder);

        [Test]
        public void AuditAll_SurfacesSharedDependencyAsDuplicate()
        {
            var result = AssetAuditor.AuditAll(new[] { _groupA, _groupB }, AssetPolicy.MobileDefaults());

            Assert.IsTrue(result.Policy.IsClean, "prefabs trigger no importer policy violations");
            Assert.IsFalse(result.IsClean, "the shared dependency makes the overall audit non-clean");

            var sharedDup = result.Duplicates.FirstOrDefault(d => d.Asset == _matPath);
            Assert.IsNotNull(sharedDup, "the shared material is reported as a cross-bundle duplicate");
            CollectionAssert.Contains(sharedDup.Bundles, "a", "duplicated into bundle a");
            CollectionAssert.Contains(sharedDup.Bundles, "b", "duplicated into bundle b");
        }

        [Test]
        public void Exporter_SerializesDuplicates()
        {
            var report = new AssetAuditReport("policy-x", 2, new List<PolicyViolation>());
            var duplicates = new List<DuplicateDependency>
            {
                new DuplicateDependency { Asset = "Assets/Shared.mat", Bundles = new[] { "a", "b" } },
            };

            string json = AssetAuditReportExporter.ToJson(report, duplicates);
            StringAssert.Contains("Assets/Shared.mat", json, "duplicate asset path present");
            StringAssert.Contains("\"duplicateCount\": 1", json, "duplicate count present");
            StringAssert.Contains("\"a\"", json, "member bundle present");
        }

        private static AssetGroup MakeGroupWithPrefab(string bundle, string prefabPath, Material shared)
        {
            var go = new GameObject(bundle);
            go.AddComponent<MeshRenderer>().sharedMaterial = shared; // implicit dependency on the material
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            var group = ScriptableObject.CreateInstance<AssetGroup>();
            group.BundleName = bundle;
            group.Entries = new List<AssetEntry> { new AssetEntry { Asset = prefab, Address = bundle + "/root" } };
            AssetDatabase.CreateAsset(group, prefabPath.Replace(".prefab", "_group.asset"));
            return group;
        }
    }
}
