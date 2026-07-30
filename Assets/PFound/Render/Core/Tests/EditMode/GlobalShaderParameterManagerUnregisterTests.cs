using NUnit.Framework;
using PFound.Render.Core.ShaderParameters;

namespace PFound.Render.Core.Tests
{
    public sealed class GlobalShaderParameterManagerUnregisterTests
    {
        private sealed class P : IGlobalShaderParameterProvider
        {
            public string DebugName => "P";
            public void Publish() { }
        }

        [SetUp]
        public void SetUp() => GlobalShaderParameterManager.ResetInstance();

        [TearDown]
        public void TearDown() => GlobalShaderParameterManager.ResetInstance();

        [Test]
        public void Unregister_ReturnsTrue_ForRegistered()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var p = new P();
                mgr.Register(p, 0);
                Assert.That(mgr.Unregister(p), Is.True);
                Assert.That(mgr.Count, Is.EqualTo(0));
            }
            finally { mgr.Dispose(); }
        }

        [Test]
        public void Unregister_ReturnsFalse_ForUnknownOrNull()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                Assert.That(mgr.Unregister(new P()), Is.False);
                Assert.That(mgr.Unregister(null), Is.False);
            }
            finally { mgr.Dispose(); }
        }

        [Test]
        public void UnregisterAndReRegister_Succeeds()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var p = new P();
                mgr.Register(p, 0);
                mgr.Unregister(p);
                Assert.DoesNotThrow(() => mgr.Register(p, 0));
            }
            finally { mgr.Dispose(); }
        }
    }
}