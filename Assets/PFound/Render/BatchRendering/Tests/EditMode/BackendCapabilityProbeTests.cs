using NUnit.Framework;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-023a — capability probe identifies platform support. Cannot mock <c>SystemInfo</c>
    /// without an indirection layer; this test pins the basic invariants on the real platform we're
    /// running on (PC editor → supports indirect; field is reflective of the host).
    /// </summary>
    public sealed class BackendCapabilityProbeTests
    {
        [Test]
        public void ClassicPerCallCap_Is1023()
        {
            Assert.AreEqual(1023, BackendCapabilityProbe.ClassicPerCallCap,
                "Hard-coded per research.md R5; safe minimum across all target platforms.");
        }

        [Test]
        public void SupportsIndirect_IsBoolean_NoThrow()
        {
            // First-access lazy probe should not throw.
            Assert.DoesNotThrow(() => { var _ = BackendCapabilityProbe.SupportsIndirect; });
        }

        [Test]
        public void SupportsProcedural_MatchesIndirectInPhase11()
        {
            // Per research.md R2, Procedural uses the same gate as Indirect in v1.
            Assert.AreEqual(BackendCapabilityProbe.SupportsIndirect, BackendCapabilityProbe.SupportsProcedural);
        }

        [Test]
        public void MissingCapability_EmptyWhenSupported_NonEmptyWhenNot()
        {
            // Reading the property is always safe; its content reflects support state.
            string missing = BackendCapabilityProbe.MissingCapability;
            if (BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.IsTrue(string.IsNullOrEmpty(missing),
                    "Supported platforms should report empty MissingCapability.");
            }
            else
            {
                Assert.IsFalse(string.IsNullOrEmpty(missing),
                    "Unsupported platforms should name the missing capability.");
            }
        }

        [Test]
        public void RepeatedAccess_ReturnsSameValue_Cached()
        {
            // Probe is cached after first access.
            bool first = BackendCapabilityProbe.SupportsIndirect;
            bool second = BackendCapabilityProbe.SupportsIndirect;
            bool third = BackendCapabilityProbe.SupportsIndirect;
            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
        }
    }
}
