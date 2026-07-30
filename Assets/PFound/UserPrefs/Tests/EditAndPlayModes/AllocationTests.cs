using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class AllocationTests
    {
        private static readonly PrefKey<int> IntKey = new PrefKey<int>("alloc.int", 42);
        private static readonly PrefKey<float> FloatKey = new PrefKey<float>("alloc.float", 0.5f);
        private static readonly PrefKey<bool> BoolKey = new PrefKey<bool>("alloc.bool", true);

        private IPrefsStore _store;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(IntKey.Key);
            PlayerPrefs.DeleteKey(FloatKey.Key);
            PlayerPrefs.DeleteKey(BoolKey.Key);

            _store = new PrefsBuilder()
                .Add(IntKey)
                .Add(FloatKey)
                .Add(BoolKey)
                .WithLogger(new NullLogger())
                .Build();

            _store.Set(IntKey, 99);
            _store.Set(FloatKey, 0.8f);
            _store.Set(BoolKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            _store?.Dispose();
            PlayerPrefs.DeleteKey(IntKey.Key);
            PlayerPrefs.DeleteKey(FloatKey.Key);
            PlayerPrefs.DeleteKey(BoolKey.Key);
        }

        [Test]
        public void Get_Int_ZeroAllocations()
        {
            // Warm-up to JIT and initial allocations
            _store.Get(IntKey);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _store.Get(IntKey);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before, $"Get<int> allocated {after - before} bytes over 1000 calls.");
        }

        [Test]
        public void Get_Float_ZeroAllocations()
        {
            _store.Get(FloatKey);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _store.Get(FloatKey);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before, $"Get<float> allocated {after - before} bytes over 1000 calls.");
        }

        [Test]
        public void Get_Bool_ZeroAllocations()
        {
            _store.Get(BoolKey);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _store.Get(BoolKey);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before, $"Get<bool> allocated {after - before} bytes over 1000 calls.");
        }
    }
}
