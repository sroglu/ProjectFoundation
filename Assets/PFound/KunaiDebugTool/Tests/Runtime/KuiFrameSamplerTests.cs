using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiFrameSamplerTests
    {
        [Test]
        public void Create_AllocatesCapacityArrays()
        {
            var s = KuiFrameSampler.Create(64);
            try
            {
                Assert.AreEqual(64, s.Capacity);
                Assert.AreEqual(64, s.FrameTimesMs.Length);
                Assert.AreEqual(64, s.GcDeltasKb.Length);
                Assert.IsTrue(s.IsCreated);
                Assert.AreEqual(0, s.Count);
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void Sample_AdvancesHeadAndCount_UntilCapacity()
        {
            var s = KuiFrameSampler.Create(8);
            try
            {
                for (int i = 0; i < 5; i++) s.Sample(0.016f);
                Assert.AreEqual(5, s.Count);
                Assert.AreEqual(5, s.Head);
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void Sample_RingWraps_KeepsCountAtCapacity()
        {
            var s = KuiFrameSampler.Create(4);
            try
            {
                for (int i = 0; i < 10; i++) s.Sample(0.016f);
                Assert.AreEqual(4, s.Count);
                Assert.AreEqual(2, s.Head, "head wraps mod capacity");
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void Sample_SmoothedFps_MatchesConstantInputAfterWindow()
        {
            var s = KuiFrameSampler.Create(16);
            try
            {
                // 60 FPS = 16.666 ms
                for (int i = 0; i < 8; i++) s.Sample(0.016666f);
                Assert.That(s.LastSmoothedFrameMs, Is.EqualTo(16.666f).Within(0.05f));
                Assert.That(s.LastSmoothedFps,     Is.EqualTo(60f).Within(0.5f));
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void CopyOldestFirst_ReturnsInArrivalOrder_AfterWrap()
        {
            var s = KuiFrameSampler.Create(4);
            try
            {
                // After 6 samples we have written ms = 1,2,3,4,5,6.
                // Ring keeps the last 4 → 3,4,5,6 in arrival order.
                for (int i = 1; i <= 6; i++) s.Sample(i / 1000f);   // ms 1..6
                var dst = new float[4];
                int n = s.CopyOldestFirst(s.FrameTimesMs, dst);
                Assert.AreEqual(4, n);
                Assert.That(dst[0], Is.EqualTo(3f).Within(0.001f));
                Assert.That(dst[1], Is.EqualTo(4f).Within(0.001f));
                Assert.That(dst[2], Is.EqualTo(5f).Within(0.001f));
                Assert.That(dst[3], Is.EqualTo(6f).Within(0.001f));
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void MaxFrameTimeMs_WindowOverRing()
        {
            var s = KuiFrameSampler.Create(4);
            try
            {
                s.Sample(0.005f);
                s.Sample(0.020f);
                s.Sample(0.010f);
                Assert.That(s.MaxFrameTimeMs(), Is.EqualTo(20f).Within(0.001f));
            }
            finally { s.Dispose(); }
        }

        [Test]
        public void Dispose_IdempotentSafeCalls()
        {
            var s = KuiFrameSampler.Create(2);
            s.Dispose();
            // Second dispose should NOT throw — IsCreated guards it.
            Assert.DoesNotThrow(() => s.Dispose());
        }
    }
}
