using System.Collections.Generic;
using NUnit.Framework;
using PFound.Render.Core.ShaderParameters;

namespace PFound.Render.Core.Tests
{
    public sealed class GlobalShaderParameterManagerOrderingTests
    {
        private sealed class RecordingProvider : IGlobalShaderParameterProvider
        {
            public string DebugName { get; }
            public List<string> Log;
            public RecordingProvider(string name, List<string> log) { DebugName = name; Log = log; }
            public void Publish() => Log.Add(DebugName);
        }

        [SetUp]
        public void SetUp() => GlobalShaderParameterManager.ResetInstance();

        [TearDown]
        public void TearDown() => GlobalShaderParameterManager.ResetInstance();

        [Test]
        public void DistinctPriorities_PublishedInAscendingOrder()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var log = new List<string>();
                mgr.Register(new RecordingProvider("A", log), priority: 10);
                mgr.Register(new RecordingProvider("B", log), priority: 5);
                mgr.Register(new RecordingProvider("C", log), priority: 15);
                mgr.PublishAll();
                Assert.That(log, Is.EqualTo(new[] { "B", "A", "C" }));
            }
            finally { mgr.Dispose(); }
        }

        [Test]
        public void SamePriority_FifoTiebreaker()
        {
            var mgr = new GlobalShaderParameterManager();
            try
            {
                var log = new List<string>();
                mgr.Register(new RecordingProvider("A", log), priority: 5);
                mgr.Register(new RecordingProvider("B", log), priority: 5);
                mgr.Register(new RecordingProvider("C", log), priority: 5);
                mgr.PublishAll();
                Assert.That(log, Is.EqualTo(new[] { "A", "B", "C" }));
            }
            finally { mgr.Dispose(); }
        }
    }
}