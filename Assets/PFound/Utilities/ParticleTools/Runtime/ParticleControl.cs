using UnityEngine;

namespace PFound.Utilities.ParticleTools
{
    /// <summary>
    /// Convenience wrappers for driving <see cref="ParticleSystem"/> instances: repositioning
    /// a system before playing it, and batch play / stop / toggle over arrays. All helpers
    /// tolerate null entries so callers can pass partially-populated arrays without guarding.
    /// </summary>
    public static class ParticleControl
    {
        /// <summary>
        /// Moves <paramref name="system"/> to <paramref name="position"/>, restarts it from a
        /// clean state and plays it. Returns the same system for chaining.
        /// </summary>
        public static ParticleSystem PlaceAndPlay(this ParticleSystem system, Vector3 position)
        {
            return PlaceAndPlay(system, position, Quaternion.identity);
        }

        /// <summary>
        /// Moves and orients <paramref name="system"/>, restarts it from a clean state and
        /// plays it. Any already-alive particles are cleared first so the burst reads as fresh.
        /// </summary>
        public static ParticleSystem PlaceAndPlay(this ParticleSystem system, Vector3 position, Quaternion rotation)
        {
            if (system == null)
                return null;

            Transform transform = system.transform;
            transform.SetPositionAndRotation(position, rotation);

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Clear(true);
            system.Play(true);
            return system;
        }

        /// <summary>Plays every non-null system in <paramref name="systems"/>, children included.</summary>
        public static void PlayAll(this ParticleSystem[] systems)
        {
            if (systems == null)
                return;

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Play(true);
            }
        }

        /// <summary>
        /// Stops every non-null system in <paramref name="systems"/>. When
        /// <paramref name="clear"/> is true, alive particles are removed immediately;
        /// otherwise emission stops and existing particles finish their life.
        /// </summary>
        public static void StopAll(this ParticleSystem[] systems, bool clear = false)
        {
            if (systems == null)
                return;

            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Stop(true, behavior);
            }
        }

        /// <summary>
        /// Plays the array when <paramref name="playing"/> is true, otherwise stops it —
        /// a single switch for binding particle activity to a boolean state.
        /// </summary>
        public static void SetPlaying(this ParticleSystem[] systems, bool playing)
        {
            if (playing)
                PlayAll(systems);
            else
                StopAll(systems);
        }
    }
}
