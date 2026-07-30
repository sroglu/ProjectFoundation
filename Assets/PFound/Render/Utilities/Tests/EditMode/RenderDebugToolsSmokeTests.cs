using NUnit.Framework;
using UnityEngine;
using PFound.Render.Utilities;

namespace PFound.Render.Tests
{
    /// <summary>
    /// EditMode smoke test for <see cref="RenderDebugTools"/>. Visual rendering
    /// verification is performed manually per SC-009 (Conditional stripping in
    /// release builds; not covered by automated tests).
    /// </summary>
    public sealed class RenderDebugToolsSmokeTests
    {
        [Test]
        public void DrawWorldLine_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RenderDebugTools.DrawWorldLine(Vector3.zero, Vector3.one, Color.white));
        }

        [Test]
        public void DrawWorldBox_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RenderDebugTools.DrawWorldBox(Vector3.zero, Vector3.one, Quaternion.identity, Color.red));
        }

        [Test]
        public void DrawWorldRay_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RenderDebugTools.DrawWorldRay(Vector3.zero, Vector3.up, Color.green));
        }

        [Test]
        public void DrawWorldArrow_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RenderDebugTools.DrawWorldArrow(Vector3.zero, Vector3.forward, Color.blue));
        }
    }
}