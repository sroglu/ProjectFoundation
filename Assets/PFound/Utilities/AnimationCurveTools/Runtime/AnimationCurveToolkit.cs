using UnityEngine;

namespace PFound.Utilities.AnimationCurveTools
{
    /// <summary>
    /// Extension helpers for duplicating and comparing <see cref="AnimationCurve"/>
    /// instances. Unity treats curves as reference types, so a straight assignment
    /// aliases the same keyframe buffer; these helpers give proper copy and
    /// value-equality semantics.
    /// </summary>
    public static class AnimationCurveToolkit
    {
        /// <summary>
        /// Produces an independent copy of <paramref name="source"/>, including its
        /// keyframes (with tangents and weights) and the pre/post wrap modes.
        /// </summary>
        public static AnimationCurve Duplicate(this AnimationCurve source)
        {
            var copy = new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            return copy;
        }

        /// <summary>
        /// Compares two curves by content: wrap modes, keyframe count, and every
        /// keyframe field. Two <c>null</c> references are considered equal.
        /// </summary>
        public static bool ContentEquals(this AnimationCurve left, AnimationCurve right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            if (left.length != right.length)
            {
                return false;
            }
            if (left.preWrapMode != right.preWrapMode || left.postWrapMode != right.postWrapMode)
            {
                return false;
            }

            for (int i = 0; i < left.length; i++)
            {
                if (!KeyframesEqual(left[i], right[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool KeyframesEqual(Keyframe a, Keyframe b)
        {
            return a.time == b.time
                && a.value == b.value
                && a.inTangent == b.inTangent
                && a.outTangent == b.outTangent
                && a.inWeight == b.inWeight
                && a.outWeight == b.outWeight
                && a.weightedMode == b.weightedMode;
        }
    }
}
