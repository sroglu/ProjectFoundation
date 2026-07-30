using NUnit.Framework;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-026 — <see cref="CullingPolicy"/> sentinel semantics. <see cref="CullingPolicy.Default"/>
    /// enables frustum culling; <see cref="CullingPolicy.None"/> disables all stages;
    /// <c>default(CullingPolicy)</c> is the all-off zero struct (matches <c>None</c> field-wise).
    /// </summary>
    public sealed class CullingPolicySentinelTests
    {
        [Test]
        public void Default_HasFrustumOn_DistanceOff_OcclusionOff()
        {
            var p = CullingPolicy.Default;
            Assert.IsTrue(p.frustum);
            Assert.IsFalse(p.distance.enabled);
            Assert.IsFalse(p.occlusion);
        }

        [Test]
        public void None_HasAllStagesOff()
        {
            var p = CullingPolicy.None;
            Assert.IsFalse(p.frustum);
            Assert.IsFalse(p.distance.enabled);
            Assert.IsFalse(p.occlusion);
        }

        [Test]
        public void DefaultStruct_EqualsNoneFieldWise()
        {
            // default(CullingPolicy) is the all-zero struct; semantically equivalent to None.
            var d = default(CullingPolicy);
            var none = CullingPolicy.None;
            Assert.AreEqual(none.frustum, d.frustum);
            Assert.AreEqual(none.distance.enabled, d.distance.enabled);
            Assert.AreEqual(none.distance.maxDistance, d.distance.maxDistance);
            Assert.AreEqual(none.occlusion, d.occlusion);
        }

        [Test]
        public void DistanceConfig_RespectsExplicitValues()
        {
            var p = new CullingPolicy
            {
                frustum = true,
                distance = new DistanceCullingConfig { enabled = true, maxDistance = 250f },
                occlusion = false,
            };
            Assert.IsTrue(p.distance.enabled);
            Assert.AreEqual(250f, p.distance.maxDistance);
        }
    }
}
