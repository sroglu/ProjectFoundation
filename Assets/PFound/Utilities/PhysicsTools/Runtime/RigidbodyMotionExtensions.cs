using UnityEngine;

namespace PFound.Utilities.PhysicsTools
{
    /// <summary>
    /// Motion queries and resets for <see cref="Rigidbody"/>. Speed helpers decompose the
    /// linear velocity relative to the body's own orientation and the world horizontal plane,
    /// which is what movement / animation code usually needs rather than the raw vector.
    /// </summary>
    public static class RigidbodyMotionExtensions
    {
        /// <summary>Zeroes both linear and angular velocity, bringing the body to a dead stop.</summary>
        public static void ResetMotion(this Rigidbody body)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// The body's angular velocity expressed in its own local frame (world angular
        /// velocity rotated into local space), useful for reading spin about a local axis.
        /// </summary>
        public static Vector3 LocalAngularVelocity(this Rigidbody body)
        {
            return body.transform.InverseTransformDirection(body.angularVelocity);
        }

        /// <summary>Speed across the world horizontal (XZ) plane, ignoring vertical motion.</summary>
        public static float HorizontalSpeed(this Rigidbody body)
        {
            Vector3 v = body.linearVelocity;
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>
        /// Signed speed along the body's forward axis: positive when moving forward, negative
        /// when reversing. Includes any vertical component of forward motion.
        /// </summary>
        public static float ForwardSpeed(this Rigidbody body)
        {
            return Vector3.Dot(body.linearVelocity, body.transform.forward);
        }

        /// <summary>
        /// Signed forward speed measured purely on the world horizontal plane: both the
        /// velocity and the body's facing are flattened onto XZ before projecting. This is the
        /// "ground speed in the direction I'm facing" value, unaffected by pitch or falling.
        /// </summary>
        public static float HorizontalForwardSpeed(this Rigidbody body)
        {
            Vector3 v = body.linearVelocity;
            v.y = 0f;

            Vector3 facing = body.transform.forward;
            facing.y = 0f;

            if (facing.sqrMagnitude < 1e-8f)
                return 0f;

            return Vector3.Dot(v, facing.normalized);
        }
    }
}
