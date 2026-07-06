using System;
using NUnit.Framework;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-025 / FR-033 — <see cref="OneShotGate"/> emits each <c>(batchId, kind)</c> tuple
    /// once. Different batches or different kinds emit independently.
    /// </summary>
    /// <remarks>
    /// <see cref="OneShotGate"/> is internal — visible via <c>InternalsVisibleTo</c> on the runtime
    /// asmdef.
    /// </remarks>
    public sealed class OneShotDiagnosticGatingTests
    {
        [Test]
        public void SameBatchSameKind_EmitsOnce()
        {
            var gate = new OneShotGate();
            var id = Guid.NewGuid();
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.MissingEnableInstancing));
            Assert.IsFalse(gate.TryEmit(id, DiagnosticKind.MissingEnableInstancing));
            Assert.IsFalse(gate.TryEmit(id, DiagnosticKind.MissingEnableInstancing));
            Assert.AreEqual(1, gate.RecordedCount);
        }

        [Test]
        public void SameBatchDifferentKinds_EmitIndependently()
        {
            var gate = new OneShotGate();
            var id = Guid.NewGuid();
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.MissingEnableInstancing));
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.BackendUnsupported));
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.OcclusionStubActive));
            Assert.AreEqual(3, gate.RecordedCount);
        }

        [Test]
        public void DifferentBatchesSameKind_EmitIndependently()
        {
            var gate = new OneShotGate();
            Assert.IsTrue(gate.TryEmit(Guid.NewGuid(), DiagnosticKind.ZeroCountFirstSeen));
            Assert.IsTrue(gate.TryEmit(Guid.NewGuid(), DiagnosticKind.ZeroCountFirstSeen));
            Assert.IsTrue(gate.TryEmit(Guid.NewGuid(), DiagnosticKind.ZeroCountFirstSeen));
            Assert.AreEqual(3, gate.RecordedCount);
        }

        [Test]
        public void Forget_ResetsGatingForGivenBatch()
        {
            var gate = new OneShotGate();
            var id = Guid.NewGuid();
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.InvalidSource));
            Assert.IsFalse(gate.TryEmit(id, DiagnosticKind.InvalidSource));
            gate.Forget(id);
            Assert.IsTrue(gate.TryEmit(id, DiagnosticKind.InvalidSource));
        }
    }
}
