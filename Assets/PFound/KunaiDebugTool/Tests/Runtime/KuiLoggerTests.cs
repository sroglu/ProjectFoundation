// Enable [Conditional("KUNAI_VERBOSE")] call sites in this file so the
// Verbose tests below actually invoke KuiLogger.Verbose / KuiLog.Verbose.
// Without the #define the compiler drops the calls and the assertions
// would silently pass against a no-op.
#define KUNAI_VERBOSE

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kunai;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiLoggerTests
    {
        [SetUp]
        public void SetUp()
        {
            // Fresh buffer for each test
            KuiConsole.Shutdown();
            KuiConsole.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            KuiConsole.Shutdown();
        }

        [Test]
        public void Info_Uncategorised_GoesToBothConsoleAndBuffer()
        {
            const string msg = "kuilogger test info uncat";

            // Unity Console: expect a Log entry
            LogAssert.Expect(LogType.Log, msg);

            KuiLogger.Info(msg);

            // Drain async-enqueued entries onto buffer
            KuiConsole.Buffer.Drain();

            Assert.GreaterOrEqual(KuiConsole.Buffer.Count, 1, "Buffer should have at least one entry.");
            // Find our entry (other warnings from Postica etc may also be in buffer)
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Level == KuiLogLevel.Info && e.Category == null)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Expected uncategorised Info entry in Kunai buffer.");
        }

        [Test]
        public void Info_WithCategory_FormatsBracketPrefixForConsole_PreservesRawMessageInBuffer()
        {
            const string raw = "spawn at 0,0,0";
            const string cat = "Player";
            string consoleText = "[" + cat + "] " + raw;

            LogAssert.Expect(LogType.Log, consoleText);

            KuiLogger.Info(raw, cat);

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == raw && e.Category == cat && e.Level == KuiLogLevel.Info)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Buffer entry must hold the raw message + structured category (no [Cat] prefix in Message).");
        }

        [Test]
        public void Warn_RoutesToDebugLogWarning()
        {
            const string msg = "kuilogger warn test";
            LogAssert.Expect(LogType.Warning, msg);
            KuiLogger.Warn(msg);
        }

        [Test]
        public void Error_RoutesToDebugLogError()
        {
            const string msg = "kuilogger error test";
            LogAssert.Expect(LogType.Error, msg);
            KuiLogger.Error(msg);
        }

        [Test]
        public void NoDuplicate_OneCallProducesOneBufferEntry()
        {
            const string msg = "kuilogger dup-guard test";
            const string cat = "DupTest";
            LogAssert.Expect(LogType.Log, "[" + cat + "] " + msg);

            KuiLogger.Info(msg, cat);
            KuiConsole.Buffer.Drain();

            int matching = 0;
            int n = KuiConsole.Buffer.Count;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Category == cat) matching++;
            }

            Assert.AreEqual(1, matching, "Re-entry guard must prevent the Debug.Log echo from also enqueueing.");
        }

        [Test]
        public void EmptyCategory_Throws()
        {
            Assert.Throws<ArgumentException>(() => KuiLogger.Info("msg", ""));
        }

        [Test]
        public void TooLongCategory_Throws()
        {
            Assert.Throws<ArgumentException>(() => KuiLogger.Info("msg", new string('x', 32)));
        }

        [Test]
        public void CategoryWithBracket_Throws()
        {
            Assert.Throws<ArgumentException>(() => KuiLogger.Info("msg", "Bad[Cat"));
        }

        // --- KuiLog (per-class tagged) -------------------------------------

        // Test fixture used as the generic argument for KuiLogger.For<T>().
        // The tag captured by For<T>() must equal nameof(T).
        sealed class FakeSubject {}

        [Test]
        public void KuiLog_For_BindsTypeName_AndForwardsToBufferWithCategory()
        {
            const string msg = "kuilog binds type name";
            string expectedTag = nameof(FakeSubject);
            LogAssert.Expect(LogType.Log, "[" + expectedTag + "] " + msg);

            var log = KuiLogger.For<FakeSubject>();
            log.Info(msg);

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Category == expectedTag && e.Level == KuiLogLevel.Info)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Buffer entry must carry the bound type name as category.");
        }

        [Test]
        public void KuiLog_For_StringTag_AcceptsExplicitTag()
        {
            const string msg = "kuilog explicit tag";
            const string tag = "Subsystem";
            LogAssert.Expect(LogType.Log, "[" + tag + "] " + msg);

            KuiLogger.For(tag).Info(msg);

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Category == tag) { found = true; break; }
            }
            Assert.IsTrue(found, "Buffer entry must carry the explicit tag as category.");
        }

        [Test]
        public void KuiLog_Warn_Error_RouteToCorrectLevels()
        {
            string tag = nameof(FakeSubject);
            const string warn = "warn via kuilog";
            const string err  = "error via kuilog";
            LogAssert.Expect(LogType.Warning, "[" + tag + "] " + warn);
            LogAssert.Expect(LogType.Error,   "[" + tag + "] " + err);

            var log = KuiLogger.For<FakeSubject>();
            log.Warn(warn);
            log.Error(err);
        }

        // --- Verbose (Kunai-only, NOT mirrored to Unity Console) ----------

        [Test]
        public void Verbose_DoesNotMirrorToUnityConsole_BufferGetsEntry()
        {
            const string msg = "verbose direct call";

            // Intentionally NO LogAssert.Expect — Verbose must NOT emit a
            // Debug.Log of any kind. If it does, LogAssert.NoUnexpectedReceived
            // (implicit at TearDown? — explicit here for safety) would fail.
            KuiLogger.Verbose(msg);
            LogAssert.NoUnexpectedReceived();

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Level == KuiLogLevel.Verbose && e.Category == null)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Buffer must contain the Verbose entry uncategorised.");
        }

        [Test]
        public void Verbose_WithCategory_NoMirror_BufferKeepsCategory()
        {
            const string msg = "verbose categorised";
            const string cat = "Subsystem";

            KuiLogger.Verbose(msg, cat);
            LogAssert.NoUnexpectedReceived();

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Category == cat && e.Level == KuiLogLevel.Verbose)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Verbose entry must carry the explicit category in buffer.");
        }

        [Test]
        public void KuiLog_Verbose_ForwardsWithBoundTag_NoConsoleMirror()
        {
            const string msg = "verbose via kuilog";
            string expectedTag = nameof(FakeSubject);

            var log = KuiLogger.For<FakeSubject>();
            log.Verbose(msg);
            LogAssert.NoUnexpectedReceived();

            KuiConsole.Buffer.Drain();
            int n = KuiConsole.Buffer.Count;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                ref var e = ref KuiConsole.Buffer.GetAt(i);
                if (e.Message == msg && e.Category == expectedTag && e.Level == KuiLogLevel.Verbose)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "KuiLog.Verbose must forward with the bound tag and not mirror.");
        }
    }
}
