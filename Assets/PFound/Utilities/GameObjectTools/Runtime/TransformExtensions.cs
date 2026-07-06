using UnityEngine;

namespace PFound.Utilities.GameObjectTools
{
    /// <summary>
    /// Convenience operations on a single <see cref="Transform"/>: recentering a
    /// parent onto its children, resetting local TRS, ground snapping via a downward
    /// raycast, and clearing a subtree.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Moves <paramref name="parent"/> to the average world position of its direct
        /// children while holding every child fixed in world space, so the pivot ends up
        /// centered without visually shifting the contents. No-op when childless.
        /// </summary>
        public static void CenterOnChildren(this Transform parent)
        {
            int count = parent.childCount;
            if (count == 0) return;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < count; i++)
                sum += parent.GetChild(i).position;
            Vector3 center = sum / count;

            // Re-anchor the children to world positions before nudging the pivot,
            // so the visible layout is preserved.
            var worldPositions = new Vector3[count];
            for (int i = 0; i < count; i++)
                worldPositions[i] = parent.GetChild(i).position;

            parent.position = center;

            for (int i = 0; i < count; i++)
                parent.GetChild(i).position = worldPositions[i];
        }

        /// <summary>Resets local position, rotation and scale to identity in one call.</summary>
        public static void ResetLocal(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>Sets local position to zero.</summary>
        public static void ResetLocalPosition(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
        }

        /// <summary>Sets local rotation to identity.</summary>
        public static void ResetLocalRotation(this Transform transform)
        {
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Sets local scale to one.</summary>
        public static void ResetLocalScale(this Transform transform)
        {
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Casts a ray straight down from a point above <paramref name="transform"/> and,
        /// on a hit, drops the transform onto the surface (offset by
        /// <paramref name="surfaceOffset"/> along the hit normal). Returns whether ground was found.
        /// </summary>
        /// <param name="maxDistance">Total ray length; the origin starts half this distance above the transform.</param>
        /// <param name="groundMask">Layers considered ground. Defaults to everything.</param>
        /// <param name="surfaceOffset">Distance kept between the pivot and the surface along its normal.</param>
        public static bool SnapToGround(this Transform transform, float maxDistance = 100f, LayerMask? groundMask = null, float surfaceOffset = 0f)
        {
            LayerMask mask = groundMask ?? ~0;
            Vector3 origin = transform.position + Vector3.up * (maxDistance * 0.5f);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * surfaceOffset;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys every child of <paramref name="parent"/>, clearing the subtree beneath
        /// it. Uses immediate destruction outside play mode so it is safe from editor tooling.
        /// </summary>
        public static void DestroyChildren(this Transform parent)
        {
            int count = parent.childCount;
            if (count == 0) return;

            // Collect first: destroying alters childCount/indices mid-iteration.
            var doomed = new Transform[count];
            for (int i = 0; i < count; i++)
                doomed[i] = parent.GetChild(i);

            bool immediate = !Application.isPlaying;
            for (int i = 0; i < count; i++)
            {
                if (doomed[i] == null) continue;
                if (immediate)
                    Object.DestroyImmediate(doomed[i].gameObject);
                else
                    Object.Destroy(doomed[i].gameObject);
            }
        }
    }
}
