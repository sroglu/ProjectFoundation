using System;
using NUnit.Framework;
using UnityEngine.TestTools;
using PFound.Render.Core.ShaderParameters;

namespace PFound.Render.Core.Tests
{
    public sealed class GlobalShaderParameterManagerRegistrationTests
    {
        private sealed class NoopProvider : IGlobalShaderParameterProvider
        {
            public string DebugName => "Noop";
            public void Publish() { }
        }

        // Guarantee a clean process-wide registry before/after every test so a
        // provider left registered by one test can never make another test log
        // "already registered". Tests here use fresh `new` instances, so this is
        // defense-in-depth against the singleton being touched elsewhere.
        [SetUp]
        public void SetUp() => GlobalShaderParameterManager.ResetInstance();

        [TearDown]
        public void TearDown() => GlobalShaderParameterManager.ResetInstance();

        [Test]
        public void NullProvider_Throws()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                Assert.Throws<ArgumentNullException>(() => mgr.Register(null, 0));
            }
            finally { mgr.Dispose(); }
        }

        [Test]
        public void SameInstance_RegisteredTwice_Throws_AndLogsError()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var p = new NoopProvider();
                mgr.Register(p, 0);
                LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"already registered"));
                Assert.Throws<InvalidOperationException>(() => mgr.Register(p, 0));
            }
            finally { mgr.Dispose(); }
        }

        [Test]
        public void DistinctProvidersSamePriority_BothAccepted()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var a = new NoopProvider();
                var b = new NoopProvider();
                mgr.Register(a, 5);
                mgr.Register(b, 5);
                Assert.That(mgr.Count, Is.EqualTo(2));
            }
            finally { mgr.Dispose(); }
        }
    }
}