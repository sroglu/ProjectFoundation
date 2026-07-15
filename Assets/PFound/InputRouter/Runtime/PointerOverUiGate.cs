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
        ///
        /// Touch-aware: the default <c>IsPointerOverGameObject()</c> overload queries the mouse
        /// pointer id (-1), which never resolves on a touch device — so on phones/tablets a UI hit
        /// would go unnoticed and gameplay intents would fire straight through the UI. We therefore
        /// OR the mouse pointer with the primary touch pointer (id 0), matching the desktop and
        /// mobile behaviour with a single predicate.
        /// </summary>
        public static Func<bool> FromEventSystem()
        {
            return () =>
            {
                EventSystem current = EventSystem.current;
                return current != null &&
                       (current.IsPointerOverGameObject() || current.IsPointerOverGameObject(0));
            };
        }
    }
}
