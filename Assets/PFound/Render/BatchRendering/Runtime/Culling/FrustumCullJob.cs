using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Burst-compiled per-instance frustum cull. Conservative bounding-sphere-vs-six-planes test;
    /// instances whose world-space sphere is fully outside any plane are excluded.
    /// </summary>
    /// <remarks>
    /// Algorithm follows research.md R3: AoS layout, sphere-vs-plane with early-out, batch size 64,
    /// <see cref="FloatMode.Fast"/>. Conservative bound (false positives draw; false negatives are
    /// bugs). World-space radius is derived from the local-space sphere scaled by the maximum-axis
    /// scale of the per-instance <c>LocalToWorld</c> matrix — exact for uniform scale, slightly
    /// conservative for non-uniform.
    /// <para>
    /// Visible indices are appended via <see cref="NativeList{T}.ParallelWriter.AddNoResize"/>; the
    /// caller MUST have called <c>EnsureCapacity</c> on the underlying list before scheduling.
    /// </para>
    /// </remarks>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    internal struct FrustumCullJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4x4> Matrices;
        [ReadOnly] public FrustumPlanes Planes;
        [ReadOnly] public float3 MeshLocalCenter;
        [ReadOnly] public float MeshLocalRadius;

        public NativeList<int>.ParallelWriter VisibleIndices;

        public void Execute(int i)
        {
            float4x4 ltw = Matrices[i];

            // World-space center via matrix-point-transform.
            float3 center = math.transform(ltw, MeshLocalCenter);

            // World-space radius: scale by the maximum-axis scale magnitude. Conservative for
            // non-uniform scale.
            float scaleX = math.length(ltw.c0.xyz);
            float scaleY = math.length(ltw.c1.xyz);
            float scaleZ = math.length(ltw.c2.xyz);
            float maxScale = math.max(scaleX, math.max(scaleY, scaleZ));
            float radius = MeshLocalRadius * maxScale;

            // Sphere-vs-6-planes with early-out. Unity's GeometryUtility plane normals point
            // INWARD into the frustum: a point on the inside has dot(n, p) + d >= 0.
            if (!InsidePlane(Planes.Plane0, center, radius)) return;
            if (!InsidePlane(Planes.Plane1, center, radius)) return;
            if (!InsidePlane(Planes.Plane2, center, radius)) return;
            if (!InsidePlane(Planes.Plane3, center, radius)) return;
            if (!InsidePlane(Planes.Plane4, center, radius)) return;
            if (!InsidePlane(Planes.Plane5, center, radius)) return;

            VisibleIndices.AddNoResize(i);
        }

        private static bool InsidePlane(float4 plane, float3 center, float radius)
        {
            // plane.xyz = inward normal; plane.w = distance. Visible iff the sphere is not fully
            // behind the plane (signed distance + radius >= 0).
            float d = math.dot(plane.xyz, center) + plane.w;
            return d >= -radius;
        }
    }
}
