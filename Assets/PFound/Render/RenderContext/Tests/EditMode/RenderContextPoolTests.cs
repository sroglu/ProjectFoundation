using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RenderContextPoolTests
    {
        private GameObject _ownerGo;
        private RenderContextPool _pool;

        [SetUp]
        public void Setup()
        {
            _ownerGo = new GameObject("__poolOwner");
            _pool = new RenderContextPool(_ownerGo.transform);
        }

        [TearDown]
        public void Teardown()
        {
            _pool.Dispose();
            if (_ownerGo != null) UnityEngine.Object.DestroyImmediate(_ownerGo);
        }

        [Test]
        public void Lease_OnMiss_AllocatesNewEntry()
        {
            var d = RenderContextDescriptor.Default;
            var key = RenderContextPoolKey.FromDescriptor(in d, 256, 256);

            Assert.AreEqual(0, _pool.Count(key));
            var entry = _pool.Lease(in key, in d);

            Assert.IsNotNull(entry.Rt);
            Assert.AreEqual(256, entry.Rt.width);
            Assert.AreEqual(256, entry.Rt.height);
            Assert.IsNotNull(entry.Camera);
            Assert.IsNotNull(entry.ContentRoot);
            Assert.AreEqual(0, _pool.Count(key), "Lease should leave bucket empty until Return");
        }

        [Test]
        public void Return_ThenLease_ReusesSameEntry()
        {
            var d = RenderContextDescriptor.Default;
            var key = RenderContextPoolKey.FromDescriptor(in d, 256, 256);

            var first = _pool.Lease(in key, in d);
            var firstRt = first.Rt;
            var firstCam = first.Camera;

            _pool.Return(in first);
            Assert.AreEqual(1, _pool.Count(key));

            var second = _pool.Lease(in key, in d);
            Assert.AreSame(firstRt, second.Rt, "Pool must reuse RT by reference");
            Assert.AreSame(firstCam, second.Camera, "Pool must reuse Camera by reference");
            Assert.AreEqual(0, _pool.Count(key));
        }

        [Test]
        public void Lease_DifferentKeys_AllocatesSeparateEntries()
        {
            var d1 = RenderContextDescriptor.Default;
            var key1 = RenderContextPoolKey.FromDescriptor(in d1, 256, 256);
            var key2 = RenderContextPoolKey.FromDescriptor(in d1, 512, 512);

            var a = _pool.Lease(in key1, in d1);
            var b = _pool.Lease(in key2, in d1);

            Assert.AreNotSame(a.Rt, b.Rt);
            Assert.AreEqual(256, a.Rt.width);
            Assert.AreEqual(512, b.Rt.width);

            _pool.Return(in a);
            _pool.Return(in b);
            Assert.AreEqual(2, _pool.TotalCount);
            Assert.AreEqual(1, _pool.Count(key1));
            Assert.AreEqual(1, _pool.Count(key2));
        }

        [Test]
        public void Return_DestroysContentChildren()
        {
            var d = RenderContextDescriptor.Default;
            var key = RenderContextPoolKey.FromDescriptor(in d, 128, 128);
            var entry = _pool.Lease(in key, in d);

            var child = new GameObject("child");
            child.transform.SetParent(entry.ContentRoot);
            Assert.AreEqual(1, entry.ContentRoot.childCount);

            _pool.Return(in entry);
            Assert.AreEqual(0, entry.ContentRoot.childCount, "Return must destroy content children");
        }

        [Test]
        public void Dispose_DrainsAllEntries()
        {
            var d = RenderContextDescriptor.Default;
            var key = RenderContextPoolKey.FromDescriptor(in d, 64, 64);
            var entry = _pool.Lease(in key, in d);
            _pool.Return(in entry);
            Assert.AreEqual(1, _pool.TotalCount);

            _pool.Dispose();
            Assert.AreEqual(0, _pool.TotalCount);
        }

        [Test]
        public void Lease_AfterDispose_Throws()
        {
            _pool.Dispose();
            var d = RenderContextDescriptor.Default;
            var key = RenderContextPoolKey.FromDescriptor(in d, 64, 64);
            Assert.Throws<System.ObjectDisposedException>(() => _pool.Lease(in key, in d));
        }
    }
}
