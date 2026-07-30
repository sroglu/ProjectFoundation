using NUnit.Framework;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers SC-006 (classic backend draw-call shape) — <see cref="ClassicBackendState.ComputeChunkCount"/>
    /// math. The actual draw-call count is asserted in PlayMode tests via Frame Debugger; this
    /// EditMode test pins the chunking math.
    /// </summary>
    public sealed class ClassicBackendChunkingTests
    {
        [Test]
        public void ZeroVisible_ReturnsZeroChunks()
        {
            Assert.AreEqual(0, ClassicBackendState.ComputeChunkCount(0, 1023));
            Assert.AreEqual(0, ClassicBackendState.ComputeChunkCount(-1, 1023));
        }

        [Test]
        public void OneVisible_ReturnsOneChunk()
        {
            Assert.AreEqual(1, ClassicBackendState.ComputeChunkCount(1, 1023));
        }

        [Test]
        public void BelowCap_ReturnsOneChunk()
        {
            Assert.AreEqual(1, ClassicBackendState.ComputeChunkCount(500, 1023));
            Assert.AreEqual(1, ClassicBackendState.ComputeChunkCount(1023, 1023));
        }

        [Test]
        public void ExactlyCapPlusOne_ReturnsTwoChunks()
        {
            Assert.AreEqual(2, ClassicBackendState.ComputeChunkCount(1024, 1023));
        }

        [Test]
        public void ThreeChunkBoundary()
        {
            // ⌈3000 / 1023⌉ = 3
            Assert.AreEqual(3, ClassicBackendState.ComputeChunkCount(3000, 1023));
            // ⌈3069 / 1023⌉ = 3 (3069 = 3 × 1023)
            Assert.AreEqual(3, ClassicBackendState.ComputeChunkCount(3069, 1023));
            // ⌈3070 / 1023⌉ = 4
            Assert.AreEqual(4, ClassicBackendState.ComputeChunkCount(3070, 1023));
        }

        [Test]
        public void ZeroCap_ReturnsZeroChunks()
        {
            // Defensive — never crash even if some future patch passes a bad cap.
            Assert.AreEqual(0, ClassicBackendState.ComputeChunkCount(1000, 0));
            Assert.AreEqual(0, ClassicBackendState.ComputeChunkCount(1000, -1));
        }
    }
}
