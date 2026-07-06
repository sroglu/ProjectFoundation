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
    /// The texture audit/apply path over real importers. Two deliberately mis-imported textures — one uncompressed
    /// and oversized, one non-power-of-two with NPOT scaling off — are authored into an <see cref="AssetGroup"/>;
    /// the audit reports the expected violations without mutating, and the explicit apply corrects the importers so
    /// a re-audit is clean. The "bad" importer state is forced in [SetUp] (test context): importer writes made in
    /// [OneTimeSetUp] do not persist in the Unity Test Framework, whereas test-context writes do.
    /// </summary>
    public sealed class TextureAuditTests
    {
        private const string AssetFolder = "Assets/__pf_ap_audit";
        private const string FolderLeaf = "__pf_ap_audit";
        private const string TexPath = AssetFolder + "/badtex.png";   // uncompressed, oversized, NPOT-scaled
        private const string NpotPath = AssetFolder + "/npottex.png"; // compressed, 100x100, NPOT scaling None

        private AssetGroup _group;

        [OneTimeSetUp]
        public void CreateProjectAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", FolderLeaf);

            WritePng(TexPath, 100, 100);
            WritePng(NpotPath, 100, 100);

            _group = ScriptableObject.CreateInstance<AssetGroup>();
            _group.BundleName = "audit_bundle";
            _group.Entries = new List<AssetEntry>
            {
                new AssetEntry { Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath), Address = "tex" },
                new AssetEntry { Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(NpotPath), Address = "npot" },
            };
            AssetDatabase.CreateAsset(_group, AssetFolder + "/Group.asset");
            AssetDatabase.SaveAssets();
        }

        [SetUp]
        public void ForceBadImporterState()
        {
            var tex = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            tex.textureType = TextureImporterType.Default;
            tex.textureCompression = TextureImporterCompression.Uncompressed;
            tex.crunchedCompression = false;
            tex.maxTextureSize = 4096;
            tex.npotScale = TextureImporterNPOTScale.ToNearest; // scaled → isolates the compression + size violations
            tex.SaveAndReimport();

            var npot = (TextureImporter)AssetImporter.GetAtPath(NpotPath);
            npot.textureType = TextureImporterType.Default;
            npot.textureCompression = TextureImporterCompression.Compressed;
            npot.crunchedCompression = false;
            npot.maxTextureSize = 2048;
            npot.npotScale = TextureImporterNPOTScale.None; // 100x100 stays non-power-of-two
            npot.SaveAndReimport();
        }

        [OneTimeTearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(AssetFolder);

        [Test]
        public void Audit_ReportsExpectedViolations_WithoutMutating()
        {
            var report = AssetAuditor.Audit(new[] { _group }, AssetPolicy.MobileDefaults());

            Assert.AreEqual(2, report.AssetsScanned, "both authored textures scanned");
            Assert.IsFalse(report.IsClean, "the mis-imported assets produce violations");
            Assert.AreEqual(1, report.CountOf(ViolationCode.TextureUncompressed), "uncompressed texture flagged");
            Assert.AreEqual(1, report.CountOf(ViolationCode.TextureExceedsMaxSize), "oversized max size flagged");
            Assert.AreEqual(1, report.CountOf(ViolationCode.TextureNotPowerOfTwoNoNpotScale), "NPOT texture flagged");

            var unchanged = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            Assert.AreEqual(4096, unchanged.maxTextureSize, "audit is read-only: the importer is untouched");
        }

        [Test]
        public void Apply_CorrectsImporters_AndReauditIsClean()
        {
            var policy = AssetPolicy.MobileDefaults();
            var before = AssetAuditor.Audit(new[] { _group }, policy);
            Assert.IsFalse(before.IsClean, "precondition: violations exist to fix");

            int changed = AssetOptimizer.Apply(before, policy);
            Assert.AreEqual(2, changed, "both assets had fixable violations");

            var tex = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            Assert.AreNotEqual(TextureImporterCompression.Uncompressed, tex.textureCompression, "now compressed");
            Assert.AreEqual(policy.MaxTextureSize, tex.maxTextureSize, "max size capped to policy");

            var npot = (TextureImporter)AssetImporter.GetAtPath(NpotPath);
            Assert.AreNotEqual(TextureImporterNPOTScale.None, npot.npotScale, "NPOT texture now scaled");

            var after = AssetAuditor.Audit(new[] { _group }, policy);
            Assert.IsTrue(after.IsClean, "a re-audit after apply is clean");
        }

        private static void WritePng(string assetPath, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 128, 64, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllBytes(Path.Combine(projectRoot, assetPath), png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
