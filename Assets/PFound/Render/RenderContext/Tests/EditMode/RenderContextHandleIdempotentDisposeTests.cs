using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;
using PFound.Render.Tests.Helpers;

namespace PFound.Render.Tests
{
    public sealed class RenderContextHandleIdempotentDisposeTests
    {
        [Test]
        public void Dispose_DoubleCall_IsNoOp()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new TestRenderContextAnchor(64, 64);
                var handle = svc.Acquire(RenderContextDescriptor.Default, anchor);
                handle.Dispose();
                Assert.DoesNotThrow(() => handle.Dispose());
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void Properties_AfterDispose_AllThrowObjectDisposed()
        {
            var svc = new RenderContextService();
            try
            {
                var handle = svc.Acquire(RenderContextDescriptor.Default, new TestRenderContextAnchor(64, 64));
                handle.Dispose();

                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.Texture; });
                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.Camera; });
                Assert.Throws<ObjectDisposedException>(() => { var _ = handle.ContentRoot; });
                Assert.Throws<ObjectDisposedException>(handle.Refresh);
            }
            finally { svc.Dispose(); }
        }
    }
}
