using NUnit.Framework;
using PFound.Utilities.AnimationCurveTools;
using UnityEngine;

namespace PFound.Utilities.AnimationCurveTools.Tests
{
    public class AnimationCurveToolkitTests
    {
        private static AnimationCurve MakeCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 2f),
                new Keyframe(1f, 1f))
            {
                preWrapMode = WrapMode.PingPong,
                postWrapMode = WrapMode.Loop
            };
            return curve;
        }

        [Test]
        public void Duplicate_ProducesIndependentCopy()
        {
            AnimationCurve original = MakeCurve();

            AnimationCurve copy = original.Duplicate();

            Assert.AreNotSame(original, copy);
            Assert.IsTrue(original.ContentEquals(copy));

            // Mutating the copy must not touch the original.
            copy.MoveKey(1, new Keyframe(0.5f, 99f));
            Assert.AreEqual(2f, original[1].value);
            Assert.IsFalse(original.ContentEquals(copy));
        }

        [Test]
        public void Duplicate_PreservesWrapModes()
        {
            AnimationCurve copy = MakeCurve().Duplicate();

            Assert.AreEqual(WrapMode.PingPong, copy.preWrapMode);
            Assert.AreEqual(WrapMode.Loop, copy.postWrapMode);
        }

        [Test]
        public void ContentEquals_TrueForIdenticalContent()
        {
            Assert.IsTrue(MakeCurve().ContentEquals(MakeCurve()));
        }

        [Test]
        public void ContentEquals_FalseWhenKeyValueDiffers()
        {
            AnimationCurve a = MakeCurve();
            AnimationCurve b = MakeCurve();
            b.MoveKey(2, new Keyframe(1f, 5f));

            Assert.IsFalse(a.ContentEquals(b));
        }

        [Test]
        public void ContentEquals_FalseWhenKeyCountDiffers()
        {
            AnimationCurve a = MakeCurve();
            AnimationCurve b = MakeCurve();
            b.AddKey(2f, 3f);

            Assert.IsFalse(a.ContentEquals(b));
        }

        [Test]
        public void ContentEquals_FalseWhenWrapModeDiffers()
        {
            AnimationCurve a = MakeCurve();
            AnimationCurve b = MakeCurve();
            b.postWrapMode = WrapMode.Clamp;

            Assert.IsFalse(a.ContentEquals(b));
        }

        [Test]
        public void ContentEquals_BothNull_IsTrue()
        {
            Assert.IsTrue(AnimationCurveToolkit.ContentEquals(null, null));
        }

        [Test]
        public void ContentEquals_OneNull_IsFalse()
        {
            Assert.IsFalse(MakeCurve().ContentEquals(null));
        }
    }
}
