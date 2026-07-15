#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using PFound.Utilities.Messaging;

namespace PFound.Utilities.Messaging.CoreTests
{
    /// <summary>
    /// Standalone csc/mono runner exercising the engine-free dispatch, ordering,
    /// lifetime, re-entrancy, guard and StateSwitch behavior. No Unity required.
    /// </summary>
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Check(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
            }
            else
            {
                _failed++;
                Console.WriteLine("  FAIL: " + label);
            }
        }

        private static void Eq<T>(T actual, T expected, string label)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            if (!ok)
            {
                Console.WriteLine("  FAIL: " + label + " (expected=" + expected + " actual=" + actual + ")");
                _failed++;
            }
            else
            {
                _passed++;
            }
        }

        private static void Reset()
        {
            MessagingEnvironment.LivenessOf = g => g != null;
            MessagingEnvironment.ReportFault = _ => { };
        }

        public static int Main()
        {
            Reset();

            BasicRaise();
            TypedPayload();
            OrderingByPriority();
            StableOrderForEqualPriority();
            ProtectedRaiseContinues();
            UnprotectedRaisePropagates();
            FireOnceLifetime();
            UnsubscribeByCallback();
            SubscriptionDisposeDetaches();
            UnsubscribeAllByOwner();
            AddDuringRaiseDeferred();
            RemoveDuringRaiseNotFired();
            RemoveCurrentlyFiring();
            NestedRaise();
            StaleGuardAutoDropped();
            ClearRemovesAll();
            PruneDropsStale();
            SwitchEdgeCallbacks();
            SwitchNoBroadcastOnSameState();
            SwitchLateJoinFastTrack();
            SwitchUnsubscribeDetachesBoth();
            SwitchToggle();
            CountIgnoresTombstones();

            Console.WriteLine();
            Console.WriteLine("Messaging Core: " + _passed + "/" + (_passed + _failed) + " passed");
            return _failed == 0 ? 0 : 1;
        }

        private static void BasicRaise()
        {
            var ch = new EventChannel();
            int hits = 0;
            ch.Subscribe(() => hits++);
            ch.Raise();
            ch.Raise();
            Eq(hits, 2, "BasicRaise: fired inline each raise");
            Eq(ch.ListenerCount, 1, "BasicRaise: listener count");
        }

        private static void TypedPayload()
        {
            var ch = new EventChannel<string>();
            string got = null;
            ch.Subscribe(p => got = p);
            ch.Raise("hello");
            Eq(got, "hello", "TypedPayload: payload delivered");
        }

        private static void OrderingByPriority()
        {
            var ch = new EventChannel();
            var log = new List<string>();
            ch.Subscribe(() => log.Add("c"), order: 10);
            ch.Subscribe(() => log.Add("a"), order: -5);
            ch.Subscribe(() => log.Add("b"), order: 0);
            ch.Raise();
            Eq(string.Join(",", log.ToArray()), "a,b,c", "OrderingByPriority: lower runs first");
        }

        private static void StableOrderForEqualPriority()
        {
            var ch = new EventChannel();
            var log = new List<string>();
            ch.Subscribe(() => log.Add("first"), order: 0);
            ch.Subscribe(() => log.Add("second"), order: 0);
            ch.Subscribe(() => log.Add("third"), order: 0);
            ch.Raise();
            Eq(string.Join(",", log.ToArray()), "first,second,third", "StableOrder: equal orders keep registration order");
        }

        private static void ProtectedRaiseContinues()
        {
            var ch = new EventChannel();
            int faults = 0;
            MessagingEnvironment.ReportFault = _ => faults++;
            int after = 0;
            ch.Subscribe(() => { throw new InvalidOperationException("boom"); }, order: 0);
            ch.Subscribe(() => after++, order: 1);
            ch.RaiseProtected();
            Eq(faults, 1, "ProtectedRaise: fault reported");
            Eq(after, 1, "ProtectedRaise: later listener still ran");
            MessagingEnvironment.ReportFault = _ => { };
        }

        private static void UnprotectedRaisePropagates()
        {
            var ch = new EventChannel();
            ch.Subscribe(() => { throw new InvalidOperationException("boom"); });
            bool threw = false;
            try
            {
                ch.Raise();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Check(threw, "UnprotectedRaise: exception propagates");
        }

        private static void FireOnceLifetime()
        {
            var ch = new EventChannel();
            int hits = 0;
            ch.SubscribeOnce(() => hits++);
            ch.Raise();
            ch.Raise();
            Eq(hits, 1, "FireOnce: fired exactly once");
            Eq(ch.ListenerCount, 0, "FireOnce: detached after firing");
        }

        private static void UnsubscribeByCallback()
        {
            var ch = new EventChannel();
            int hits = 0;
            Action cb = () => hits++;
            ch.Subscribe(cb);
            Check(ch.Unsubscribe(cb), "UnsubscribeByCallback: returns true");
            ch.Raise();
            Eq(hits, 0, "UnsubscribeByCallback: not fired after removal");
        }

        private static void SubscriptionDisposeDetaches()
        {
            var ch = new EventChannel();
            int hits = 0;
            var sub = ch.Subscribe(() => hits++);
            Check(sub.Active, "SubscriptionDispose: active before dispose");
            sub.Dispose();
            Check(!sub.Active, "SubscriptionDispose: inactive after dispose");
            sub.Dispose(); // idempotent
            ch.Raise();
            Eq(hits, 0, "SubscriptionDispose: not fired after dispose");
        }

        private sealed class Owner
        {
            public int Hits;
            public void OnEvent() { Hits++; }
        }

        private static void UnsubscribeAllByOwner()
        {
            var ch = new EventChannel();
            var owner = new Owner();
            ch.Subscribe(owner.OnEvent);          // guard defaults to target == owner
            ch.Subscribe(owner.OnEvent, guard: owner);
            int other = 0;
            ch.Subscribe(() => other++);
            int removed = ch.UnsubscribeAll(owner);
            Eq(removed, 2, "UnsubscribeAll: removed both owner listeners");
            ch.Raise();
            Eq(owner.Hits, 0, "UnsubscribeAll: owner listeners gone");
            Eq(other, 1, "UnsubscribeAll: unrelated listener kept");
        }

        private static void AddDuringRaiseDeferred()
        {
            var ch = new EventChannel();
            int lateHits = 0;
            int firstHits = 0;
            ch.Subscribe(() =>
            {
                firstHits++;
                ch.Subscribe(() => lateHits++);
            });
            ch.Raise();
            Eq(lateHits, 0, "AddDuringRaise: new listener not fired in the raise that added it");
            ch.Raise();
            Check(lateHits >= 1, "AddDuringRaise: new listener fires on the next raise");
            Eq(firstHits, 2, "AddDuringRaise: original fired both raises");
        }

        private static void RemoveDuringRaiseNotFired()
        {
            var ch = new EventChannel();
            int bHits = 0;
            Action b = () => bHits++;
            ch.Subscribe(() => ch.Unsubscribe(b), order: 0); // removes b before it runs
            ch.Subscribe(b, order: 1);
            ch.Raise();
            Eq(bHits, 0, "RemoveDuringRaise: a not-yet-fired listener removed mid-raise does not fire");
        }

        private static void RemoveCurrentlyFiring()
        {
            var ch = new EventChannel();
            int hits = 0;
            ch.Subscribe(() =>
            {
                hits++;
                ch.UnsubscribeCurrent();
            });
            ch.Raise();
            ch.Raise();
            Eq(hits, 1, "RemoveCurrentlyFiring: self-removed after first fire");
            Eq(ch.ListenerCount, 0, "RemoveCurrentlyFiring: detached");
        }

        private static void NestedRaise()
        {
            var outer = new EventChannel();
            var inner = new EventChannel();
            var log = new List<string>();
            inner.Subscribe(() => log.Add("inner"));
            outer.Subscribe(() =>
            {
                log.Add("outer-before");
                inner.Raise();
                log.Add("outer-after");
            });
            outer.Raise();
            Eq(string.Join(",", log.ToArray()), "outer-before,inner,outer-after", "NestedRaise: reentrant raise ordering");
        }

        private static void StaleGuardAutoDropped()
        {
            var ch = new EventChannel();
            var dead = new object();
            MessagingEnvironment.LivenessOf = g => !ReferenceEquals(g, dead);
            int hits = 0;
            ch.Subscribe(() => hits++, guard: dead);
            int alive = 0;
            ch.Subscribe(() => alive++);
            ch.Raise();
            Eq(hits, 0, "StaleGuard: dead-guard listener skipped");
            Eq(alive, 1, "StaleGuard: live listener fired");
            Eq(ch.ListenerCount, 1, "StaleGuard: dead listener auto-dropped");
            Reset();
        }

        private static void ClearRemovesAll()
        {
            var ch = new EventChannel();
            ch.Subscribe(() => { });
            ch.Subscribe(() => { });
            ch.Clear();
            Eq(ch.ListenerCount, 0, "Clear: all removed");
        }

        private static void PruneDropsStale()
        {
            var ch = new EventChannel();
            var dead = new object();
            ch.Subscribe(() => { }, guard: dead);
            ch.Subscribe(() => { });
            MessagingEnvironment.LivenessOf = g => !ReferenceEquals(g, dead);
            int removed = ch.RemoveStaleListeners();
            Eq(removed, 1, "Prune: one stale removed");
            Eq(ch.ListenerCount, 1, "Prune: live kept");
            Reset();
        }

        private static void SwitchEdgeCallbacks()
        {
            var sw = new StateSwitch(startOn: false);
            var log = new List<string>();
            sw.Subscribe(() => log.Add("on"), () => log.Add("off"));
            sw.TurnOn();
            sw.TurnOff();
            sw.TurnOn();
            Eq(string.Join(",", log.ToArray()), "on,off,on", "Switch: on/off edges broadcast paired callbacks");
        }

        private static void SwitchNoBroadcastOnSameState()
        {
            var sw = new StateSwitch(startOn: false);
            int on = 0, off = 0;
            sw.Subscribe(() => on++, () => off++);
            sw.TurnOff();       // already off
            sw.TurnOn();
            sw.TurnOn();        // already on
            Eq(on, 1, "SwitchSameState: on fires only on real transition");
            Eq(off, 0, "SwitchSameState: no spurious off");
        }

        private static void SwitchLateJoinFastTrack()
        {
            var sw = new StateSwitch(startOn: false);
            sw.TurnOn();
            int on = 0, off = 0;
            sw.Subscribe(() => on++, () => off++);   // joins while ON
            Eq(on, 1, "SwitchLateJoin: on-callback fast-tracked when already on");
            Eq(off, 0, "SwitchLateJoin: off-callback not run");

            var sw2 = new StateSwitch(startOn: false); // joining while OFF should not fast-track
            int on2 = 0;
            sw2.Subscribe(() => on2++, () => { });
            Eq(on2, 0, "SwitchLateJoin: no fast-track while off");
        }

        private static void SwitchUnsubscribeDetachesBoth()
        {
            var sw = new StateSwitch(startOn: false);
            int on = 0, off = 0;
            var sub = sw.Subscribe(() => on++, () => off++);
            sub.Dispose();
            sw.TurnOn();
            sw.TurnOff();
            Eq(on, 0, "SwitchUnsubscribe: on side detached");
            Eq(off, 0, "SwitchUnsubscribe: off side detached");
            Eq(sw.ListenerCount, 0, "SwitchUnsubscribe: count zero");
        }

        private static void SwitchToggle()
        {
            var sw = new StateSwitch(startOn: false);
            sw.Toggle();
            Check(sw.IsOn, "SwitchToggle: on after first toggle");
            sw.Toggle();
            Check(!sw.IsOn, "SwitchToggle: off after second toggle");
        }

        private static void CountIgnoresTombstones()
        {
            var ch = new EventChannel();
            Action a = () => { };
            Action b = () => { };
            ch.Subscribe(a);
            ch.Subscribe(b);
            ch.Unsubscribe(a);
            Eq(ch.ListenerCount, 1, "Count: tombstone excluded");
        }
    }
}
#endif
