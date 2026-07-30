using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiCommandRegistryTests
    {
        // Test-local commands. The scanner walks the entire AppDomain, so we
        // pick highly-distinctive names that no real shipping code would use.
        static class _CmdTargets
        {
            [KuiCommand("kunai-test-add",  help: "add a + b")]
            public static int Add(int a, int b) => a + b;

            [KuiCommand("kunai-test-noop")]
            public static void Noop() { }

            [KuiCommand("kunai-test-bad-param")]
            public static void BadParam(System.IDisposable d) { }   // unsupported type → skipped
        }

        // Separate non-static fixture so we can declare an instance method
        // that the scanner is supposed to skip with a warning.
        class _CmdInstanceTargets
        {
            [KuiCommand("kunai-test-instance-skip")]
            public int InstanceMethod() => 1;
        }

        [Test]
        public void BuildFromScan_RegistersValidStaticCommands()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            Assert.IsTrue(reg.TryGet("kunai-test-add", out var add));
            Assert.AreEqual(2, add.Parameters.Length);
            Assert.IsTrue(reg.TryGet("kunai-test-noop", out _));
        }

        [Test]
        public void BuildFromScan_SkipsUnsupportedParamType()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            Assert.IsFalse(reg.TryGet("kunai-test-bad-param", out _),
                "Methods with unsupported parameter types should NOT register");
        }

        [Test]
        public void BuildFromScan_SkipsInstanceMethod()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            Assert.IsFalse(reg.TryGet("kunai-test-instance-skip", out _),
                "Instance methods should NOT register");
        }

        [Test]
        public void Match_PrefixCaseInsensitive()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            int hits = 0;
            foreach (var name in reg.Match("KUNAI-TEST-"))
            {
                Assert.IsTrue(name.StartsWith("kunai-test-", System.StringComparison.OrdinalIgnoreCase));
                hits++;
            }
            Assert.GreaterOrEqual(hits, 2, "expected to match at least 2 kunai-test-* commands");
        }

        [Test]
        public void Match_EmptyPrefix_ReturnsAll()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            int total = 0;
            foreach (var _ in reg.Match("")) total++;
            Assert.AreEqual(reg.Count, total);
        }

        [Test]
        public void TryGet_UnknownName_False()
        {
            var reg = new KuiCommandRegistry();
            reg.BuildFromScan();
            Assert.IsFalse(reg.TryGet("definitely-not-a-real-command-xyz123", out var entry));
            Assert.IsNull(entry);
        }

        [Test]
        public void IsSupportedParam_CoversExpectedTypes()
        {
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(string)));
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(int)));
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(float)));
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(double)));
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(bool)));
            Assert.IsTrue (KuiCommandRegistry.IsSupportedParam(typeof(KuiLogLevel)));
            Assert.IsFalse(KuiCommandRegistry.IsSupportedParam(typeof(System.IDisposable)));
            Assert.IsFalse(KuiCommandRegistry.IsSupportedParam(typeof(byte[])));
            Assert.IsFalse(KuiCommandRegistry.IsSupportedParam(typeof(UnityEngine.Vector3)));
        }
    }
}
