using UnityEngine;

namespace PFound.Utilities.InputTools
{
    /// <summary>
    /// Small, backend-agnostic input helpers: projecting screen points onto a
    /// world plane, physics picking under a screen point, touch lookup by finger
    /// id, and vertical-axis inversion. The pure geometry entry points take an
    /// explicit screen point so they work with any input source; convenience
    /// overloads read the legacy <see cref="Input"/> cursor for quick use.
    /// </summary>
    public static class InputToolkit
    {
        /// <summary>
        /// Intersects <paramref name="ray"/> with <paramref name="plane"/>.
        /// Returns false when the ray runs parallel to (or away from) the plane.
        /// </summary>
        public static bool RaycastPlane(Ray ray, Plane plane, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Intersects the ray through <paramref name="screenPoint"/> (as seen by
        /// <paramref name="camera"/>) with <paramref name="plane"/>.
        /// </summary>
        public static bool RaycastPlane(Camera camera, Vector2 screenPoint, Plane plane, out Vector3 point)
        {
            return RaycastPlane(camera.ScreenPointToRay(screenPoint), plane, out point);
        }

        /// <summary>
        /// Intersects the ray through <paramref name="screenPoint"/> with the
        /// horizontal ground plane at height <paramref name="groundHeight"/>.
        /// </summary>
        public static bool RaycastGround(Camera camera, Vector2 screenPoint, out Vector3 point, float groundHeight = 0f)
        {
            var ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            return RaycastPlane(camera, screenPoint, ground, out point);
        }

        /// <summary>
        /// World point where the legacy mouse cursor meets the ground plane at
        /// <paramref name="groundHeight"/>.
        /// </summary>
        public static bool ProjectCursorToGround(Camera camera, out Vector3 point, float groundHeight = 0f)
        {
            return RaycastGround(camera, Input.mousePosition, out point, groundHeight);
        }

        /// <summary>
        /// Physics-picks the closest collider along the ray through
        /// <paramref name="screenPoint"/>.
        /// </summary>
        public static bool PickUnderScreenPoint(Camera camera, Vector2 screenPoint, out RaycastHit hit,
            float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers)
        {
            return Physics.Raycast(camera.ScreenPointToRay(screenPoint), out hit, maxDistance, layerMask);
        }

        /// <summary>
        /// GameObject of the closest collider under <paramref name="screenPoint"/>,
        /// or <c>null</c> when nothing is hit.
        /// </summary>
        public static GameObject PickObjectUnderScreenPoint(Camera camera, Vector2 screenPoint,
            float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers)
        {
            if (PickUnderScreenPoint(camera, screenPoint, out RaycastHit hit, maxDistance, layerMask))
            {
                return hit.collider.gameObject;
            }
            return null;
        }

        /// <summary>
        /// GameObject beneath the legacy mouse cursor, or <c>null</c>.
        /// </summary>
        public static GameObject PickObjectUnderCursor(Camera camera,
            float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers)
        {
            return PickObjectUnderScreenPoint(camera, Input.mousePosition, maxDistance, layerMask);
        }

        /// <summary>
        /// Finds the active legacy touch whose <see cref="Touch.fingerId"/> equals
        /// <paramref name="fingerId"/>.
        /// </summary>
        public static bool TryGetTouch(int fingerId, out Touch touch)
        {
            int count = Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                Touch candidate = Input.GetTouch(i);
                if (candidate.fingerId == fingerId)
                {
                    touch = candidate;
                    return true;
                }
            }
            touch = default;
            return false;
        }

        /// <summary>Negates the Y component of a 2D vector.</summary>
        public static Vector2 InvertY(Vector2 value)
        {
            return new Vector2(value.x, -value.y);
        }

        /// <summary>Negates the Y component of a 3D vector, leaving X and Z intact.</summary>
        public static Vector3 InvertY(Vector3 value)
        {
            return new Vector3(value.x, -value.y, value.z);
        }

        /// <summary>
        /// Flips a screen-space Y coordinate against a viewport of
        /// <paramref name="height"/> pixels (top-left origin to bottom-left origin
        /// or vice versa).
        /// </summary>
        public static Vector2 FlipScreenY(Vector2 screenPoint, float height)
        {
            return new Vector2(screenPoint.x, height - screenPoint.y);
        }
    }
}
