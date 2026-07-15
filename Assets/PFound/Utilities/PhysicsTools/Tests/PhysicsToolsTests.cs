using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.PhysicsTools;

namespace PFound.Utilities.PhysicsTools.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="PhysicsQueries"/> and
    /// <see cref="RigidbodyMotionExtensions"/>. Velocities are assigned directly (no
    /// simulation runs in edit mode) and colliders are synced before raycasting. Everything
    /// spawned is destroyed in <see cref="TearDown"/>.
    /// </summary>
    public class PhysicsToolsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
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

        private Rigidbody NewBody()
        {
            GameObject go = Track(new GameObject("Body"));
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            return body;
        }

        // ---- Raycast excluding ----

        [Test]
        public void RaycastExcluding_SkipsExcludedHierarchyAndHitsBeyond()
        {
            GameObject self = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            self.transform.position = Vector3.zero;

            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();

            bool hitSomething = PhysicsQueries.RaycastExcluding(
                new Vector3(0f, 0f, 0f), Vector3.down, self, out RaycastHit hit, 50f);

            Assert.IsTrue(hitSomething);
            Assert.AreEqual(floor.transform, hit.collider.transform);
        }

        [Test]
        public void RaycastExcluding_WithoutExclusion_WouldHitSelf()
        {
            GameObject self = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            self.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            // Excluding null means nothing is filtered: the ray from above hits the cube itself.
            bool hitSomething = PhysicsQueries.RaycastExcluding(
                new Vector3(0f, 5f, 0f), Vector3.down, null, out RaycastHit hit, 50f);

            Assert.IsTrue(hitSomething);
            Assert.AreEqual(self.transform, hit.collider.transform);
        }

        [Test]
        public void RaycastExcluding_ExcludesChildColliders()
        {
            // Isolate on a dedicated layer so only this test's collider is a raycast candidate —
            // the assertion is "no hit remains after exclusion", which any stray collider left in
            // the shared edit-mode physics scene by another test would otherwise break.
            const int testLayer = 31;
            GameObject root = Track(new GameObject("Root"));
            GameObject childCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            childCollider.layer = testLayer;
            childCollider.transform.SetParent(root.transform);
            childCollider.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            bool hitSomething = PhysicsQueries.RaycastExcluding(
                new Vector3(0f, 5f, 0f), Vector3.down, root, out _, 50f, 1 << testLayer);

            Assert.IsFalse(hitSomething);
        }

        // ---- Rigidbody motion ----

        [Test]
        public void ResetMotion_ZeroesLinearAndAngular()
        {
            Rigidbody body = NewBody();
            body.linearVelocity = new Vector3(1f, 2f, 3f);
            body.angularVelocity = new Vector3(4f, 5f, 6f);

            body.ResetMotion();

            Assert.AreEqual(Vector3.zero, body.linearVelocity);
            Assert.AreEqual(Vector3.zero, body.angularVelocity);
        }

        [Test]
        public void HorizontalSpeed_IgnoresVerticalComponent()
        {
            Rigidbody body = NewBody();
            body.linearVelocity = new Vector3(3f, 100f, 4f);

            Assert.AreEqual(5f, body.HorizontalSpeed(), 0.001f);
        }

        [Test]
        public void ForwardSpeed_ProjectsOntoFacingAxis()
        {
            Rigidbody body = NewBody();
            body.linearVelocity = new Vector3(3f, 0f, 4f);

            // Facing +Z: forward speed is the Z component.
            Assert.AreEqual(4f, body.ForwardSpeed(), 0.001f);

            // Rotate to face +X: forward speed becomes the X component.
            body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.AreEqual(3f, body.ForwardSpeed(), 0.001f);
        }

        [Test]
        public void ForwardSpeed_IsNegativeWhenReversing()
        {
            Rigidbody body = NewBody();
            body.linearVelocity = new Vector3(0f, 0f, -6f);

            Assert.Less(body.ForwardSpeed(), 0f);
        }

        [Test]
        public void HorizontalForwardSpeed_FlattensPitchBeforeProjecting()
        {
            Rigidbody body = NewBody();
            // Pitched down 45 degrees, but we only want ground-plane forward speed.
            body.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            body.linearVelocity = new Vector3(0f, 0f, 10f);

            // Horizontal velocity (0,0,10) along horizontal facing (0,0,1) => 10.
            Assert.AreEqual(10f, body.HorizontalForwardSpeed(), 0.001f);
        }

        [Test]
        public void LocalAngularVelocity_ConvertsWorldSpinIntoLocalFrame()
        {
            Rigidbody body = NewBody();
            body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            body.angularVelocity = new Vector3(0f, 0f, 2f); // world +Z spin

            Vector3 local = body.LocalAngularVelocity();

            // Under a +90 yaw the inverse rotation maps world +Z onto local -X.
            Assert.AreEqual(-2f, local.x, 0.001f);
            Assert.AreEqual(0f, local.z, 0.001f);
            Assert.AreEqual(2f, local.magnitude, 0.001f);
        }
    }
}
