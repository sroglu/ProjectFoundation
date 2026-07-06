using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-033 — instance-source contract: Count semantics, mutually-exclusive accessor
    /// contract, null-arg guards on <see cref="NativeArrayInstanceSource"/> ctor.
    /// </summary>
    public sealed class InstanceSourceContractsTests
    {
        [Test]
        public void Ctor_UncreatedNativeArray_ThrowsArgumentNullException()
        {
            // default(NativeArray<float4x4>) has IsCreated == false.
            var uncreated = default(NativeArray<float4x4>);
            Assert.Throws<ArgumentNullException>(() => new NativeArrayInstanceSource(uncreated));
        }

        [Test]
        public void Ctor_NegativeCount_ThrowsArgumentOutOfRange()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NativeArrayInstanceSource(arr, -1));
        }

        [Test]
        public void Ctor_CountGreaterThanLength_ThrowsArgumentOutOfRange()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NativeArrayInstanceSource(arr, 11));
        }

        [Test]
        public void Count_DefaultsToArrayLength()
        {
            using var arr = new NativeArray<float4x4>(42, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr);
            Assert.AreEqual(42, src.Count);
        }

        [Test]
        public void Count_RespectsCtorOverride()
        {
            using var arr = new NativeArray<float4x4>(42, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr, 17);
            Assert.AreEqual(17, src.Count);
        }

        [Test]
        public void SetCount_MutatesCount()
        {
            using var arr = new NativeArray<float4x4>(42, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr);
            src.SetCount(7);
            Assert.AreEqual(7, src.Count);
            src.SetCount(0);
            Assert.AreEqual(0, src.Count);
            src.SetCount(42);
            Assert.AreEqual(42, src.Count);
        }

        [Test]
        public void SetCount_OutOfRange_Throws()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr);
            Assert.Throws<ArgumentOutOfRangeException>(() => src.SetCount(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => src.SetCount(11));
        }

        [Test]
        public void TryGetNativeArrayView_ReturnsTrueWithExpectedLength()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr, 7);
            Assert.IsTrue(src.TryGetNativeArrayView(out var view));
            Assert.AreEqual(7, view.Length);
        }

        [Test]
        public void TryGetComputeBuffer_ReturnsFalseOnNativeArraySource()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr);
            Assert.IsFalse(src.TryGetComputeBuffer(out var buffer, out var stride));
            Assert.IsNull(buffer);
            Assert.AreEqual(0, stride);
        }

        [Test]
        public void TryGetNativeArrayView_AndTryGetComputeBuffer_AreMutuallyExclusive()
        {
            using var arr = new NativeArray<float4x4>(10, Allocator.TempJob);
            var src = new NativeArrayInstanceSource(arr);
            bool hasView = src.TryGetNativeArrayView(out _);
            bool hasBuffer = src.TryGetComputeBuffer(out _, out _);
            Assert.IsTrue(hasView != hasBuffer, "Exactly one of TryGetNativeArrayView / TryGetComputeBuffer must return true.");
        }
    }
}
