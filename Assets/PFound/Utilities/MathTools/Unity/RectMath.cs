using UnityEngine;

namespace PFound.Utilities.MathTools.Unity
{
    /// <summary>
    /// UnityEngine <see cref="Rect"/> helpers: construction from corners, growing,
    /// merging and clamping a point into the rectangle.
    /// </summary>
    public static class RectMath
    {
        /// <summary>Builds a rect from its minimum and maximum corners.</summary>
        public static Rect FromMinMax(Vector2 min, Vector2 max)
        {
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>Grows the rect outward on every side by <paramref name="amount"/> (negative shrinks it).</summary>
        public static Rect Expand(Rect rect, float amount)
        {
            return new Rect(
                rect.xMin - amount,
                rect.yMin - amount,
                rect.width + amount * 2f,
                rect.height + amount * 2f);
        }

        /// <summary>Grows the rect by a separate horizontal and vertical amount.</summary>
        public static Rect Expand(Rect rect, float horizontal, float vertical)
        {
            return new Rect(
                rect.xMin - horizontal,
                rect.yMin - vertical,
                rect.width + horizontal * 2f,
                rect.height + vertical * 2f);
        }

        /// <summary>The smallest rect that contains both inputs.</summary>
        public static Rect Union(Rect a, Rect b)
        {
            float minX = Mathf.Min(a.xMin, b.xMin);
            float minY = Mathf.Min(a.yMin, b.yMin);
            float maxX = Mathf.Max(a.xMax, b.xMax);
            float maxY = Mathf.Max(a.yMax, b.yMax);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>Grows the rect just enough to include <paramref name="point"/>.</summary>
        public static Rect Encapsulate(Rect rect, Vector2 point)
        {
            float minX = Mathf.Min(rect.xMin, point.x);
            float minY = Mathf.Min(rect.yMin, point.y);
            float maxX = Mathf.Max(rect.xMax, point.x);
            float maxY = Mathf.Max(rect.yMax, point.y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>Clamps a point so it lies on or inside the rectangle.</summary>
        public static Vector2 ClampPoint(Rect rect, Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }
    }
}
