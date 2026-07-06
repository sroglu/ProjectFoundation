using System;
using UnityEngine.EventSystems;

namespace PFound.InputRouter
{
    /// <summary>
    /// Convenience factory for the pointer-over-UI predicate the core router asks for. The core
    /// stays free of any EventSystem dependency; this engine-facing helper wraps
    /// <see cref="EventSystem"/> so callers can inject a ready-made gate without writing the
    /// null-safe boilerplate themselves.
    /// </summary>
    public static class PointerOverUiGate
    {
        /// <summary>
        /// A predicate that is true while the current <see cref="EventSystem"/> reports the
        /// pointer over a UI element. Returns false when no EventSystem is present.
        /// </summary>
        public static Func<bool> FromEventSystem()
        {
            return () =>
            {
                EventSystem current = EventSystem.current;
                return current != null && current.IsPointerOverGameObject();
            };
        }
    }
}
