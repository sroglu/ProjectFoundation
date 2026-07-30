using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.RenderContext;
using PFound.Render.Tests.Helpers;

namespace PFound.Render.Tests
{
    /// <summary>
    /// US5 footgun-warning tests (T048). Each footgun emits exactly one
    /// <c>Debug.LogWarning</c> per service lifetime. Asserted via <see cref="LogAssert.Expect"/>
    /// + a follow-up acquire that must NOT add another warning.
    /// </summary>
    public sealed class RenderContextDiagnosticsTests
    {
        [Test]
        public void HighMsaa_EmitsOneShotWarning()
        {
            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 64;
                desc.Height = 64;
                desc.Msaa = 8;
                desc.CullingMask = 1 << 5; // avoid the CullingMask=Everything warning

                LogAssert.Expect(LogType.Warning, new Regex(@"\[RenderContext\] MSAA=8"));
                var h1 = svc.Acquire(desc, new TestRenderContextAnchor(64, 64));
                h1.Dispose();

                // Second Acquire with same MSAA: no additional warning
                var h2 = svc.Acquire(desc, new TestRenderContextAnchor(64, 64, new object()));
                h2.Dispose();
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void CullingMaskEverything_EmitsOneShotWarning()
        {
            var svc = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 64;
                desc.Height = 64;
                desc.Msaa = 1;
                desc.CullingMask = ~0;

                LogAssert.Expect(LogType.Warning, new Regex(@"\[RenderContext\] CullingMask='Everything'"));
                var h1 = svc.Acquire(desc, new TestRenderContextAnchor(64, 64));
                h1.Dispose();

                var h2 = svc.Acquire(desc, new TestRenderContextAnchor(64, 64, new object()));
                h2.Dispose();
            }
            finally { svc.Dispose(); }
        }

        [Test]
        public void DifferentServices_EachEmitWarningOnce()
        {
            var svc1 = new RenderContextService();
            var svc2 = new RenderContextService();
            try
            {
                var desc = RenderContextDescriptor.Default;
                desc.Width = 64;
                desc.Height = 64;
                desc.CullingMask = ~0;
                desc.Msaa = 1;

                LogAssert.Expect(LogType.Warning, new Regex(@"\[RenderContext\] CullingMask='Everything'"));
                var h1 = svc1.Acquire(desc, new TestRenderContextAnchor(64, 64));
                h1.Dispose();

                LogAssert.Expect(LogType.Warning, new Regex(@"\[RenderContext\] CullingMask='Everything'"));
                var h2 = svc2.Acquire(desc, new TestRenderContextAnchor(64, 64));
                h2.Dispose();
            }
            finally
            {
                svc1.Dispose();
                svc2.Dispose();
            }
        }
    }
}
