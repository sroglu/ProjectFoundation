using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using PFound.Render.Core.ShaderParameters;

namespace PFound.Render.Core.Tests
{
    public sealed class GlobalShaderParameterManagerZeroAllocTests
    {
        private sealed class NoopProvider : IGlobalShaderParameterProvider
        {
            public string DebugName { get; }
            public NoopProvider(string n) { DebugName = n; }
            public void Publish() { }
        }

        [UnityTest]
        public IEnumerator PublishAll_AllocatesZeroBytes_Over60Frames()
        {
            var mgr = new GlobalShaderParameterManager();
            for (int i = 0; i < 16; i++) mgr.Register(new NoopProvider("p" + i), priority: i);
            try
            {
                yield return ZeroAllocAssertions.AssertZeroAlloc(
                    () => mgr.PublishAll(),
                    frameCount: 60,
                    label: "GlobalShaderParameterManager.PublishAll");
            }
            finally { mgr.Dispose(); }
        }
    }
}