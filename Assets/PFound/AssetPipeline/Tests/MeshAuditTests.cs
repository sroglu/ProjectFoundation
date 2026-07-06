using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PFound.AssetPipeline.Core;
using PFound.AssetPipeline.Editor;
using PFound.ContentDelivery.Editor;
using UnityEditor;
using UnityEngine;

namespace PFound.AssetPipeline.Tests
{
    /// <summary>
    /// The mesh audit/apply path over a real <see cref="ModelImporter"/> (a tiny .obj imported with Read/Write on
    /// and no mesh compression): the audit reports both mesh violations without mutating, and the explicit apply
    /// turns Read/Write off and raises compression so a re-audit is clean. The bad importer state is forced in
    /// [SetUp] (test context) — importer writes made in [OneTimeSetUp] do not persist in the Unity Test Framework.
    /// </summary>
    public sealed class MeshAuditTests
    {
        private const string AssetFolder = "Assets/__pf_ap_mesh";
        private const string FolderLeaf = "__pf_ap_mesh";
        private const string ObjAsset = AssetFolder + "/tri.obj";

        private AssetGroup _group;

        // Requires some mesh compression and disallows Read/Write (texture rules are irrelevant for a model).
        private static AssetPolicy MeshPolicy() => new AssetPolicy { MinMeshCompression = MeshCompressionLevel.Medium };

        [OneTimeSetUp]
        public void CreateProjectAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", FolderLeaf);

            // Minimal Wavefront OBJ — a single triangle with a face normal (avoids the "no normals" import warning);
            // Unity imports it through ModelImporter.
            const string obj = "o tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//1 2//1 3//1\n";
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(Path.Combine(projectRoot, ObjAsset), obj);
            AssetDatabase.ImportAsset(ObjAsset, ImportAssetOptions.ForceSynchronousImport);

            _group = ScriptableObject.CreateInstance<AssetGroup>();
            _group.BundleName = "mesh_bundle";
            _group.Entries = new List<AssetEntry>
            {
                new AssetEntry { Asset = AssetDatabase.LoadMainAssetAtPath(ObjAsset), Address = "tri" },
            };
            AssetDatabase.CreateAsset(_group, AssetFolder + "/Group.asset");
            AssetDatabase.SaveAssets();
        }

        [SetUp]
        public void ForceBadImporterState()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ObjAsset);
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        [OneTimeTearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(AssetFolder);

        [Test]
        public void Reader_SeesModelImporterState()
        {
            var facts = MeshImporterReader.Read(ObjAsset);
            Assert.IsNotNull(facts, "the reader produces mesh facts for the model");
            Assert.AreEqual(MeshCompressionLevel.Off, facts.MeshCompression, "the reader sees no mesh compression");
            Assert.IsTrue(facts.ReadWriteEnabled, "the reader sees Read/Write enabled");
        }

        [Test]
        public void Audit_ReportsMeshViolations_WithoutMutating()
        {
            var report = AssetAuditor.Audit(new[] { _group }, MeshPolicy());

            Assert.AreEqual(1, report.AssetsScanned, "the model was scanned");
            Assert.AreEqual(1, report.CountOf(ViolationCode.MeshReadWriteEnabled), "Read/Write flagged");
            Assert.AreEqual(1, report.CountOf(ViolationCode.MeshUncompressed), "no mesh compression flagged");

            var unchanged = (ModelImporter)AssetImporter.GetAtPath(ObjAsset);
            Assert.IsTrue(unchanged.isReadable, "audit is read-only: the importer is untouched");
        }

        [Test]
        public void Apply_CorrectsImporter_AndReauditIsClean()
        {
            var policy = MeshPolicy();
            var before = AssetAuditor.Audit(new[] { _group }, policy);
            Assert.IsFalse(before.IsClean, "precondition: violations exist to fix");

            int changed = AssetOptimizer.Apply(before, policy);
            Assert.AreEqual(1, changed, "the model had fixable violations");

            var after = (ModelImporter)AssetImporter.GetAtPath(ObjAsset);
            Assert.IsFalse(after.isReadable, "Read/Write turned off");
            Assert.AreNotEqual(ModelImporterMeshCompression.Off, after.meshCompression, "mesh compression raised to the policy minimum");

            Assert.IsTrue(AssetAuditor.Audit(new[] { _group }, policy).IsClean, "a re-audit after apply is clean");
        }
    }
}
