using UnityEngine;

namespace PFound.Utilities.MathTools.Unity
{
    /// <summary>
    /// Integer vector helpers (Vector2Int / Vector3Int): row-major grid index
    /// conversions plus component-wise sign, abs and single-component copies.
    /// </summary>
    public static class IntVectorMath
    {
        // ---- Grid index <-> coordinate (row-major) ------------------------------

        /// <summary>Flattens a grid coordinate to a linear index for a grid <paramref name="width"/> columns wide.</summary>
        public static int ToIndex(Vector2Int coordinate, int width)
        {
            return coordinate.y * width + coordinate.x;
        }

        /// <summary>Expands a linear index back into a grid coordinate for a grid <paramref name="width"/> columns wide.</summary>
        public static Vector2Int ToCoordinate(int index, int width)
        {
            return new Vector2Int(index % width, index / width);
        }

        // ---- Component-wise --------------------------------------------------

        public static Vector2Int Sign(Vector2Int v) =>
            new Vector2Int(System.Math.Sign(v.x), System.Math.Sign(v.y));

        public static Vector3Int Sign(Vector3Int v) =>
            new Vector3Int(System.Math.Sign(v.x), System.Math.Sign(v.y), System.Math.Sign(v.z));

        public static Vector2Int Abs(Vector2Int v) =>
            new Vector2Int(Mathf.Abs(v.x), Mathf.Abs(v.y));

        public static Vector3Int Abs(Vector3Int v) =>
            new Vector3Int(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ---- Single-component copies -------------------------------------------

        public static Vector2Int WithX(Vector2Int v, int x) => new Vector2Int(x, v.y);
        public static Vector2Int WithY(Vector2Int v, int y) => new Vector2Int(v.x, y);

        public static Vector3Int WithX(Vector3Int v, int x) => new Vector3Int(x, v.y, v.z);
        public static Vector3Int WithY(Vector3Int v, int y) => new Vector3Int(v.x, y, v.z);
        public static Vector3Int WithZ(Vector3Int v, int z) => new Vector3Int(v.x, v.y, z);
    }
}
