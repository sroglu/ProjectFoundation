using NUnit.Framework;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.Tests
{
    /// <summary>
    /// Watcher tests run via direct <see cref="AnchorResizeWatcher.Tick"/> invocation —
    /// the LoopScheduler BeforeRender callback hook is exercised inside Unity's normal frame
    /// loop, which EditMode tests do not drive. The Tick logic is what we care about.
    /// </summary>
    public sealed class AnchorResizeWatcherTests
    {
        private sealed class MutableAnchor : IRenderContextAnchor
        {
            public int PreferredWidth { get; set; }
            public int PreferredHeight { get; set; }
            public bool TargetAlive { get; set; } = true;
            public object Target { get; } = new object();
            public IRenderContextSink CreateSink() => new NoSink();
            private sealed class NoSink : IRenderContextSink
            {
                public void Bind(RenderTexture rt) { }
                public void Unbind() { }
            }
        }

        [Test]
        public void Tick_AnchorAlive_NoSizeChange_DoesNotRefresh()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new MutableAnchor { PreferredWidth = 128, PreferredHeight = 128 };
                var handle = svc.Acquire(RenderContextDescriptor.Default, anchor);
                var rtBefore = handle.Texture;

                // Walk forward 1 tick — no size change, so no refresh
                GetWatcher(svc).Tick();
                Assert.AreSame(rtBefore, handle.Texture, "RT should not change when size is stable");
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void Tick_AnchorSizeChange_TriggersRefresh()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new MutableAnchor { PreferredWidth = 128, PreferredHeight = 128 };
                var handle = svc.Acquire(RenderContextDescriptor.Default, anchor);
                var rtBefore = handle.Texture;
                int wBefore = rtBefore.width, hBefore = rtBefore.height;

                anchor.PreferredWidth = 256;
                anchor.PreferredHeight = 256;
                GetWatcher(svc).Tick();

                Assert.AreNotSame(rtBefore, handle.Texture, "RT must swap on size change");
                Assert.AreEqual(256, handle.Texture.width);
                Assert.AreEqual(256, handle.Texture.height);
                Assert.AreEqual(wBefore, 128);
                Assert.AreEqual(hBefore, 128);
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void Tick_AnchorZeroSize_SkipsRefresh()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new MutableAnchor { PreferredWidth = 64, PreferredHeight = 64 };
                var handle = svc.Acquire(RenderContextDescriptor.Default, anchor);
                var rtBefore = handle.Texture;

                anchor.PreferredWidth = 0;
                anchor.PreferredHeight = 0;
                GetWatcher(svc).Tick();

                Assert.AreSame(rtBefore, handle.Texture, "Zero size must skip refresh, last RT persists");
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void Tick_AnchorTargetDestroyed_AutoDisposesHandle()
        {
            var svc = new RenderContextService();
            try
            {
                var anchor = new MutableAnchor { PreferredWidth = 64, PreferredHeight = 64 };
                var handle = svc.Acquire(RenderContextDescriptor.Default, anchor);
                Assert.IsTrue(handle.IsAlive);

                anchor.TargetAlive = false;
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[RenderContext\] Anchor target destroyed externally"));
                GetWatcher(svc).Tick();

                Assert.IsFalse(handle.IsAlive, "Watcher must auto-dispose handle when anchor.TargetAlive flips false");
            }
            finally { svc.Dispose(); }
        }

        private static AnchorResizeWatcher GetWatcher(RenderContextService svc)
        {
            var field = typeof(RenderContextService).GetField("_watcher",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (AnchorResizeWatcher)field.GetValue(svc);
        }
    }
}
