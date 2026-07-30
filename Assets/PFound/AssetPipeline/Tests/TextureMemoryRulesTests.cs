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
    /// The two texture memory rules: a Default texture with Read/Write enabled (a CPU copy alongside the GPU one)
    /// and a Sprite with mipmaps (dead weight at native resolution) are authored into an <see cref="AssetGroup"/>;
    /// the audit reports both without mutating, and apply turns Read/Write and mipmaps off so a re-audit is clean.
    /// The non-sprite texture keeps its (default) mipmaps untouched — that rule is sprite-scoped. Bad state is forced
    /// in [SetUp] (test context), since importer writes in [OneTimeSetUp] don't persist in the Test Framework.
    /// </summary>
    public sealed class TextureMemoryRulesTests
    {
        private const string AssetFolder = "Assets/__pf_ap_mem";
        private const string FolderLeaf = "__pf_ap_mem";
        private const string TexPath = AssetFolder + "/rwtex.png";    // Default, Read/Write on
        private const string SpritePath = AssetFolder + "/mipsprite.png"; // Sprite, mipmaps on

        private AssetGroup _group;

        [OneTimeSetUp]
        public void CreateProjectAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", FolderLeaf);

            WritePng(TexPath, 256, 256);    // power-of-two → no NPOT noise
            WritePng(SpritePath, 256, 256);

            _group = ScriptableObject.CreateInstance<AssetGroup>();
            _group.BundleName = "mem_bundle";
            _group.Entries = new List<AssetEntry>
            {
                new AssetEntry { Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath), Address = "rw" },
                new AssetEntry { Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath), Address = "mip" },
            };
            AssetDatabase.CreateAsset(_group, AssetFolder + "/Group.asset");
            AssetDatabase.SaveAssets();
        }

        [SetUp]
        public void ForceBadImporterState()
        {
            var tex = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            tex.textureType = TextureImporterType.Default;
            tex.isReadable = true; // Read/Write on (the violation)
            tex.SaveAndReimport();

            var sprite = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            sprite.textureType = TextureImporterType.Sprite;
            sprite.mipmapEnabled = true; // mipmaps on a sprite (the violation)
            sprite.SaveAndReimport();
        }

        [OneTimeTearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(AssetFolder);

        [Test]
        public void Audit_FlagsTextureMemoryViolations_WithoutMutating()
        {
            var report = AssetAuditor.Audit(new[] { _group }, AssetPolicy.MobileDefaults());

            Assert.AreEqual(2, report.AssetsScanned, "both textures scanned");
            Assert.AreEqual(1, report.CountOf(ViolationCode.TextureReadWriteEnabled), "Read/Write texture flagged");
            Assert.AreEqual(1, report.CountOf(ViolationCode.SpriteMipmapsEnabled), "mipmapped sprite flagged");

            var unchanged = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            Assert.IsTrue(unchanged.isReadable, "audit is read-only: the importer is untouched");
        }

        [Test]
        public void Apply_TurnsOffReadWriteAndSpriteMipmaps_AndReauditIsClean()
        {
            var policy = AssetPolicy.MobileDefaults();
            var before = AssetAuditor.Audit(new[] { _group }, policy);
            Assert.IsFalse(before.IsClean, "precondition: violations exist to fix");

            int changed = AssetOptimizer.Apply(before, policy);
            Assert.AreEqual(2, changed, "both textures had fixable violations");

            Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(TexPath)).isReadable, "Read/Write turned off");
            Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(SpritePath)).mipmapEnabled, "sprite mipmaps turned off");

            Assert.IsTrue(AssetAuditor.Audit(new[] { _group }, policy).IsClean, "a re-audit after apply is clean");
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
