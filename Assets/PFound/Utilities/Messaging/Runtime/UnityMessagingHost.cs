#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// Installs the Unity-aware seams into <see cref="MessagingEnvironment"/> so the
    /// engine-free dispatch core gains fake-null liveness detection and player-log fault
    /// reporting when hosted inside Unity. Runs automatically before the first scene
    /// loads; also runs in edit mode via the static constructor path.
    /// </summary>
    public static class UnityMessagingHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            MessagingEnvironment.LivenessOf = IsAlive;
            MessagingEnvironment.ReportFault = ReportFault;
        }

        private static bool IsAlive(object guard)
        {
            if (guard == null)
            {
                return false;
            }

            // A UnityEngine.Object survivor compares non-null even through the fake-null
            // overload; a destroyed one reports null here and is treated as dead.
            UnityEngine.Object unityObject = guard as UnityEngine.Object;
            if (!ReferenceEquals(unityObject, null))
            {
                return unityObject != null;
            }

            // Plain managed guards are alive as long as the reference exists.
            return true;
        }

        private static void ReportFault(Exception error)
        {
            Debug.LogException(error);
        }
    }
}
#endif
