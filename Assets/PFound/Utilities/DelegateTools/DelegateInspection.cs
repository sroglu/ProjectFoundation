using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PFound.Utilities.DelegateTools
{
    /// <summary>
    /// Classifies a <see cref="Delegate"/> by whether its bound receiver (<see cref="Delegate.Target"/>)
    /// is a <see cref="UnityEngine.Object"/>, and — crucially — whether that receiver is still alive.
    /// Unity overloads <c>==</c> so a destroyed object compares equal to <c>null</c> ("fake null") even
    /// though the managed reference is non-null; these helpers respect that overload so callers can
    /// safely drop callbacks whose owner has been destroyed.
    /// </summary>
    public static class DelegateInspection
    {
        /// <summary>True when <paramref name="callback"/> is a non-static delegate whose receiver is a
        /// <see cref="UnityEngine.Object"/> (regardless of whether it is still alive).</summary>
        public static bool TargetsUnityObject(Delegate callback)
        {
            return callback != null && callback.Target is Object;
        }

        /// <summary>True when the delegate targets a Unity object that has not been destroyed. Uses the
        /// engine's overloaded equality so a destroyed ("fake null") object returns false.</summary>
        public static bool TargetsLiveUnityObject(Delegate callback)
        {
            return callback?.Target is Object owner && owner != null;
        }

        /// <summary>True when the delegate targets a Unity object that <em>has</em> been destroyed — the
        /// managed reference is non-null but the engine reports it as fake-null.</summary>
        public static bool TargetsDestroyedUnityObject(Delegate callback)
        {
            return callback?.Target is Object owner && owner == null;
        }
    }
}
