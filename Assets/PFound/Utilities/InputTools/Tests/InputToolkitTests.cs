using NUnit.Framework;
using PFound.Utilities.InputTools;
using UnityEngine;

namespace PFound.Utilities.InputTools.Tests
{
    public class InputToolkitTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void RaycastPlane_RayPointingDown_HitsGroundBelowOrigin()
        {
            var ray = new Ray(new Vector3(3f, 10f, -4f), Vector3.down);
            var ground = new Plane(Vector3.up, Vector3.zero);

            bool hit = InputToolkit.RaycastPlane(ray, ground, out Vector3 point);

            Assert.IsTrue(hit);
            Assert.AreEqual(3f, point.x, Tolerance);
            Assert.AreEqual(0f, point.y, Tolerance);
            Assert.AreEqual(-4f, point.z, Tolerance);
        }

        [Test]
        public void RaycastPlane_ParallelRay_Misses()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.forward);
            var ground = new Plane(Vector3.up, Vector3.zero);

            bool hit = InputToolkit.RaycastPlane(ray, ground, out Vector3 point);

            Assert.IsFalse(hit);
            Assert.AreEqual(Vector3.zero, point);
        }

        [Test]
        public void RaycastGround_ProjectsCameraRayOntoRaisedGround()
        {
            var go = new GameObject("cam", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // looking straight down

            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool hit = InputToolkit.RaycastGround(camera, screenCenter, out Vector3 point, groundHeight: 2f);

            Assert.IsTrue(hit);
            Assert.AreEqual(2f, point.y, 1e-2f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PickObjectUnderScreenPoint_ReturnsColliderObject()
        {
            var camGo = new GameObject("cam", typeof(Camera));
            var camera = camGo.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            // A headless edit-mode run has no reliable Screen/game-view size, so ScreenPointToRay
            // would aim off-target. A render-texture target pins the camera to a known resolution.
            var rt = new RenderTexture(256, 256, 16);
            camera.targetTexture = rt;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            var screenCenter = new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f);
            GameObject picked = InputToolkit.PickObjectUnderScreenPoint(camera, screenCenter);

            Assert.AreSame(cube, picked);

            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(camGo);
        }

        [Test]
        public void TryGetTouch_NoTouches_ReturnsFalse()
        {
            bool found = InputToolkit.TryGetTouch(0, out Touch touch);

            Assert.IsFalse(found);
            Assert.AreEqual(default(Touch), touch);
        }

        [Test]
        public void InvertY_Vector2_NegatesYOnly()
        {
            Assert.AreEqual(new Vector2(3f, -7f), InputToolkit.InvertY(new Vector2(3f, 7f)));
        }

        [Test]
        public void InvertY_Vector3_NegatesYKeepsXZ()
        {
            Assert.AreEqual(new Vector3(3f, -7f, 5f), InputToolkit.InvertY(new Vector3(3f, 7f, 5f)));
        }

        [Test]
        public void FlipScreenY_MirrorsAgainstHeight()
        {
            Assert.AreEqual(new Vector2(50f, 800f), InputToolkit.FlipScreenY(new Vector2(50f, 200f), 1000f));
        }
    }
}
