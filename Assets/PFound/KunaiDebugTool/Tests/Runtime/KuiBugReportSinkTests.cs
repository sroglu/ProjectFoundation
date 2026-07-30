using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiBugReportSinkTests
    {
        sealed class CapturingSink : IBugReportSink
        {
            public KuiBugReport LastReceived;
            public int          Calls;
            public void Send(KuiBugReport report) { Calls++; LastReceived = report; }
        }

        sealed class ThrowingSink : IBugReportSink
        {
            public int Calls;
            public void Send(KuiBugReport report) { Calls++; throw new System.Exception("synthetic sink failure"); }
        }

        // Each test ensures KuiConsole is initialised so the BugReporter logger
        // can route its error log without NREing.
        [SetUp]
        public void Setup()
        {
            if (KuiConsole.Buffer == null) KuiConsole.Initialize();
        }

        [Test]
        public void Register_NullSink_NoOp()
        {
            int before = KuiBugReporter.SinkCount;
            KuiBugReporter.RegisterSink(null);
            Assert.AreEqual(before, KuiBugReporter.SinkCount);
        }

        [Test]
        public void Register_SameInstanceTwice_OnlyOnce()
        {
            var s = new CapturingSink();
            int before = KuiBugReporter.SinkCount;
            KuiBugReporter.RegisterSink(s);
            KuiBugReporter.RegisterSink(s);
            try { Assert.AreEqual(before + 1, KuiBugReporter.SinkCount); }
            finally { KuiBugReporter.UnregisterSink(s); }
        }

        [Test]
        public void Dispatch_DeliversReportToSink()
        {
            var s = new CapturingSink();
            KuiBugReporter.RegisterSink(s);
            try
            {
                var r = new KuiBugReport { Description = "hello" };
                KuiBugReporter.Dispatch(r);
                Assert.AreEqual(1, s.Calls);
                Assert.AreSame(r, s.LastReceived);
            }
            finally { KuiBugReporter.UnregisterSink(s); }
        }

        [Test]
        public void Dispatch_ThrowingSink_DoesNotPropagate()
        {
            var bad = new ThrowingSink();
            var ok  = new CapturingSink();
            KuiBugReporter.RegisterSink(bad);
            KuiBugReporter.RegisterSink(ok);
            // KuiBugReporter routes sink failures through KuiLogger.Error, which now
            // mirrors to UnityEngine.Debug.LogError — the test runner would otherwise
            // flag this expected log as an "unhandled error".
            LogAssert.Expect(LogType.Error, "[BugReporter] sink ThrowingSink threw: synthetic sink failure");
            try
            {
                Assert.DoesNotThrow(() => KuiBugReporter.Dispatch(new KuiBugReport()));
                Assert.AreEqual(1, bad.Calls);
                Assert.AreEqual(1, ok.Calls, "second sink must still receive the report");
            }
            finally
            {
                KuiBugReporter.UnregisterSink(bad);
                KuiBugReporter.UnregisterSink(ok);
            }
        }

        [Test]
        public void Unregister_NeverRegistered_NoOp()
        {
            var s = new CapturingSink();
            int before = KuiBugReporter.SinkCount;
            Assert.DoesNotThrow(() => KuiBugReporter.UnregisterSink(s));
            Assert.AreEqual(before, KuiBugReporter.SinkCount);
        }

        [Test]
        public void Dispatch_NoSinks_NoOp()
        {
            // The current AppDomain might have other registered sinks from
            // prior tests — count them but don't assume zero.
            int before = KuiBugReporter.SinkCount;
            Assert.DoesNotThrow(() => KuiBugReporter.Dispatch(new KuiBugReport()));
            Assert.AreEqual(before, KuiBugReporter.SinkCount, "Dispatch should not mutate the sink list");
        }
    }
}
