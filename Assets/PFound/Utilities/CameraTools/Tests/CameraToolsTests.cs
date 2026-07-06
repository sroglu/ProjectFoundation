using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.CameraTools;

namespace PFound.Utilities.CameraTools.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="CameraVisibility"/>. A camera is aimed down +Z and
    /// objects are placed inside / behind / far off-axis to exercise the frustum test. All
    /// spawned objects are destroyed in <see cref="TearDown"/>.
    /// </summary>
    public class CameraToolsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Camera _camera;

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            GameObject camObject = Track(new GameObject("Cam"));
            _camera = camObject.AddComponent<Camera>();
            _camera.transform.position = Vector3.zero;
            _camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 60f;
            _camera.aspect = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        private GameObject Cube(Vector3 position)
        {
            GameObject cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.transform.position = position;
            return cube;
        }

        [Test]
        public void Renderer_InFront_IsSeen()
        {
            Renderer renderer = Cube(new Vector3(0f, 0f, 10f)).GetComponent<Renderer>();
            Assert.IsTrue(renderer.IsSeenBy(_camera));
        }

        [Test]
        public void Renderer_Behind_IsNotSeen()
        {
            Renderer renderer = Cube(new Vector3(0f, 0f, -10f)).GetComponent<Renderer>();
            Assert.IsFalse(renderer.IsSeenBy(_camera));
        }

        [Test]
        public void Renderer_FarOffAxis_IsNotSeen()
        {
            Renderer renderer = Cube(new Vector3(500f, 0f, 10f)).GetComponent<Renderer>();
            Assert.IsFalse(renderer.IsSeenBy(_camera));
        }

        [Test]
        public void Collider_InFront_IsSeen()
        {
            Collider collider = Cube(new Vector3(0f, 0f, 12f)).GetComponent<Collider>();
            Assert.IsTrue(collider.IsSeenBy(_camera));
        }

        [Test]
        public void Bounds_InsideFrustum_MatchesCameraOverload()
        {
            var bounds = new Bounds(new Vector3(0f, 0f, 15f), Vector3.one);
            Assert.IsTrue(CameraVisibility.IsBoundsInside(bounds, _camera));
        }

        [Test]
        public void Bounds_TestedAgainstPrebakedPlanes_MatchesCameraResult()
        {
            var inside = new Bounds(new Vector3(0f, 0f, 15f), Vector3.one);
            var behind = new Bounds(new Vector3(0f, 0f, -15f), Vector3.one);

            Plane[] planes = CameraVisibility.CollectFrustumPlanes(_camera);

            Assert.AreEqual(6, planes.Length);
            Assert.IsTrue(CameraVisibility.IsBoundsInside(inside, planes));
            Assert.IsFalse(CameraVisibility.IsBoundsInside(behind, planes));
        }

        [Test]
        public void CollectFrustumPlanes_FillsProvidedArrayWithoutAllocating()
        {
            var planes = new Plane[6];
            CameraVisibility.CollectFrustumPlanes(_camera, planes);

            var inside = new Bounds(new Vector3(0f, 0f, 15f), Vector3.one);
            Assert.IsTrue(CameraVisibility.IsBoundsInside(inside, planes));
        }

        [Test]
        public void Renderer_SeenBy_PrebakedPlanesMatchesCameraOverload()
        {
            Renderer renderer = Cube(new Vector3(0f, 0f, 8f)).GetComponent<Renderer>();
            Plane[] planes = CameraVisibility.CollectFrustumPlanes(_camera);

            Assert.AreEqual(renderer.IsSeenBy(_camera), renderer.IsSeenBy(planes));
        }
    }
}
