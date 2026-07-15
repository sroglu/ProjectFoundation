using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.MeshTools;

namespace PFound.Utilities.MeshTools.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="MeshBuilder"/> and <see cref="MeshEditing"/>. Meshes
    /// created here are destroyed in <see cref="TearDown"/> to avoid leaking assets.
    /// </summary>
    public class MeshToolsTests
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();

        private Mesh Track(Mesh mesh)
        {
            _meshes.Add(mesh);
            return mesh;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] != null)
                    Object.DestroyImmediate(_meshes[i]);
            }
            _meshes.Clear();
        }

        // ---- Plane ----

        [Test]
        public void CreateXZPlane_SingleQuad_HasFourVerticesTwoTriangles()
        {
            Mesh mesh = Track(MeshBuilder.CreateXZPlane(2f, 2f));

            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
        }

        [Test]
        public void CreateXZPlane_IsCentredAndFlatOnY()
        {
            Mesh mesh = Track(MeshBuilder.CreateXZPlane(4f, 6f));

            Assert.AreEqual(Vector3.zero, mesh.bounds.center);
            Assert.AreEqual(4f, mesh.bounds.size.x, 0.001f);
            Assert.AreEqual(6f, mesh.bounds.size.z, 0.001f);
            Assert.AreEqual(0f, mesh.bounds.size.y, 0.001f);

            foreach (Vector3 normal in mesh.normals)
                Assert.AreEqual(Vector3.up, normal);
        }

        [Test]
        public void CreateXZPlane_Tessellation_ProducesGrid()
        {
            Mesh mesh = Track(MeshBuilder.CreateXZPlane(2f, 2f, 3, 2));

            Assert.AreEqual((3 + 1) * (2 + 1), mesh.vertexCount);
            Assert.AreEqual(3 * 2 * 6, mesh.triangles.Length);
        }

        // ---- Cylinder ----

        [Test]
        public void CreateCylinder_HasExpectedRadiusAndHeight()
        {
            Mesh mesh = Track(MeshBuilder.CreateCylinder(2f, 5f, 24));

            Assert.AreEqual(5f, mesh.bounds.size.y, 0.001f);
            Assert.AreEqual(4f, mesh.bounds.size.x, 0.01f);
            Assert.AreEqual(4f, mesh.bounds.size.z, 0.01f);
        }

        [Test]
        public void CreateCylinder_Capped_AddsMoreTrianglesThanOpen()
        {
            Mesh open = Track(MeshBuilder.CreateCylinder(1f, 2f, 12, capped: false));
            Mesh closed = Track(MeshBuilder.CreateCylinder(1f, 2f, 12, capped: true));

            Assert.Greater(closed.triangles.Length, open.triangles.Length);
        }

        [Test]
        public void CreateCylinder_ClampsRadialSegmentsToMinimumThree()
        {
            Mesh mesh = Track(MeshBuilder.CreateCylinder(1f, 2f, 1, capped: false));
            // 3 segments * 6 indices for the side wall.
            Assert.AreEqual(3 * 6, mesh.triangles.Length);
        }

        // ---- Scaling ----

        [Test]
        public void ScaleVertices_ExpandsBoundsComponentWise()
        {
            Mesh mesh = Track(MeshBuilder.CreateXZPlane(2f, 2f));
            mesh.ScaleVertices(new Vector3(3f, 1f, 2f));

            Assert.AreEqual(6f, mesh.bounds.size.x, 0.001f);
            Assert.AreEqual(4f, mesh.bounds.size.z, 0.001f);
        }

        [Test]
        public void ScaleVertices_Uniform_ReturnsSameInstance()
        {
            Mesh mesh = Track(MeshBuilder.CreateXZPlane(2f, 2f));
            Mesh result = mesh.ScaleVertices(2f);

            Assert.AreSame(mesh, result);
            Assert.AreEqual(4f, mesh.bounds.size.x, 0.001f);
        }

        // ---- Submesh extraction ----

        [Test]
        public void ExtractSubmesh_IsolatesOnlyThatSubmeshGeometry()
        {
            Mesh source = Track(BuildTwoSubmeshMesh());

            Mesh sub0 = Track(source.ExtractSubmesh(0));
            Mesh sub1 = Track(source.ExtractSubmesh(1));

            Assert.AreEqual(1, sub0.subMeshCount);
            Assert.AreEqual(1, sub1.subMeshCount);
            // Each source submesh is one triangle -> 3 unique verts, 3 indices.
            Assert.AreEqual(3, sub0.vertexCount);
            Assert.AreEqual(3, sub0.triangles.Length);
            Assert.AreEqual(3, sub1.vertexCount);

            // sub1 must have picked up the second triangle's vertices, not the first.
            Assert.IsTrue(System.Array.Exists(sub1.vertices, v => v == new Vector3(5f, 0f, 0f)));
            Assert.IsFalse(System.Array.Exists(sub1.vertices, v => v == new Vector3(0f, 0f, 0f)));
        }

        [Test]
        public void ExtractSubmesh_OutOfRange_Throws()
        {
            Mesh source = Track(BuildTwoSubmeshMesh());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => source.ExtractSubmesh(5));
        }

        private static Mesh BuildTwoSubmeshMesh()
        {
            var mesh = new Mesh { name = "TwoSub" };
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), // tri 0
                new Vector3(5f, 0f, 0f), new Vector3(6f, 0f, 0f), new Vector3(5f, 1f, 0f), // tri 1
            };
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
