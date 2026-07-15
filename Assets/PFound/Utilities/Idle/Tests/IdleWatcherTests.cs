using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using PFound.Utilities.Idle;
using UnityEngine.TestTools;

namespace PFound.Utilities.Idle.Tests
{
    // Edit-mode tests. Most transitions are exercised deterministically through the manual
    // Tick() seam with an injected clock, so they need no real waiting. One play-mode-style
    // test exercises the live UniTask loop end to end.
    public class IdleWatcherTests
    {
        // Advanceable fake clock so threshold crossings are exact and instant.
        private sealed class FakeClock
        {
            public double Now;
            public double Read() => Now;
        }

        private static IdleWatcher Build(FakeClock clock, TimeSpan threshold, Func<bool> probe = null)
        {
            return new IdleWatcher(threshold, probe, TimeSpan.FromSeconds(1), clock.Read);
        }

        [Test]
        public void StartsActive()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(5));
            Assert.IsFalse(w.CurrentlyIdle);
        }

        [Test]
        public void RejectsNonPositiveThreshold()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdleWatcher(TimeSpan.Zero));
        }

        [Test]
        public void GoesIdleAfterThreshold()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(5));
            int edges = 0;
            bool last = false;
            w.OnIdleChanged += s => { edges++; last = s; };

            clock.Now = 4.9;
            w.Tick();
            Assert.IsFalse(w.CurrentlyIdle, "not yet past threshold");
            Assert.AreEqual(0, edges);

            clock.Now = 5.0;
            w.Tick();
            Assert.IsTrue(w.CurrentlyIdle, "idle once threshold reached");
            Assert.AreEqual(1, edges, "exactly one edge on going idle");
            Assert.IsTrue(last);
        }

        [Test]
        public void IdleEdgeFiresOnlyOnce()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(2));
            int edges = 0;
            w.OnIdleChanged += _ => edges++;

            clock.Now = 3;
            w.Tick();
            w.Tick();
            w.Tick();
            Assert.AreEqual(1, edges, "staying idle does not refire");
        }

        [Test]
        public void PokeReturnsToActiveAndFiresEdge()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(2));
            var states = new List<bool>();
            w.OnIdleChanged += states.Add;

            clock.Now = 2;
            w.Tick();
            Assert.IsTrue(w.CurrentlyIdle);

            clock.Now = 2.1;
            w.Poke();
            Assert.IsFalse(w.CurrentlyIdle, "poke reactivates immediately");
            CollectionAssert.AreEqual(new[] { true, false }, states, "one idle then one active edge");
        }

        [Test]
        public void PokeWhileActiveDoesNotFire()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(5));
            int edges = 0;
            w.OnIdleChanged += _ => edges++;

            w.Poke();
            w.Poke();
            Assert.AreEqual(0, edges, "no edge while already active");
            Assert.IsFalse(w.CurrentlyIdle);
        }

        [Test]
        public void PokeResetsTheQuietTimer()
        {
            var clock = new FakeClock();
            using var w = Build(clock, TimeSpan.FromSeconds(5));

            clock.Now = 4;
            w.Poke();          // resets last-activity to t=4
            clock.Now = 8;     // 4s elapsed since poke, still under threshold
            w.Tick();
            Assert.IsFalse(w.CurrentlyIdle, "poke pushed the deadline out");

            clock.Now = 9;     // now 5s since poke
            w.Tick();
            Assert.IsTrue(w.CurrentlyIdle);
        }

        [Test]
        public void ActivityProbeCountsAsActivity()
        {
            var clock = new FakeClock();
            bool active = true;
            using var w = Build(clock, TimeSpan.FromSeconds(3), () => active);

            clock.Now = 10;
            w.Tick();          // probe true -> treated as activity, timer reset to t=10
            Assert.IsFalse(w.CurrentlyIdle);

            active = false;
            clock.Now = 12;
            w.Tick();
            Assert.IsFalse(w.CurrentlyIdle, "under threshold since last probe hit");

            clock.Now = 13;
            w.Tick();
            Assert.IsTrue(w.CurrentlyIdle, "idle once probe stays quiet past threshold");
        }

        [UnityTest]
        public IEnumerator LiveLoopTransitionsToIdle() => UniTask.ToCoroutine(async () =>
        {
            // Real UniTask loop with the real clock and a short threshold.
            using var w = new IdleWatcher(TimeSpan.FromMilliseconds(150), pollInterval: TimeSpan.FromMilliseconds(30));
            bool sawIdle = false;
            w.OnIdleChanged += s => { if (s) sawIdle = true; };

            w.Start();
            Assert.IsTrue(w.IsRunning);
            await UniTask.Delay(TimeSpan.FromMilliseconds(400));
            Assert.IsTrue(sawIdle, "live loop reported idle after the quiet threshold");
            Assert.IsTrue(w.CurrentlyIdle);

            w.Poke();
            await UniTask.Delay(TimeSpan.FromMilliseconds(60));
            Assert.IsFalse(w.CurrentlyIdle, "poke reactivated the live watcher");

            w.Stop();
            Assert.IsFalse(w.IsRunning);
        });
    }
}
