using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.GameObjectTools;

namespace PFound.Utilities.GameObjectTools.Tests
{
    /// <summary>
    /// EditMode coverage for the GameObjectTools helpers. Every spawned object is
    /// tracked and destroyed in <see cref="TearDown"/> so the scene stays clean.
    /// </summary>
    public class GameObjectToolsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        private GameObject NewRoot(string name = "Root")
        {
            return Track(new GameObject(name));
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

        // ---- Creation ----

        [Test]
        public void GetOrCreateChild_CreatesWhenMissing()
        {
            GameObject root = NewRoot();
            GameObject child = root.transform.GetOrCreateChild("Slot");

            Assert.IsNotNull(child);
            Assert.AreEqual("Slot", child.name);
            Assert.AreEqual(root.transform, child.transform.parent);
        }

        [Test]
        public void GetOrCreateChild_ReturnsExistingInsteadOfDuplicating()
        {
            GameObject root = NewRoot();
            GameObject first = root.transform.GetOrCreateChild("Slot");
            GameObject second = root.transform.GetOrCreateChild("Slot");

            Assert.AreSame(first, second);
            Assert.AreEqual(1, root.transform.childCount);
        }

        [Test]
        public void CreateWithComponents_AddsRequestedComponents()
        {
            GameObject go = Track(GameObjectFactory.CreateWithComponents("Rigid", typeof(Rigidbody), typeof(BoxCollider)));

            Assert.IsTrue(go.HasComponent<Rigidbody>());
            Assert.IsTrue(go.HasComponent<BoxCollider>());
        }

        [Test]
        public void CreatePrimitive_ParentsUnderGivenTransform()
        {
            GameObject root = NewRoot();
            GameObject cube = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube, root.transform));

            Assert.AreEqual(root.transform, cube.transform.parent);
            Assert.IsTrue(cube.HasComponent<MeshFilter>());
        }

        // ---- Recursive ops ----

        [Test]
        public void SetLayerRecursively_AppliesToWholeSubtree()
        {
            GameObject root = NewRoot();
            GameObject child = root.transform.GetOrCreateChild("A");
            GameObject grandChild = child.transform.GetOrCreateChild("B");

            root.SetLayerRecursively(5);

            Assert.AreEqual(5, root.layer);
            Assert.AreEqual(5, child.layer);
            Assert.AreEqual(5, grandChild.layer);
        }

        [Test]
        public void SetRenderersEnabled_TogglesEveryRenderer()
        {
            GameObject root = NewRoot();
            GameObject a = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube, root.transform));
            GameObject b = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Sphere, root.transform));

            root.SetRenderersEnabled(false);

            Assert.IsFalse(a.GetComponent<Renderer>().enabled);
            Assert.IsFalse(b.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void SetRenderersColor_WritesPropertyBlock()
        {
            GameObject root = NewRoot();
            GameObject cube = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube, root.transform));

            root.SetRenderersColor(Color.red);

            var block = new MaterialPropertyBlock();
            cube.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(Color.red, block.GetColor(Shader.PropertyToID("_BaseColor")));
        }

        // ---- Bounds ----

        [Test]
        public void GetRendererBounds_EncapsulatesAllRenderers()
        {
            GameObject root = NewRoot();
            GameObject cube = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube, root.transform));
            cube.transform.position = new Vector3(10f, 0f, 0f);

            Bounds bounds = root.transform.GetRendererBounds();

            Assert.IsTrue(bounds.Contains(new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void GetColliderBounds_EncapsulatesAllColliders()
        {
            GameObject root = NewRoot();
            GameObject cube = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube, root.transform));
            cube.transform.position = new Vector3(-4f, 0f, 0f);
            // Collider.bounds reads from the physics representation, which lags the transform until
            // a sync — without this the collider still reports its spawn position (origin).
            Physics.SyncTransforms();

            Bounds bounds = root.transform.GetColliderBounds();

            Assert.IsTrue(bounds.Contains(new Vector3(-4f, 0f, 0f)));
        }

        // ---- Hierarchy queries ----

        [Test]
        public void IsDescendantOf_And_IsParentOf_AreConsistent()
        {
            GameObject root = NewRoot();
            GameObject child = root.transform.GetOrCreateChild("A");
            GameObject grandChild = child.transform.GetOrCreateChild("B");

            Assert.IsTrue(grandChild.transform.IsDescendantOf(root.transform));
            Assert.IsTrue(root.transform.IsParentOf(grandChild.transform));
            Assert.IsFalse(root.transform.IsDescendantOf(root.transform));
        }

        [Test]
        public void FindChildByName_SearchesRecursively()
        {
            GameObject root = NewRoot();
            GameObject child = root.transform.GetOrCreateChild("A");
            GameObject target = child.transform.GetOrCreateChild("Target");

            Transform found = root.transform.FindChildByName("Target");

            Assert.AreEqual(target.transform, found);
        }

        [Test]
        public void FindChildStartingEndingContaining_MatchByAffix()
        {
            GameObject root = NewRoot();
            GameObject a = root.transform.GetOrCreateChild("Enemy_01");
            GameObject b = a.transform.GetOrCreateChild("Prop_Table");

            Assert.AreEqual(a.transform, root.transform.FindChildStartingWith("Enemy"));
            Assert.AreEqual(a.transform, root.transform.FindChildEndingWith("_01"));
            Assert.AreEqual(b.transform, root.transform.FindChildContaining("Table"));
        }

        [Test]
        public void FindChildrenStartingWith_ReturnsAllMatches()
        {
            GameObject root = NewRoot();
            root.transform.GetOrCreateChild("Enemy_A");
            root.transform.GetOrCreateChild("Enemy_B");
            root.transform.GetOrCreateChild("Ally_C");

            List<Transform> matches = root.transform.FindChildrenStartingWith("Enemy");

            Assert.AreEqual(2, matches.Count);
        }

        [Test]
        public void GetDirectChildren_ReturnsOnlyImmediate()
        {
            GameObject root = NewRoot();
            GameObject a = root.transform.GetOrCreateChild("A");
            a.transform.GetOrCreateChild("Deep");
            root.transform.GetOrCreateChild("B");

            List<Transform> children = root.transform.GetDirectChildren();

            Assert.AreEqual(2, children.Count);
        }

        [Test]
        public void ForEachDirectChild_VisitsEach()
        {
            GameObject root = NewRoot();
            root.transform.GetOrCreateChild("A");
            root.transform.GetOrCreateChild("B");

            int visits = 0;
            root.transform.ForEachDirectChild(_ => visits++);

            Assert.AreEqual(2, visits);
        }

        // ---- Transform helpers ----

        [Test]
        public void CenterOnChildren_MovesPivotToCentroidKeepingChildrenFixed()
        {
            GameObject root = NewRoot();
            GameObject a = root.transform.GetOrCreateChild("A");
            GameObject b = root.transform.GetOrCreateChild("B");
            a.transform.position = new Vector3(0f, 0f, 0f);
            b.transform.position = new Vector3(4f, 0f, 0f);

            root.transform.CenterOnChildren();

            Assert.AreEqual(new Vector3(2f, 0f, 0f), root.transform.position);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), a.transform.position);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), b.transform.position);
        }

        [Test]
        public void ResetLocal_RestoresIdentityTRS()
        {
            GameObject go = NewRoot();
            go.transform.localPosition = new Vector3(1f, 2f, 3f);
            go.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            go.transform.localScale = new Vector3(5f, 5f, 5f);

            go.transform.ResetLocal();

            Assert.AreEqual(Vector3.zero, go.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, go.transform.localRotation);
            Assert.AreEqual(Vector3.one, go.transform.localScale);
        }

        [Test]
        public void SnapToGround_PlacesOnColliderSurface()
        {
            GameObject floor = Track(GameObjectFactory.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();

            GameObject mover = NewRoot("Mover");
            mover.transform.position = new Vector3(0f, 8f, 0f);

            bool grounded = mover.transform.SnapToGround(50f);

            Assert.IsTrue(grounded);
            // Top of the floor sits at y = 0.5 (half of unit cube scaled by 1 on Y).
            Assert.AreEqual(0.5f, mover.transform.position.y, 0.01f);
        }

        [Test]
        public void SnapToGround_ReturnsFalseWhenNothingBelow()
        {
            GameObject mover = NewRoot("Mover");
            mover.transform.position = new Vector3(1000f, 1000f, 1000f);

            bool grounded = mover.transform.SnapToGround(5f);

            Assert.IsFalse(grounded);
        }

        [Test]
        public void DestroyChildren_ClearsSubtree()
        {
            GameObject root = NewRoot();
            root.transform.GetOrCreateChild("A");
            root.transform.GetOrCreateChild("B");

            root.transform.DestroyChildren();

            Assert.AreEqual(0, root.transform.childCount);
        }

        // ---- Component helpers ----

        [Test]
        public void HasComponent_ReflectsPresence()
        {
            GameObject go = NewRoot();
            Assert.IsFalse(go.HasComponent<Rigidbody>());
            go.AddComponent<Rigidbody>();
            Assert.IsTrue(go.HasComponent<Rigidbody>());
        }

        [Test]
        public void IsBehaviourEnabled_HandlesNullAndState()
        {
            GameObject go = NewRoot();
            var light = go.AddComponent<Light>();

            Assert.IsTrue(light.IsBehaviourEnabled());
            light.enabled = false;
            Assert.IsFalse(light.IsBehaviourEnabled());

            Behaviour nothing = null;
            Assert.IsFalse(nothing.IsBehaviourEnabled());
        }
    }
}
