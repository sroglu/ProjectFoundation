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
