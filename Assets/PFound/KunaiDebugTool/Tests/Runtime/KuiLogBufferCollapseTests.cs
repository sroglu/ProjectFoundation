using System.Collections.Generic;
using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiLogBufferCollapseTests
    {
        static KuiLogBuffer Seed(params (KuiLogLevel level, string cat, string msg)[] items)
        {
            var buf = new KuiLogBuffer(items.Length + 4);
            foreach (var i in items)
            {
                buf.Enqueue(new KuiLogEntry { Level = i.level, Category = i.cat, Message = i.msg });
            }
            buf.Drain();
            return buf;
        }

        static List<int> AllIndices(KuiLogBuffer buf)
        {
            var l = new List<int>(buf.Count);
            for (int i = 0; i < buf.Count; i++) l.Add(i);
            return l;
        }

        [Test]
        public void Collapse_NoDuplicates_OneSpanPerEntry()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "AI", "a"),
                (KuiLogLevel.Info, "AI", "b"),
                (KuiLogLevel.Info, "AI", "c")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(3, runs.Count);
            for (int i = 0; i < runs.Count; i++)
                Assert.AreEqual(1, runs[i].RunCount, "expected RunCount=1 for entry " + i);
        }

        [Test]
        public void Collapse_AllIdentical_SingleSpan()
        {
            var buf = Seed(
                (KuiLogLevel.Warning, "Net", "ping"),
                (KuiLogLevel.Warning, "Net", "ping"),
                (KuiLogLevel.Warning, "Net", "ping")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(0, runs[0].FirstIndex);
            Assert.AreEqual(3, runs[0].RunCount);
        }

        [Test]
        public void Collapse_DifferentLevel_Splits()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "AI", "x"),
                (KuiLogLevel.Info, "AI", "x"),
                (KuiLogLevel.Warning, "AI", "x")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual(2, runs[0].RunCount);
            Assert.AreEqual(1, runs[1].RunCount);
        }

        [Test]
        public void Collapse_DifferentCategory_Splits()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "AI",  "x"),
                (KuiLogLevel.Info, "Net", "x")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(2, runs.Count);
        }

        [Test]
        public void Collapse_NullVsNonNullCategory_Splits()
        {
            var buf = Seed(
                (KuiLogLevel.Info, null, "x"),
                (KuiLogLevel.Info, "AI", "x")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(2, runs.Count);
        }

        [Test]
        public void Collapse_DifferentMessage_Splits()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "AI", "x"),
                (KuiLogLevel.Info, "AI", "y")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(2, runs.Count);
        }

        [Test]
        public void Collapse_RunsThenSingleton_PreservesShape()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "AI", "a"),
                (KuiLogLevel.Info, "AI", "a"),
                (KuiLogLevel.Info, "AI", "a"),
                (KuiLogLevel.Info, "AI", "b"),
                (KuiLogLevel.Info, "AI", "c"),
                (KuiLogLevel.Info, "AI", "c")
            );
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(3, runs.Count);
            Assert.AreEqual(3, runs[0].RunCount);   // a × 3
            Assert.AreEqual(1, runs[1].RunCount);   // b × 1
            Assert.AreEqual(2, runs[2].RunCount);   // c × 2
        }

        [Test]
        public void Collapse_EmptyInput_NoSpans()
        {
            var buf = Seed();
            var runs = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreEqual(0, runs.Count);
        }

        [Test]
        public void Collapse_ReusesSameListInstance_NoFrameAlloc()
        {
            var buf = Seed(
                (KuiLogLevel.Info, "A", "x"),
                (KuiLogLevel.Info, "A", "x")
            );
            var first  = buf.CollapseConsecutive(AllIndices(buf));
            var second = buf.CollapseConsecutive(AllIndices(buf));
            Assert.AreSame(first, second, "Internal runs list should be reused frame-to-frame");
        }
    }
}
