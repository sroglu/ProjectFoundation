using System;
using System.Collections.Generic;

namespace PFound.AssetPipeline.Core.Tests
{
    /// <summary>
    /// Standalone mono/csc runner for the engine-free asset-policy core: synthetic importer facts in, expected
    /// violation set out — no Unity, no AssetDatabase. Mirrors the ContentDelivery.Core test harness.
    /// </summary>
    internal static class Program
    {
        private static int s_passed;
        private static int s_failed;

        private static int Main()
        {
            Run("Clean mobile-default texture yields no violations", Texture_CleanIsClean);
            Run("Uncompressed texture flagged when RequireCompressed", Texture_Uncompressed);
            Run("Uncompressed RGBA32 flagged by the narrow rule when RequireCompressed off", Texture_Rgba32NarrowRule);
            Run("RequireCompressed subsumes the RGBA32 rule (no double report)", Texture_NoDoubleReport);
            Run("Crunch flagged when DisallowCrunch", Texture_Crunched);
            Run("maxTextureSize above the cap flagged", Texture_ExceedsMaxSize);
            Run("NPOT non-sprite texture with NPOT-scale None flagged", Texture_NpotTextureFlagged);
            Run("NPOT texture with NPOT-scale set is clean", Texture_NpotTextureScaledClean);
            Run("Power-of-two texture is clean", Texture_PowerOfTwoTextureClean);
            Run("NPOT sprite is exempt from the POT rule", Texture_NpotSpriteExempt);
            Run("Texture Read/Write flagged unless required", Texture_ReadWriteFlagged);
            Run("Texture Read/Write skipped when marked required", Texture_ReadWriteRequiredSkipped);
            Run("Sprite with mipmaps flagged", Texture_SpriteMipmapsFlagged);
            Run("Non-sprite texture with mipmaps is not flagged", Texture_NonSpriteMipmapsClean);
            Run("Mesh Read/Write flagged unless required", Mesh_ReadWriteFlagged);
            Run("Mesh Read/Write skipped when marked required", Mesh_ReadWriteRequiredSkipped);
            Run("Mesh below minimum compression flagged", Mesh_BelowMinCompression);
            Run("Mesh at/above minimum compression clean", Mesh_AtMinCompressionClean);
            Run("Evaluate batches textures and meshes together", Evaluate_Batches);
            Run("Disabling a rule suppresses its violation", Policy_RuleTogglesOff);
            Run("MobileDefaults Describe lists the active rules", Policy_DescribeDefaults);
            Run("AuditReport summarizes counts + per-code roll-up", Report_Summarizes);
            Run("AtlasInputHasher is order-independent", Atlas_OrderIndependent);
            Run("AtlasInputHasher changes when a member is added", Atlas_AddMemberChanges);
            Run("AtlasInputHasher changes when a member's content changes", Atlas_ContentChange);
            Run("AtlasInputHasher empty set is stable + non-empty", Atlas_EmptyStable);

            Run("Overridden platform uncompressed flagged with platform tag", Platform_OverrideUncompressed);
            Run("Overridden platform oversize flagged with platform tag", Platform_OverrideOversize);
            Run("Non-overridden platform is ignored", Platform_NotOverriddenIgnored);
            Run("Default + platform violations coexist without merging", Platform_DefaultAndOverrideCoexist);
            Run("Graph skips an unreferenced texture entirely", Graph_UnreferencedSkipped);
            Run("Graph checks only Read/Write on an atlas member", Graph_AtlasMemberOnlyReadWrite);
            Run("Graph skips an unreferenced mesh", Graph_UnreferencedMeshSkipped);
            Run("Null graph evaluates everything (parity)", Graph_NullEvaluatesAll);
            Run("Atlas size covers summed area", AtlasSize_CoversArea);
            Run("Atlas size holds the largest single dimension", AtlasSize_HoldsLargestDimension);
            Run("Atlas size clamps to 4096 and flags it", AtlasSize_Clamps);
            Run("Atlas size floors at the minimum", AtlasSize_MinFloor);
            Run("Duplicate textures grouped by identical content hash", Dup_GroupsIdentical);
            Run("Distinct content hashes yield no duplicate group", Dup_DistinctNoGroup);
            Run("Duplicate finder ignores empty path/hash entries", Dup_IgnoresEmpty);

            Console.WriteLine();
            Console.WriteLine(s_failed == 0
                ? "ALL PASSED (" + s_passed + ")"
                : (s_passed + " passed, " + s_failed + " FAILED"));
            return s_failed == 0 ? 0 : 1;
        }

        // ---- texture tests -----------------------------------------------------------------------

        private static void Texture_CleanIsClean()
        {
            var f = new TextureImporterFacts
            {
                AssetPath = "a.png", Width = 512, Height = 512, Compression = TextureCompressionLevel.Normal,
                Crunched = false, MaxTextureSize = 2048, NpotScale = NpotScale.None, IsSprite = false,
                FormatName = "ASTC_6x6",
            };
            AssertNoViolations(Eval(f));
        }

        private static void Texture_Uncompressed()
        {
            var f = NormalTexture();
            f.Compression = TextureCompressionLevel.Uncompressed;
            f.FormatName = "RGBA32";
            AssertSingle(Eval(f), ViolationCode.TextureUncompressed);
        }

        private static void Texture_Rgba32NarrowRule()
        {
            var policy = new AssetPolicy { RequireCompressed = false, DisallowUncompressedRgba32 = true };
            var f = NormalTexture();
            f.Compression = TextureCompressionLevel.Uncompressed;
            f.FormatName = "RGBA32";
            AssertSingle(Eval(f, policy), ViolationCode.TextureUncompressedRgba32);

            // Uncompressed but not RGBA32 → the narrow rule does not fire.
            var g = NormalTexture();
            g.Compression = TextureCompressionLevel.Uncompressed;
            g.FormatName = "RGB24";
            AssertNoViolations(Eval(g, policy));
        }

        private static void Texture_NoDoubleReport()
        {
            var f = NormalTexture();
            f.Compression = TextureCompressionLevel.Uncompressed;
            f.FormatName = "RGBA32";
            var v = Eval(f); // RequireCompressed on by default
            AssertEqual(1, v.Count, "exactly one compression violation (no RGBA32 double-report)");
            AssertEqual(ViolationCode.TextureUncompressed, v[0].Code, "the general rule, not the narrow one");
        }

        private static void Texture_Crunched()
        {
            var f = NormalTexture();
            f.Crunched = true;
            AssertSingle(Eval(f), ViolationCode.TextureCrunched);
        }

        private static void Texture_ExceedsMaxSize()
        {
            var f = NormalTexture();
            f.MaxTextureSize = 4096; // policy cap is 2048
            AssertSingle(Eval(f), ViolationCode.TextureExceedsMaxSize);
        }

        private static void Texture_NpotTextureFlagged()
        {
            var f = NormalTexture();
            f.Width = 100; f.Height = 100; f.IsSprite = false; f.NpotScale = NpotScale.None;
            AssertSingle(Eval(f), ViolationCode.TextureNotPowerOfTwoNoNpotScale);
        }

        private static void Texture_NpotTextureScaledClean()
        {
            var f = NormalTexture();
            f.Width = 100; f.Height = 100; f.IsSprite = false; f.NpotScale = NpotScale.ToNearest;
            AssertNoViolations(Eval(f));
        }

        private static void Texture_PowerOfTwoTextureClean()
        {
            var f = NormalTexture();
            f.Width = 256; f.Height = 128; f.IsSprite = false; f.NpotScale = NpotScale.None;
            AssertNoViolations(Eval(f));
        }

        private static void Texture_NpotSpriteExempt()
        {
            var f = NormalTexture();
            f.Width = 100; f.Height = 100; f.IsSprite = true; f.NpotScale = NpotScale.None;
            AssertNoViolations(Eval(f));
        }

        private static void Texture_ReadWriteFlagged()
        {
            var f = NormalTexture();
            f.ReadWriteEnabled = true;
            AssertSingle(Eval(f), ViolationCode.TextureReadWriteEnabled);
        }

        private static void Texture_ReadWriteRequiredSkipped()
        {
            var f = NormalTexture();
            f.ReadWriteEnabled = true; f.ReadWriteRequired = true;
            AssertNoViolations(Eval(f));
        }

        private static void Texture_SpriteMipmapsFlagged()
        {
            var f = NormalTexture();
            f.IsSprite = true; f.MipmapsEnabled = true; // 512x512 POT sprite → only the mipmap rule fires
            AssertSingle(Eval(f), ViolationCode.SpriteMipmapsEnabled);
        }

        private static void Texture_NonSpriteMipmapsClean()
        {
            var f = NormalTexture();
            f.IsSprite = false; f.MipmapsEnabled = true; // 3D textures legitimately keep mipmaps
            AssertNoViolations(Eval(f));
        }

        // ---- mesh tests --------------------------------------------------------------------------

        private static void Mesh_ReadWriteFlagged()
        {
            var m = NormalMesh();
            m.ReadWriteEnabled = true;
            AssertSingle(EvalMesh(m), ViolationCode.MeshReadWriteEnabled);
        }

        private static void Mesh_ReadWriteRequiredSkipped()
        {
            var m = NormalMesh();
            m.ReadWriteEnabled = true; m.ReadWriteRequired = true;
            AssertNoViolations(EvalMesh(m));
        }

        private static void Mesh_BelowMinCompression()
        {
            var policy = new AssetPolicy { MinMeshCompression = MeshCompressionLevel.Medium };
            var m = NormalMesh();
            m.MeshCompression = MeshCompressionLevel.Low;
            AssertSingle(EvalMesh(m, policy), ViolationCode.MeshUncompressed);
        }

        private static void Mesh_AtMinCompressionClean()
        {
            var policy = new AssetPolicy { MinMeshCompression = MeshCompressionLevel.Medium };
            var m = NormalMesh();
            m.MeshCompression = MeshCompressionLevel.High;
            AssertNoViolations(EvalMesh(m, policy));
        }

        // ---- batch / policy ----------------------------------------------------------------------

        private static void Evaluate_Batches()
        {
            var t = NormalTexture(); t.Crunched = true;
            var m = NormalMesh(); m.ReadWriteEnabled = true;
            var v = AssetPolicyEvaluator.Evaluate(new[] { t }, new[] { m }, AssetPolicy.MobileDefaults());
            AssertEqual(2, v.Count, "one texture + one mesh violation");
        }

        private static void Policy_RuleTogglesOff()
        {
            var f = NormalTexture(); f.Crunched = true;
            var policy = new AssetPolicy { DisallowCrunch = false };
            AssertNoViolations(Eval(f, policy));
        }

        private static void Policy_DescribeDefaults()
        {
            string d = AssetPolicy.MobileDefaults().Describe();
            Assert(d.Contains("maxTextureSize<=2048"), "describes the resolution cap");
            Assert(d.Contains("require-compressed"), "describes require-compressed");
            Assert(d.Contains("disallow-crunch"), "describes disallow-crunch");
            Assert(d.Contains("disallow-mesh-readwrite"), "describes disallow-mesh-readwrite");
        }

        private static void Report_Summarizes()
        {
            var violations = new List<PolicyViolation>
            {
                new PolicyViolation("a.png", ViolationCode.TextureCrunched, "x"),
                new PolicyViolation("b.png", ViolationCode.TextureCrunched, "y"),
                new PolicyViolation("c.fbx", ViolationCode.MeshReadWriteEnabled, "z"),
            };
            var report = new AssetAuditReport("policy-x", 10, violations);
            AssertEqual(10, report.AssetsScanned, "assets scanned");
            AssertEqual(3, report.ViolationCount, "violation count");
            Assert(!report.IsClean, "not clean");
            AssertEqual(2, report.CountOf(ViolationCode.TextureCrunched), "two crunch violations");
            AssertEqual(1, report.CountOf(ViolationCode.MeshReadWriteEnabled), "one mesh violation");

            var clean = new AssetAuditReport("policy-x", 5, new List<PolicyViolation>());
            Assert(clean.IsClean, "empty report is clean");
        }

        private static void Atlas_OrderIndependent()
        {
            string a = AtlasInputHasher.Compute(new[] { "x|1", "y|2", "z|3" });
            string b = AtlasInputHasher.Compute(new[] { "z|3", "x|1", "y|2" });
            AssertEqual(a, b, "same set, different order → same hash");
        }

        private static void Atlas_AddMemberChanges()
        {
            string before = AtlasInputHasher.Compute(new[] { "x|1", "y|2" });
            string after = AtlasInputHasher.Compute(new[] { "x|1", "y|2", "z|3" });
            Assert(before != after, "adding a member changes the hash");
        }

        private static void Atlas_ContentChange()
        {
            string before = AtlasInputHasher.Compute(new[] { "x|aaa", "y|2" });
            string after = AtlasInputHasher.Compute(new[] { "x|bbb", "y|2" });
            Assert(before != after, "a member's content hash change changes the atlas hash");
        }

        private static void Atlas_EmptyStable()
        {
            string a = AtlasInputHasher.Compute(new string[0]);
            string b = AtlasInputHasher.Compute(new string[0]);
            AssertEqual(a, b, "empty set is stable");
            AssertEqual(32, a.Length, "hash is 32 hex chars");
        }

        // ---- per-platform tests ------------------------------------------------------------------

        private static void Platform_OverrideUncompressed()
        {
            var f = NormalTexture();
            f.PlatformOverrides = new List<PlatformTextureFacts>
            {
                new PlatformTextureFacts
                {
                    Platform = "iOS", Overridden = true, Compression = TextureCompressionLevel.Uncompressed,
                    FormatName = "RGBA32", MaxTextureSize = 2048,
                },
            };
            var v = Eval(f);
            AssertEqual(1, v.Count, "one platform violation");
            AssertEqual(ViolationCode.TextureUncompressed, v[0].Code, "uncompressed on the override");
            AssertEqual("iOS", v[0].Platform, "tagged with the platform");
        }

        private static void Platform_OverrideOversize()
        {
            var f = NormalTexture();
            f.PlatformOverrides = new List<PlatformTextureFacts>
            {
                new PlatformTextureFacts
                {
                    Platform = "Android", Overridden = true, Compression = TextureCompressionLevel.Normal,
                    FormatName = "ETC2_RGBA8", MaxTextureSize = 4096,
                },
            };
            var v = Eval(f);
            AssertSingle(v, ViolationCode.TextureExceedsMaxSize);
            AssertEqual("Android", v[0].Platform, "tagged with the platform");
        }

        private static void Platform_NotOverriddenIgnored()
        {
            var f = NormalTexture();
            f.PlatformOverrides = new List<PlatformTextureFacts>
            {
                new PlatformTextureFacts
                {
                    Platform = "iOS", Overridden = false, Compression = TextureCompressionLevel.Uncompressed,
                    FormatName = "RGBA32", MaxTextureSize = 8192,
                },
            };
            AssertNoViolations(Eval(f)); // an inherited (non-overridden) platform mirrors the clean default
        }

        private static void Platform_DefaultAndOverrideCoexist()
        {
            var f = NormalTexture();
            f.Crunched = true; // default-platform crunch violation
            f.PlatformOverrides = new List<PlatformTextureFacts>
            {
                new PlatformTextureFacts
                {
                    Platform = "iOS", Overridden = true, Compression = TextureCompressionLevel.Uncompressed,
                    FormatName = "RGBA32", MaxTextureSize = 2048,
                },
            };
            var v = Eval(f);
            AssertEqual(2, v.Count, "default crunch + iOS uncompressed");
        }

        // ---- reference-graph gating tests --------------------------------------------------------

        private static void Graph_UnreferencedSkipped()
        {
            var f = NormalTexture();
            f.Crunched = true;
            var graph = new FakeGraph(); // nothing referenced
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateTexture(f, AssetPolicy.MobileDefaults(), into, graph);
            AssertEqual(0, into.Count, "unreferenced asset is skipped");
        }

        private static void Graph_AtlasMemberOnlyReadWrite()
        {
            var f = NormalTexture();
            f.Crunched = true;            // a format rule that must be suppressed for atlas members
            f.ReadWriteEnabled = true;    // the one rule that still applies
            var graph = new FakeGraph();
            graph.Referenced.Add(f.AssetPath);
            graph.AtlasPacked.Add(f.AssetPath);
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateTexture(f, AssetPolicy.MobileDefaults(), into, graph);
            AssertEqual(1, into.Count, "only the Read/Write rule applies to an atlas member");
            AssertEqual(ViolationCode.TextureReadWriteEnabled, into[0].Code, "and it is the Read/Write one");
        }

        private static void Graph_UnreferencedMeshSkipped()
        {
            var m = NormalMesh();
            m.ReadWriteEnabled = true;
            var graph = new FakeGraph();
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateMesh(m, AssetPolicy.MobileDefaults(), into, graph);
            AssertEqual(0, into.Count, "unreferenced mesh is skipped");
        }

        private static void Graph_NullEvaluatesAll()
        {
            var f = NormalTexture();
            f.Crunched = true;
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateTexture(f, AssetPolicy.MobileDefaults(), into, null);
            AssertEqual(1, into.Count, "null graph = no gating, everything evaluated");
        }

        // ---- atlas-size tests --------------------------------------------------------------------

        private static void AtlasSize_CoversArea()
        {
            // 4 sprites of 64x64 = 16384 px area; smallest POT square covering it is 128 (128*128=16384).
            int size = AtlasSizeCalculator.ComputeMaxTextureSize(4 * 64 * 64, 64, out bool clamped);
            AssertEqual(128, size, "smallest POT square covering the summed area");
            Assert(!clamped, "well within the cap");
        }

        private static void AtlasSize_HoldsLargestDimension()
        {
            // Tiny total area but one 512-wide member forces the page up to 512.
            int size = AtlasSizeCalculator.ComputeMaxTextureSize(1024, 512, out _);
            AssertEqual(512, size, "page side must hold the largest single member");
        }

        private static void AtlasSize_Clamps()
        {
            int size = AtlasSizeCalculator.ComputeMaxTextureSize(1e12, 100000, out bool clamped);
            AssertEqual(AtlasSizeCalculator.MaxAtlasSize, size, "clamped to 4096");
            Assert(clamped, "clamp flag set");
        }

        private static void AtlasSize_MinFloor()
        {
            int size = AtlasSizeCalculator.ComputeMaxTextureSize(0, 0, out bool clamped);
            AssertEqual(AtlasSizeCalculator.MinAtlasSize, size, "empty set floors at the minimum page");
            Assert(!clamped, "not clamped");
        }

        // ---- duplicate-texture tests -------------------------------------------------------------

        private static void Dup_GroupsIdentical()
        {
            var groups = DuplicateTextureFinder.Find(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("a.png", "H1"),
                new KeyValuePair<string, string>("b.png", "H1"),
                new KeyValuePair<string, string>("c.png", "H2"),
                new KeyValuePair<string, string>("d.png", "H1"),
            });
            AssertEqual(1, groups.Count, "one duplicate group (H1)");
            AssertEqual("H1", groups[0].ContentHash, "grouped on the shared hash");
            AssertEqual(3, groups[0].Paths.Length, "all three identical paths");
            AssertEqual("a.png", groups[0].Paths[0], "paths sorted");
        }

        private static void Dup_DistinctNoGroup()
        {
            var groups = DuplicateTextureFinder.Find(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("a.png", "H1"),
                new KeyValuePair<string, string>("b.png", "H2"),
            });
            AssertEqual(0, groups.Count, "distinct content, no duplicate group");
        }

        private static void Dup_IgnoresEmpty()
        {
            var groups = DuplicateTextureFinder.Find(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("", "H1"),
                new KeyValuePair<string, string>("b.png", ""),
                new KeyValuePair<string, string>("c.png", "H3"),
            });
            AssertEqual(0, groups.Count, "empty path/hash entries are ignored");
        }

        private sealed class FakeGraph : IAssetReferenceGraph
        {
            public readonly HashSet<string> Referenced = new HashSet<string>();
            public readonly HashSet<string> AtlasPacked = new HashSet<string>();
            public bool IsReferenced(string assetPath) => Referenced.Contains(assetPath);
            public bool IsAtlasPacked(string assetPath) => AtlasPacked.Contains(assetPath);
        }

        // ---- helpers -----------------------------------------------------------------------------

        private static TextureImporterFacts NormalTexture() => new TextureImporterFacts
        {
            AssetPath = "t.png", Width = 512, Height = 512, Compression = TextureCompressionLevel.Normal,
            Crunched = false, MaxTextureSize = 2048, NpotScale = NpotScale.None, IsSprite = false,
            MipmapsEnabled = false, FormatName = "ASTC_6x6",
        };

        private static MeshImporterFacts NormalMesh() => new MeshImporterFacts
        {
            AssetPath = "m.fbx", ReadWriteEnabled = false, MeshCompression = MeshCompressionLevel.Off,
            OptimizeMesh = true, ReadWriteRequired = false,
        };

        private static List<PolicyViolation> Eval(TextureImporterFacts f, AssetPolicy policy = null)
        {
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateTexture(f, policy ?? AssetPolicy.MobileDefaults(), into);
            return into;
        }

        private static List<PolicyViolation> EvalMesh(MeshImporterFacts m, AssetPolicy policy = null)
        {
            var into = new List<PolicyViolation>();
            AssetPolicyEvaluator.EvaluateMesh(m, policy ?? AssetPolicy.MobileDefaults(), into);
            return into;
        }

        private static void AssertNoViolations(List<PolicyViolation> v)
        {
            if (v.Count != 0) throw new Exception("expected no violations but got: " + Join(v));
        }

        private static void AssertSingle(List<PolicyViolation> v, ViolationCode code)
        {
            if (v.Count != 1) throw new Exception("expected exactly one violation (" + code + ") but got: " + Join(v));
            if (v[0].Code != code) throw new Exception("expected " + code + " but got " + v[0].Code);
        }

        private static string Join(List<PolicyViolation> v) => v.Count == 0 ? "(none)" : string.Join("; ", v);

        // ---- harness -----------------------------------------------------------------------------

        private static void Run(string name, Action test)
        {
            try { test(); s_passed++; Console.WriteLine("  PASS  " + name); }
            catch (Exception e) { s_failed++; Console.WriteLine("  FAIL  " + name + " :: " + e.Message); }
        }

        private static void Assert(bool condition, string what)
        {
            if (!condition) throw new Exception("assertion failed: " + what);
        }

        private static void AssertEqual(object expected, object actual, string what)
        {
            if (!Equals(expected, actual))
                throw new Exception("expected <" + expected + "> but got <" + actual + "> for " + what);
        }
    }
}
