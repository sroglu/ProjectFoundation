using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PFound.AssetPipeline.Editor;
using PFound.ContentDelivery.Editor;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace PFound.AssetPipeline.Tests
{
    /// <summary>
    /// The sprite-atlas builder: a set of sprites is packed into a real <see cref="UnityEngine.U2D.SpriteAtlas"/>
    /// via Unity's own API, the build stamps an input content hash, and that hash changes when the input SET
    /// changes — so a content-addressed rebuild is detectable. Also pins the "feed back into authoring" step.
    /// </summary>
    public sealed class SpriteAtlasBuilderTests
    {
        private const string AssetFolder = "Assets/__pf_ap_atlas";
        private const string FolderLeaf = "__pf_ap_atlas";
        private const string AtlasPath = AssetFolder + "/Test.spriteatlas";

        private string _sprite1, _sprite2, _sprite3;

        [OneTimeSetUp]
        public void CreateProjectAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", FolderLeaf);

            _sprite1 = WriteSprite("s1.png");
            _sprite2 = WriteSprite("s2.png");
            _sprite3 = WriteSprite("s3.png");
        }

        [OneTimeTearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(AssetFolder);

        [Test]
        public void Build_PacksSprites_AndStampsContentHash()
        {
            var paths = new List<string> { _sprite1, _sprite2 };
            string expected = SpriteAtlasBuilder.ComputeContentHash(paths);

            var result = SpriteAtlasBuilder.Build(AtlasPath, paths);

            Assert.AreEqual(2, result.MemberCount, "both sprites went into the atlas");
            Assert.AreEqual(expected, result.ContentHash, "the build's hash matches the standalone computation");
            Assert.AreEqual(32, result.ContentHash.Length, "content hash is 32 hex chars");

            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(AtlasPath);
            Assert.IsNotNull(atlas, "the atlas asset was created");
            Assert.AreEqual(2, atlas.GetPackables().Length, "the atlas holds the two input sprites");
            Assert.AreEqual(expected, SpriteAtlasBuilder.GetStoredContentHash(AtlasPath), "the hash is persisted on the atlas");
            Assert.IsTrue(SpriteAtlasBuilder.IsUpToDate(AtlasPath, paths), "a rebuild of the same set is up to date");
        }

        [Test]
        public void ContentHash_ChangesWhenInputSetChanges()
        {
            string two = SpriteAtlasBuilder.ComputeContentHash(new[] { _sprite1, _sprite2 });
            string three = SpriteAtlasBuilder.ComputeContentHash(new[] { _sprite1, _sprite2, _sprite3 });
            Assert.AreNotEqual(two, three, "adding a sprite to the input set changes the atlas content hash");
        }

        [Test]
        public void AddAtlasToGroup_FeedsAuthoring()
        {
            SpriteAtlasBuilder.Build(AtlasPath, new List<string> { _sprite1, _sprite2 });

            var group = ScriptableObject.CreateInstance<AssetGroup>();
            group.BundleName = "atlas_bundle";
            try
            {
                var entry = SpriteAtlasBuilder.AddAtlasToGroup(group, AtlasPath, "ui/atlas");
                Assert.AreEqual("ui/atlas", entry.Address);
                Assert.AreEqual(1, group.Entries.Count, "the atlas was appended to the group");
                Assert.AreEqual(AtlasPath, AssetDatabase.GetAssetPath(group.Entries[0].Asset), "the entry references the built atlas");
            }
            finally { Object.DestroyImmediate(group); }
        }

        private string WriteSprite(string fileName)
        {
            string assetPath = AssetFolder + "/" + fileName;

            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            // Vary pixels by file name so each sprite has a distinct source content hash.
            byte tint = (byte)(fileName.GetHashCode() & 0xFF);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(tint, 128, 64, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllBytes(Path.Combine(projectRoot, assetPath), png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
            return assetPath;
        }
    }
}
