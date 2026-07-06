using Unity.Mathematics;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Six camera frustum planes packed as <c>float4</c> values (xyz = normal, w = distance), in the
    /// order Unity returns from <c>GeometryUtility.CalculateFrustumPlanes</c>:
    /// Left, Right, Bottom, Top, Near, Far.
    /// </summary>
    /// <remarks>
    /// This struct is Burst-compatible (pure value type, no managed references). Use
    /// <see cref="FromCamera"/> to populate it once per camera per frame from the main thread;
    /// pass it by value into Burst-compiled cull jobs.
    /// </remarks>
    internal struct FrustumPlanes
    {
        public float4 Plane0;
        public float4 Plane1;
        public float4 Plane2;
        public float4 Plane3;
        public float4 Plane4;
        public float4 Plane5;

        /// <summary>
        /// Computes the frustum planes for <paramref name="camera"/> into <paramref name="planes"/>.
        /// </summary>
        /// <remarks>
        /// Uses a stack-allocated scratch <c>Plane[6]</c> array — Unity's
        /// <c>GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])</c> overload writes into a
        /// caller-supplied array; we then copy the values into the <see cref="FrustumPlanes"/>
        /// fields. The scratch array is the only managed allocation per camera per frame from this
        /// helper, and it is reused via the caller-passed reference (Burst-safe at the call site).
        /// </remarks>
        public static void FromCamera(Camera camera, Plane[] scratch, out FrustumPlanes planes)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, scratch);
            planes = default;
            planes.Plane0 = PlaneToFloat4(scratch[0]);
            planes.Plane1 = PlaneToFloat4(scratch[1]);
            planes.Plane2 = PlaneToFloat4(scratch[2]);
            planes.Plane3 = PlaneToFloat4(scratch[3]);
            planes.Plane4 = PlaneToFloat4(scratch[4]);
            planes.Plane5 = PlaneToFloat4(scratch[5]);
        }

        private static float4 PlaneToFloat4(Plane p)
        {
            return new float4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        }
    }
}
