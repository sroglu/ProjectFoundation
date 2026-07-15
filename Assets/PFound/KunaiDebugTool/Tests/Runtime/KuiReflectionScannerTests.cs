using System;
using System.Reflection;
using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiReflectionScannerTests
    {
        // Test-local attribute so the scan only finds members from this fixture
        // (not whatever else is loaded in the test runner's AppDomain).
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, Inherited = false)]
        sealed class _TestMarkerAttribute : Attribute
        {
            public string Tag { get; }
            public _TestMarkerAttribute(string tag) { Tag = tag; }
        }

        // Targets the scanner is supposed to find (static, declared in this assembly).
        static class _Targets
        {
            [_TestMarker("field")]      public static int  TaggedField;
            [_TestMarker("method")]     public static void TaggedMethod() { }
                                        public static int  UntaggedField;
        }

        [Test]
        public void Scan_VisitsEveryTaggedStaticMember()
        {
            int hits = 0;
            string lastTag = null;

            var result = KuiReflectionScanner.Scan<_TestMarkerAttribute>((mi, attr) =>
            {
                if (mi.DeclaringType != typeof(_Targets)) return;
                hits++;
                lastTag = attr.Tag;
            });

            Assert.GreaterOrEqual(hits, 2, "Both _Targets.TaggedField and TaggedMethod should be visited");
            Assert.IsNotNull(lastTag);
            Assert.IsNotNull(result.Errors);
        }

        [Test]
        public void Scan_VisitorThrows_RecordedAsErrorNotAborts()
        {
            int hits = 0;
            int throws = 0;

            var result = KuiReflectionScanner.Scan<_TestMarkerAttribute>((mi, attr) =>
            {
                if (mi.DeclaringType != typeof(_Targets)) return;
                hits++;
                if (attr.Tag == "field")
                {
                    throws++;
                    throw new InvalidOperationException("synthetic visit failure");
                }
            });

            // Visitor was called for both members despite the first throwing.
            Assert.AreEqual(2, hits);
            Assert.AreEqual(1, throws);
            Assert.IsTrue(result.Errors.Count >= 1, "Throwing visitor must surface an error entry");
            bool found = false;
            for (int i = 0; i < result.Errors.Count; i++)
                if (result.Errors[i].Message != null
                 && result.Errors[i].Message.Contains("synthetic visit failure"))
                    found = true;
            Assert.IsTrue(found, "Error message must mention the original exception text");
        }

        [Test]
        public void Scan_NullVisitor_ReturnsEmptyResultNoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var result = KuiReflectionScanner.Scan<_TestMarkerAttribute>(null);
                Assert.AreEqual(0, result.MembersMatched);
                Assert.AreEqual(0, result.TypesScanned);
            });
        }

        [Test]
        public void Scan_TypesScannedCounter_Increments()
        {
            // The current AppDomain has many assemblies + many types — the
            // scanner should walk a non-trivial number of them.
            var result = KuiReflectionScanner.Scan<_TestMarkerAttribute>((_, __) => { });
            Assert.Greater(result.TypesScanned, 0);
        }

        [Test]
        public void Scan_PerAssemblyTryCatch_ToleratesBrokenAssembliesIfAny()
        {
            // This test asserts the scanner returns SUCCESSFULLY (no propagated
            // exception) regardless of whether the current AppDomain happens to
            // contain a broken assembly. Errors land in result.Errors instead.
            KuiReflectionResult r = default;
            Assert.DoesNotThrow(() =>
            {
                r = KuiReflectionScanner.Scan<_TestMarkerAttribute>((_, __) => { });
            });
            Assert.IsNotNull(r.Errors);
        }
    }
}
