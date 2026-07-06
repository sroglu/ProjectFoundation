using UnityEngine;

namespace PFound.Utilities.MathTools.Unity
{
    /// <summary>
    /// UnityEngine floating-point vector helpers (Vector2 / Vector3): component-wise
    /// arithmetic, per-component reductions, single-component copies, planar utilities
    /// and rotation about a pivot.
    /// </summary>
    public static class VectorMath
    {
        // ---- Component-wise arithmetic -----------------------------------------

        public static Vector2 Multiply(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector3 Multiply(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        public static Vector2 Divide(Vector2 a, Vector2 b) => new Vector2(a.x / b.x, a.y / b.y);
        public static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

        // ---- Reductions / shaping ----------------------------------------------

        public static Vector2 Midpoint(Vector2 a, Vector2 b) => (a + b) * 0.5f;
        public static Vector3 Midpoint(Vector3 a, Vector3 b) => (a + b) * 0.5f;

        public static Vector2 Abs(Vector2 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
        public static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        public static Vector2 Sign(Vector2 v) => new Vector2(Mathf.Sign(v.x), Mathf.Sign(v.y));
        public static Vector3 Sign(Vector3 v) => new Vector3(Mathf.Sign(v.x), Mathf.Sign(v.y), Mathf.Sign(v.z));

        public static float MinComponent(Vector2 v) => Mathf.Min(v.x, v.y);
        public static float MinComponent(Vector3 v) => Mathf.Min(v.x, Mathf.Min(v.y, v.z));

        public static float MaxComponent(Vector2 v) => Mathf.Max(v.x, v.y);
        public static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

        /// <summary>Shrinks the vector so its magnitude never exceeds <paramref name="maxLength"/>.</summary>
        public static Vector2 ClampLength(Vector2 v, float maxLength)
        {
            float sqr = v.sqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                return v * (maxLength / Mathf.Sqrt(sqr));
            }
            return v;
        }

        /// <summary>Shrinks the vector so its magnitude never exceeds <paramref name="maxLength"/>.</summary>
        public static Vector3 ClampLength(Vector3 v, float maxLength)
        {
            float sqr = v.sqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                return v * (maxLength / Mathf.Sqrt(sqr));
            }
            return v;
        }

        // ---- Single-component copies -------------------------------------------

        public static Vector2 WithX(Vector2 v, float x) => new Vector2(x, v.y);
        public static Vector2 WithY(Vector2 v, float y) => new Vector2(v.x, y);

        public static Vector3 WithX(Vector3 v, float x) => new Vector3(x, v.y, v.z);
        public static Vector3 WithY(Vector3 v, float y) => new Vector3(v.x, y, v.z);
        public static Vector3 WithZ(Vector3 v, float z) => new Vector3(v.x, v.y, z);

        // ---- Rotation & angles --------------------------------------------------

        /// <summary>Rotates <paramref name="point"/> around <paramref name="pivot"/> by <paramref name="degrees"/> (2D, CCW).</summary>
        public static Vector2 RotateAround(Vector2 point, Vector2 pivot, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            Vector2 offset = point - pivot;
            return new Vector2(
                pivot.x + offset.x * cos - offset.y * sin,
                pivot.y + offset.x * sin + offset.y * cos);
        }

        /// <summary>Rotates <paramref name="point"/> around <paramref name="pivot"/> by a Quaternion.</summary>
        public static Vector3 RotateAround(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return pivot + rotation * (point - pivot);
        }

        /// <summary>Signed angle in degrees from <paramref name="from"/> to <paramref name="to"/> (2D, CCW positive).</summary>
        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            float unsigned = Vector2.Angle(from, to);
            float sign = Mathf.Sign(from.x * to.y - from.y * to.x);
            return unsigned * sign;
        }

        // ---- Planar helpers -----------------------------------------------------

        /// <summary>Squared distance between two points projected onto the XZ (ground) plane.</summary>
        public static float SquaredDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
