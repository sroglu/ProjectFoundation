using UnityEngine;

namespace PFound.Utilities.PhysicsTools
{
    /// <summary>
    /// Raycast helpers that answer a common gameplay question the built-in API does not: cast
    /// a ray but disregard hits belonging to one specific object (for example the caster's own
    /// body). Implemented by gathering all hits and returning the nearest one that is not part
    /// of the excluded hierarchy.
    /// </summary>
    public static class PhysicsQueries
    {
        /// <summary>
        /// Casts a ray and returns the closest hit whose collider is <b>not</b> part of
        /// <paramref name="excluded"/> (that object or any of its descendants). Colliders on the
        /// excluded hierarchy are skipped entirely, so a ray fired from inside an object passes
        /// through its own colliders to whatever lies beyond.
        /// </summary>
        public static bool RaycastExcluding(
            Vector3 origin,
            Vector3 direction,
            GameObject excluded,
            out RaycastHit hit,
            float maxDistance = Mathf.Infinity,
            int layerMask = Physics.DefaultRaycastLayers,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            RaycastHit[] candidates = Physics.RaycastAll(origin, direction, maxDistance, layerMask, triggerInteraction);

            bool found = false;
            hit = default;
            float nearest = Mathf.Infinity;
            Transform excludedRoot = excluded != null ? excluded.transform : null;

            for (int i = 0; i < candidates.Length; i++)
            {
                RaycastHit candidate = candidates[i];
                if (excludedRoot != null && candidate.collider.transform.IsChildOf(excludedRoot))
                    continue;

                if (candidate.distance < nearest)
                {
                    nearest = candidate.distance;
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Ray-based overload of <see cref="RaycastExcluding(Vector3,Vector3,GameObject,out RaycastHit,float,int,QueryTriggerInteraction)"/>.</summary>
        public static bool RaycastExcluding(
            Ray ray,
            GameObject excluded,
            out RaycastHit hit,
            float maxDistance = Mathf.Infinity,
            int layerMask = Physics.DefaultRaycastLayers,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return RaycastExcluding(ray.origin, ray.direction, excluded, out hit, maxDistance, layerMask, triggerInteraction);
        }
    }
}
