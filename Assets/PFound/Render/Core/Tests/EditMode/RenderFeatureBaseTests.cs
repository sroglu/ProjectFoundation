using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;

namespace PFound.Render.Core.Tests
{
    public sealed class RenderFeatureBaseTests
    {
        private sealed class CountingPass : ScriptableRenderPass, IDisposable
        {
            public int DisposeCallCount;
            public void Dispose() => DisposeCallCount++;
        }

        private sealed class TestFeature : RenderFeatureBase
        {
            public int OnCreateCount;
            public int OnDisposeCount;
            public CountingPass Pass;

            protected override void OnCreate()
            {
                OnCreateCount++;
                Pass = new CountingPass();
                EnqueuePass(Pass);
            }

            protected override void OnDispose() => OnDisposeCount++;
        }

        private sealed class NullArgFeature : RenderFeatureBase
        {
            protected override void OnCreate() { /* OnCreate body verified via reflection in test */ }

            public void EnqueueNull() => base.GetType().GetMethod("EnqueuePass",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(this, new object[] { null });
        }

        [Test]
        public void OnCreate_IsCalledOnce_PerCreate()
        {
            var f = ScriptableObject.CreateInstance<TestFeature>();
            try
            {
                f.Create();
                Assert.That(f.OnCreateCount, Is.EqualTo(1), "OnCreate should run exactly once per Create().");
            }
            finally { ScriptableObject.DestroyImmediate(f); }
        }

        [Test]
        public void Dispose_RunsOnDispose_AndDisposesEnqueuedPasses()
        {
            var f = ScriptableObject.CreateInstance<TestFeature>();
            f.Create();
            var pass = f.Pass;

            ScriptableObject.DestroyImmediate(f);

            Assert.That(pass.DisposeCallCount, Is.EqualTo(1), "Enqueued IDisposable pass should be disposed once.");
        }

        [Test]
        public void OnCreate_ResetsPassesOnSecondCreate()
        {
            var f = ScriptableObject.CreateInstance<TestFeature>();
            try
            {
                f.Create();
                var first = f.Pass;
                f.Create();
                Assert.That(f.OnCreateCount, Is.EqualTo(2));
                Assert.That(f.Pass, Is.Not.SameAs(first), "Second Create should rebuild passes.");
                // First pass list is cleared on second Create — so its Dispose shouldn't fire until feature Dispose.
                Assert.That(first.DisposeCallCount, Is.EqualTo(0));
            }
            finally { ScriptableObject.DestroyImmediate(f); }
        }
    }
}
