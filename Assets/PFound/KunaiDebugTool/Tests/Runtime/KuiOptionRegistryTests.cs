using System;
using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiOptionRegistryTests
    {
        // Distinctive names so we can probe the AppDomain without colliding
        // with real shipping options.
        static class _OptionTargets
        {
            [KuiOption,                                  KuiCategory("KunaiTests")]
            public static bool   KunaiTestBool;

            [KuiOption("Custom Label"),                  KuiCategory("KunaiTests")]
            public static int    KunaiTestInt = 5;

            [KuiOption,           KuiRange(0f, 10f),     KuiCategory("KunaiTests")]
            public static float  KunaiTestFloatRanged = 3f;

            [KuiOption,                                  KuiCategory("KunaiTests")]
            public static float  KunaiTestFloat = 1.5f;

            // Unsupported type — should NOT register, only warn.
            [KuiOption,                                  KuiCategory("KunaiTests")]
            public static UnityEngine.Vector3 KunaiTestUnsupported;

            [KuiOption,                                  KuiCategory("KunaiTests")]
            public static SampleEnum Sample;

            // No category attribute → default group.
            [KuiOption]
            public static string KunaiTestUncategorised = "x";

            public enum SampleEnum { A, B, C }
        }

        // Separate non-static fixture so we can declare an instance field
        // that the scanner is supposed to skip.
        class _InstanceTargets
        {
            [KuiOption]
            public bool KunaiTestInstanceFieldShouldSkip;
        }

        static KuiOptionEntry FindByLabel(KuiOptionRegistry reg, string category, string label)
        {
            var rows = reg.InCategory(category);
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Label == label) return rows[i];
            return null;
        }

        [Test]
        public void BuildFromScan_RegistersSupportedTypes()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();

            Assert.IsNotNull(FindByLabel(reg, "KunaiTests", "KunaiTestBool"));
            Assert.IsNotNull(FindByLabel(reg, "KunaiTests", "Custom Label"),  "Label override should override member name");
            Assert.IsNotNull(FindByLabel(reg, "KunaiTests", "KunaiTestFloatRanged"));
            Assert.IsNotNull(FindByLabel(reg, "KunaiTests", "KunaiTestFloat"));
            Assert.IsNotNull(FindByLabel(reg, "KunaiTests", "Sample"));
        }

        [Test]
        public void BuildFromScan_SkipsUnsupportedType()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();
            Assert.IsNull(FindByLabel(reg, "KunaiTests", "KunaiTestUnsupported"));
        }

        [Test]
        public void BuildFromScan_SkipsInstanceField()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();
            // Instance fields don't have a category attached, but they shouldn't
            // appear under any category in the registry.
            foreach (var c in reg.Categories)
            {
                var rows = reg.InCategory(c);
                for (int i = 0; i < rows.Count; i++)
                    Assert.AreNotEqual("KunaiTestInstanceFieldShouldSkip", rows[i].Label);
            }
        }

        [Test]
        public void BuildFromScan_RangeAttributePersists()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();
            var e = FindByLabel(reg, "KunaiTests", "KunaiTestFloatRanged");
            Assert.IsNotNull(e);
            Assert.AreEqual(0d,  e.Min);
            Assert.AreEqual(10d, e.Max);
        }

        [Test]
        public void BuildFromScan_NoCategory_FallsToDefault()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();
            var e = FindByLabel(reg, KuiOptionRegistry.DefaultCategory, "KunaiTestUncategorised");
            Assert.IsNotNull(e, "Members without [KuiCategory] should land under 'Default'");
        }

        [Test]
        public void GetSet_RoundTripsThroughDelegates()
        {
            var reg = new KuiOptionRegistry();
            reg.BuildFromScan();
            // KunaiTestInt has [KuiOption("Custom Label")] — label override wins (see line 17).
            var e = FindByLabel(reg, "KunaiTests", "Custom Label");
            Assert.IsNotNull(e);

            int original = (int)e.Get();
            try
            {
                e.Set(123);
                Assert.AreEqual(123, _OptionTargets.KunaiTestInt);
                Assert.AreEqual(123, (int)e.Get());
            }
            finally
            {
                _OptionTargets.KunaiTestInt = original;
            }
        }

        [Test]
        public void IsSupportedType_CoversAllExpectedTypes()
        {
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(bool)));
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(int)));
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(float)));
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(double)));
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(string)));
            Assert.IsTrue (KuiOptionRegistry.IsSupportedType(typeof(KuiLogLevel)));
            Assert.IsFalse(KuiOptionRegistry.IsSupportedType(typeof(UnityEngine.Vector3)));
            Assert.IsFalse(KuiOptionRegistry.IsSupportedType(typeof(byte[])));
        }

        [Test]
        public void RangeAttribute_RejectsInvertedRange()
        {
            Assert.Throws<ArgumentException>(() => new KuiRangeAttribute(10, 5));
        }
    }
}
