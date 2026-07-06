using UnityEngine;

namespace PFound.Utilities.MathTools.Unity
{
    /// <summary>
    /// UnityEngine <see cref="Bounds"/> helpers, currently transforming an
    /// axis-aligned box by an arbitrary matrix and re-fitting an AABB around the result.
    /// </summary>
    public static class BoundsMath
    {
        /// <summary>
        /// Transforms an axis-aligned box by <paramref name="matrix"/> and returns the tightest
        /// axis-aligned box that still contains it. Uses the absolute-value trick on the matrix
        /// basis so only center + extents need transforming, not all eight corners.
        /// </summary>
        public static Bounds Transform(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;

            Vector3 newExtents = new Vector3(
                Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z);

            Bounds result = new Bounds(center, Vector3.zero);
            result.extents = newExtents;
            return result;
        }
    }
}
