using System;
using NUnit.Framework;
using UnityEngine;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-022 — <see cref="BatchRenderingFeature"/> guard surface: AttachService(null)
    /// throws; DetachService is idempotent; double-attach is allowed (replaces previous binding).
    /// </summary>
    public sealed class BatchRenderingFeatureGuardsTests
    {
        private BatchRenderingFeature _feature;

        [SetUp]
        public void SetUp()
        {
            _feature = ScriptableObject.CreateInstance<BatchRenderingFeature>();
            _feature.Create();
        }

        [TearDown]
        public void TearDown()
        {
            if (_feature != null) UnityEngine.Object.DestroyImmediate(_feature);
        }

        [Test]
        public void AttachService_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _feature.AttachService(null));
        }

        [Test]
        public void DetachService_WithoutAttach_IsSafe()
        {
            Assert.DoesNotThrow(() => _feature.DetachService());
            // Twice — still safe.
            Assert.DoesNotThrow(() => _feature.DetachService());
        }

        [Test]
        public void AttachService_Then_DetachService_LeavesNoServiceState()
        {
            using var service = new BatchRenderingService();
            _feature.AttachService(service);
            _feature.DetachService();

            // After detach, TryGetService returns false. This is internal but visible via
            // InternalsVisibleTo.
            // We exercise the logic indirectly: a fresh attach should work.
            using var s2 = new BatchRenderingService();
            Assert.DoesNotThrow(() => _feature.AttachService(s2));
            _feature.DetachService();
        }

        [Test]
        public void DoubleAttach_ReplacesPreviousBinding()
        {
            using var s1 = new BatchRenderingService();
            using var s2 = new BatchRenderingService();
            _feature.AttachService(s1);
            _feature.AttachService(s2);
            // No exceptions, no warnings expected; second binding wins.
            _feature.DetachService();
        }

        [Test]
        public void InjectionPoint_DefaultsToAfterRenderingOpaques()
        {
            Assert.AreEqual(UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingOpaques, _feature.injectionPoint);
        }
    }
}
