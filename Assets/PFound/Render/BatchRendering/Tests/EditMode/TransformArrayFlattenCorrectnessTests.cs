using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using PFound.Render.BatchRendering;

namespace PFound.Render.Tests.BatchRendering
{
    /// <summary>
    /// Covers FR-033 / FR-015 — <see cref="TransformArrayInstanceSource"/> flatten correctness and
    /// null-entry warn-once gating.
    /// </summary>
    public sealed class TransformArrayFlattenCorrectnessTests
    {
        [UnityTest]
        public IEnumerator FlattenedView_MatchesTransformLocalToWorld()
        {
            var goA = new GameObject("__flatten_test_a__");
            goA.transform.position = new Vector3(1, 2, 3);
            var goB = new GameObject("__flatten_test_b__");
            goB.transform.position = new Vector3(-5, 0, 10);
            goB.transform.rotation = Quaternion.Euler(0, 45, 0);

            var transforms = new Transform[] { goA.transform, goB.transform };
            var source = new TransformArrayInstanceSource(transforms);

            try
            {
                Assert.AreEqual(2, source.Count);

                // Schedule one tick + complete to populate _flatView.
                source.OnTickBegin(default, out var handle);
                handle.Complete();

                Assert.IsTrue(source.TryGetNativeArrayView(out var view));
                Assert.AreEqual(2, view.Length);

                // Each entry should match the Transform's localToWorldMatrix.
                Matrix4x4 expectedA = goA.transform.localToWorldMatrix;
                Matrix4x4 actualA = (Matrix4x4)view[0];
                AssertMatricesEqual(expectedA, actualA, 0.001f);

                Matrix4x4 expectedB = goB.transform.localToWorldMatrix;
                Matrix4x4 actualB = (Matrix4x4)view[1];
                AssertMatricesEqual(expectedB, actualB, 0.001f);
            }
            finally
            {
                source.Dispose();
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
            yield return null;
        }

        [Test]
        public void Ctor_NullArray_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TransformArrayInstanceSource(null));
        }

        [UnityTest]
        public IEnumerator NullEntry_LogsWarningOnce_ThenSilent()
        {
            var go = new GameObject("__flatten_null_test__");
            try
            {
                var transforms = new Transform[] { go.transform, null, go.transform };
                var source = new TransformArrayInstanceSource(transforms);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*null Transform.*"));

                source.OnTickBegin(default, out var h1);
                h1.Complete();

                // Subsequent tick — no further warning expected.
                source.OnTickBegin(default, out var h2);
                h2.Complete();

                source.Dispose();
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryGetComputeBuffer_ReturnsFalse()
        {
            var go = new GameObject("__flatten_buffer_check__");
            try
            {
                var source = new TransformArrayInstanceSource(new[] { go.transform });
                Assert.IsFalse(source.TryGetComputeBuffer(out var buffer, out var stride));
                Assert.IsNull(buffer);
                Assert.AreEqual(0, stride);
                source.Dispose();
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void AssertMatricesEqual(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
        {
            for (int i = 0; i < 16; i++)
            {
                float diff = Mathf.Abs(expected[i] - actual[i]);
                Assert.LessOrEqual(diff, tolerance, $"Matrix element {i} differs by {diff} (expected {expected[i]}, got {actual[i]}).");
            }
        }
    }
}
