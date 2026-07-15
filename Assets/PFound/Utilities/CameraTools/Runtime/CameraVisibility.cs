using UnityEngine;

namespace PFound.Utilities.CameraTools
{
    /// <summary>
    /// Frustum-culling predicates built on Unity's <see cref="GeometryUtility"/>. Every
    /// query reduces to an axis-aligned bounding volume tested against a set of six frustum
    /// planes, so callers can either pass a <see cref="Camera"/> (planes recomputed each call)
    /// or supply a pre-baked plane array when the same camera is queried many times per frame.
    /// </summary>
    public static class CameraVisibility
    {
        /// <summary>
        /// Fills <paramref name="planes"/> (length 6) with the frustum planes of
        /// <paramref name="camera"/>. Reuse the array across calls to avoid per-frame garbage.
        /// </summary>
        public static void CollectFrustumPlanes(Camera camera, Plane[] planes)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
        }

        /// <summary>Allocates and returns the six frustum planes of <paramref name="camera"/>.</summary>
        public static Plane[] CollectFrustumPlanes(Camera camera)
        {
            return GeometryUtility.CalculateFrustumPlanes(camera);
        }

        /// <summary>True when any part of <paramref name="bounds"/> falls inside the frustum described by <paramref name="frustumPlanes"/>.</summary>
        public static bool IsBoundsInside(Bounds bounds, Plane[] frustumPlanes)
        {
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        /// <summary>True when any part of <paramref name="bounds"/> is inside <paramref name="camera"/>'s view frustum.</summary>
        public static bool IsBoundsInside(Bounds bounds, Camera camera)
        {
            return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds);
        }

        /// <summary>True when the world-space bounds of <paramref name="renderer"/> are visible to <paramref name="camera"/>.</summary>
        public static bool IsSeenBy(this Renderer renderer, Camera camera)
        {
            return renderer != null && IsBoundsInside(renderer.bounds, camera);
        }

        /// <summary>True when <paramref name="renderer"/>'s bounds are visible against pre-baked <paramref name="frustumPlanes"/>.</summary>
        public static bool IsSeenBy(this Renderer renderer, Plane[] frustumPlanes)
        {
            return renderer != null && IsBoundsInside(renderer.bounds, frustumPlanes);
        }

        /// <summary>True when the world-space bounds of <paramref name="collider"/> are visible to <paramref name="camera"/>.</summary>
        public static bool IsSeenBy(this Collider collider, Camera camera)
        {
            return collider != null && IsBoundsInside(collider.bounds, camera);
        }

        /// <summary>True when <paramref name="collider"/>'s bounds are visible against pre-baked <paramref name="frustumPlanes"/>.</summary>
        public static bool IsSeenBy(this Collider collider, Plane[] frustumPlanes)
        {
            return collider != null && IsBoundsInside(collider.bounds, frustumPlanes);
        }
    }
}
