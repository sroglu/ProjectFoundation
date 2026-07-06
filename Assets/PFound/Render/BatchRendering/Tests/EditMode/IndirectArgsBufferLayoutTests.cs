using NUnit.Framework;
using UnityEngine;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers research.md R1 — <see cref="IndirectBackendState"/> pre-authors the
    /// <c>GraphicsBuffer.IndirectDrawIndexedArgs</c> args layout from the mesh / subMesh at register
    /// time. Phase 11 writes only the <c>instanceCount</c> slot per tick (zero managed alloc).
    /// </summary>
    [TestFixture]
    public sealed class IndirectArgsBufferLayoutTests
    {
        private Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            // Unit triangle mesh with 1 sub-mesh.
            _mesh = new Mesh { name = "__args_layout_test_mesh__" };
            _mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            _mesh.triangles = new[] { 0, 1, 2 };
            _mesh.RecalculateBounds();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mesh != null) Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void RegisterTime_ArgsCapturesMeshIndexCount()
        {
            if (!BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host platform does not support indirect rendering.");
                return;
            }

            var state = new IndirectBackendState(_mesh, 0);
            try
            {
                Assert.AreEqual(_mesh.GetIndexCount(0), state.ArgsIndexCountPerInstance);
                Assert.AreEqual(_mesh.GetIndexStart(0), state.ArgsStartIndex);
                Assert.AreEqual((uint)_mesh.GetBaseVertex(0), state.ArgsBaseVertexIndex);
            }
            finally { state.Dispose(); }
        }

        [Test]
        public void RegisterTime_ScratchHasCorrectInitialValues()
        {
            if (!BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host platform does not support indirect rendering.");
                return;
            }

            var state = new IndirectBackendState(_mesh, 0);
            try
            {
                var scratch = state.ArgsScratch;
                Assert.AreEqual(5, scratch.Length);
                Assert.AreEqual(_mesh.GetIndexCount(0), scratch[0]); // indexCountPerInstance
                Assert.AreEqual(0u, scratch[1]);                       // instanceCount (pre-tick)
                Assert.AreEqual(_mesh.GetIndexStart(0), scratch[2]);    // startIndex
                Assert.AreEqual((uint)_mesh.GetBaseVertex(0), scratch[3]); // baseVertexIndex
                Assert.AreEqual(0u, scratch[4]);                       // startInstance
            }
            finally { state.Dispose(); }
        }

        [Test]
        public void Dispose_ReleasesArgsBuffer_Idempotent()
        {
            if (!BackendCapabilityProbe.SupportsIndirect)
            {
                Assert.Ignore("Host platform does not support indirect rendering.");
                return;
            }

            var state = new IndirectBackendState(_mesh, 0);
            Assert.DoesNotThrow(() => state.Dispose());
            // Second dispose should be safe (defensive — the IDisposable contract).
            Assert.DoesNotThrow(() => state.Dispose());
        }
    }
}
