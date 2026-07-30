using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class ChangeEventTests
    {
        private static readonly PrefKey<int> Counter = new PrefKey<int>("evt.counter", 0);
        private static readonly PrefKey<string> Tag = new PrefKey<string>("evt.tag", "default");

        private IPrefsStore _store;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(Counter.Key);
            PlayerPrefs.DeleteKey(Tag.Key);
            _store = new PrefsBuilder()
                .Add(Counter)
                .Add(Tag)
                .WithLogger(new NullLogger())
                .Build();
        }

        [TearDown]
        public void TearDown()
        {
            _store?.Dispose();
            PlayerPrefs.DeleteKey(Counter.Key);
            PlayerPrefs.DeleteKey(Tag.Key);
        }

        [Test]
        public void Set_DifferentValue_FiresEvent()
        {
            var events = new List<PrefChange<int>>();
            _store.Subscribe(Counter, c => events.Add(c));

            _store.Set(Counter, 5);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(0, events[0].OldValue);
            Assert.AreEqual(5, events[0].NewValue);
            Assert.AreEqual(PrefChangeKind.Set, events[0].Kind);
            Assert.AreEqual(Counter.Key, events[0].Key);
        }

        [Test]
        public void Set_SameValue_DoesNotFireEvent()
        {
            _store.Set(Counter, 7);

            var events = new List<PrefChange<int>>();
            _store.Subscribe(Counter, c => events.Add(c));

            _store.Set(Counter, 7); // same as current

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Clear_FiresEventWithDefaultAsNew()
        {
            _store.Set(Counter, 12);

            var events = new List<PrefChange<int>>();
            _store.Subscribe(Counter, c => events.Add(c));

            _store.Clear(Counter);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(PrefChangeKind.Cleared, events[0].Kind);
            Assert.AreEqual(12, events[0].OldValue);
            Assert.AreEqual(0, events[0].NewValue); // default
        }

        [Test]
        public void Subscribe_ReturnsDisposable_UnsubscribesOnDispose()
        {
            var events = new List<PrefChange<int>>();
            var sub = _store.Subscribe(Counter, c => events.Add(c));

            _store.Set(Counter, 1);
            sub.Dispose();
            _store.Set(Counter, 2);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, events[0].NewValue);
        }

        [Test]
        public void MultipleSubscribers_AllReceive()
        {
            int aCount = 0, bCount = 0;
            _store.Subscribe(Counter, _ => aCount++);
            _store.Subscribe(Counter, _ => bCount++);

            _store.Set(Counter, 99);

            Assert.AreEqual(1, aCount);
            Assert.AreEqual(1, bCount);
        }

        [Test]
        public void Subscribe_DifferentKeys_OnlyMatchingFires()
        {
            int counterEvents = 0, tagEvents = 0;
            _store.Subscribe(Counter, _ => counterEvents++);
            _store.Subscribe(Tag, _ => tagEvents++);

            _store.Set(Counter, 5);

            Assert.AreEqual(1, counterEvents);
            Assert.AreEqual(0, tagEvents);
        }

        [Test]
        public void Subscribe_NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _store.Subscribe<int>(Counter, null));
        }
    }
}
