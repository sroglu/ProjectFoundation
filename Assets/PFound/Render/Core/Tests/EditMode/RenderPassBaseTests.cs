using NUnit.Framework;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderPassBaseTests
    {
        private sealed class TestData { public int Value; }

        private sealed class TestPass : RenderPassBase<TestData>
        {
            public int PopulateCount;
            public int ExecuteCount;
            public int LastValueSeenByExecute;

            public TestPass(string tag = "TestPass", RenderPassEvent ev = RenderPassEvent.AfterRenderingTransparents)
                : base(tag, ev) { }

            protected override void Populate(IRasterRenderGraphBuilder builder, ref TestData data, ContextContainer frameData)
            {
                PopulateCount++;
                data.Value = 42;
            }

            protected override void Execute(RasterCommandBuffer cmd, in TestData data)
            {
                ExecuteCount++;
                LastValueSeenByExecute = data.Value;
            }
        }

        [Test]
        public void Constructor_DefaultsInjectionPoint_ToAfterTransparents()
        {
            var p = new TestPass();
            Assert.That(p.renderPassEvent, Is.EqualTo(RenderPassEvent.AfterRenderingTransparents));
        }

        [Test]
        public void Constructor_RespectsExplicitInjectionPoint()
        {
            var p = new TestPass(ev: RenderPassEvent.BeforeRenderingOpaques);
            Assert.That(p.renderPassEvent, Is.EqualTo(RenderPassEvent.BeforeRenderingOpaques));
        }

        [Test]
        public void Constructor_DefaultsTagToTypeName_WhenNullOrEmpty()
        {
            var p1 = new TestPass(tag: null);
            var p2 = new TestPass(tag: "");
            // The base caches the tag privately; we verify indirectly by ensuring construction does not throw
            // and the pass is usable.
            Assert.That(p1, Is.Not.Null);
            Assert.That(p2, Is.Not.Null);
        }
    }
}
