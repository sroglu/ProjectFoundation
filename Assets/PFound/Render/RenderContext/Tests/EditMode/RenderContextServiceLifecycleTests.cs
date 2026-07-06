using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    public sealed class RenderContextServiceLifecycleTests
    {
        /// <summary>
        /// Minimal stub anchor for lifecycle tests — fixed size, no real Unity target.
        /// Used by US4 (T042) and tests in this file. Defined inline to avoid leaking a Helpers folder before T042.
        /// </summary>
        private sealed class StubAnchor : IRenderContextAnchor
        {
            private readonly object _target;
            public int PreferredWidth { get; set; }
            public int PreferredHeight { get; set; }
            public bool TargetAlive { get; set; } = true;
            public StubAnchor(int w, int h, object target = null)
            {
                PreferredWidth = w;
                PreferredHeight = h;
                _target = target ?? new object();
            }
            public object Target => _target;
            public IRenderContextSink CreateSink() => new NoOpSink();
        }

        private sealed class NoOpSink : IRenderContextSink
        {
            public void Bind(RenderTexture rt) { }
            public void Unbind() { }
        }

        [Test]
        public void Ctor_CreatesOwnerGameObjectMarkedDontDestroyOnLoad()
        {
            var svc = new RenderContextService();
            try
            {
                var ownerGo = GameObject.Find("[RenderContextService]");
                Assert.IsNotNull(ownerGo, "Hidden owner GameObject should exist after ctor");
                Assert.IsTrue((ownerGo.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave);
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void Acquire_NullAnchor_Throws()
        {
            var svc = new RenderContextService();
            try
            {
                Assert.Throws<ArgumentNullException>(
                    () => svc.Acquire(RenderContextDescriptor.Default, null));
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void Acquire_AfterDispose_Throws()
        {
            var svc = new RenderContextService();
            svc.Dispose();
            Assert.Throws<ObjectDisposedException>(
                () => svc.Acquire(RenderContextDescriptor.Default, new StubAnchor(64, 64)));
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var svc = new RenderContextService();
            svc.Dispose();
            Assert.DoesNotThrow(() => svc.Dispose());
        }

        [Test]
        public void Acquire_SameTargetTwice_ThrowsInvalidOperation()
        {
            var svc = new RenderContextService();
            try
            {
                var target = new object();
                var a1 = new StubAnchor(64, 64, target);
                var a2 = new StubAnchor(64, 64, target);
                var h1 = svc.Acquire(RenderContextDescriptor.Default, a1);
                Assert.Throws<InvalidOperationException>(
                    () => svc.Acquire(RenderContextDescriptor.Default, a2));
                h1.Dispose();
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void Acquire_TargetNotAlive_Throws()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new StubAnchor(64, 64) { TargetAlive = false };
                Assert.Throws<InvalidOperationException>(
                    () => svc.Acquire(RenderContextDescriptor.Default, anchor));
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void Dispose_DisposesLiveHandles()
        {
            var svc = new RenderContextService();
            var handle = svc.Acquire(RenderContextDescriptor.Default, new StubAnchor(64, 64));
            Assert.IsTrue(handle.IsAlive);
            svc.Dispose();
            Assert.IsFalse(handle.IsAlive);
            Assert.Throws<ObjectDisposedException>(() => { var _ = handle.Texture; });
        }

        [Test]
        public void Acquire_InvalidDescriptor_DoesNotLeakBoundTarget()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new StubAnchor(64, 64);
                var bad = RenderContextDescriptor.Default;
                bad.Msaa = 7; // invalid
                Assert.Throws<ArgumentException>(() => svc.Acquire(bad, anchor));
                // Re-acquire with the same anchor should now succeed (no leaked binding)
                var good = svc.Acquire(RenderContextDescriptor.Default, anchor);
                Assert.IsTrue(good.IsAlive);
                good.Dispose();
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void HandleDispose_IsIdempotent()
        {
            var svc = new RenderContextService();
            try
            {
                var handle = svc.Acquire(RenderContextDescriptor.Default, new StubAnchor(64, 64));
                handle.Dispose();
                Assert.DoesNotThrow(() => handle.Dispose());
            }
            finally
            {
                svc.Dispose();
            }
        }

        [Test]
        public void HandleAccessor_AfterDispose_ThrowsObjectDisposed()
        {
            var svc = new RenderContextService();
            try
            {
                var handle = svc.Acquire(RenderContextDescriptor.Default, new StubAnchor(64, 64));
                handle.Dispose();
                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.Texture; });
                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.Camera; });
                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.ContentRoot; });
                Assert.Throws<ObjectDisposedException>(handle.Refresh);
            }
            finally
            {
                svc.Dispose();
            }
        }
    }
}
