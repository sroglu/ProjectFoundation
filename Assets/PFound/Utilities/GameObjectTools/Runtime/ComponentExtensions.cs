using UnityEngine;

namespace PFound.Utilities.GameObjectTools
{
    /// <summary>
    /// Small predicates over components: presence checks and a null-tolerant
    /// enabled-state query for <see cref="Behaviour"/>.
    /// </summary>
    public static class ComponentExtensions
    {
        /// <summary>True when <paramref name="gameObject"/> carries a component of type <typeparamref name="T"/>.</summary>
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject != null && gameObject.TryGetComponent<T>(out _);
        }

        /// <summary>True when the object owning <paramref name="component"/> carries a component of type <typeparamref name="T"/>.</summary>
        public static bool HasComponent<T>(this Component component) where T : Component
        {
            return component != null && component.TryGetComponent<T>(out _);
        }

        /// <summary>
        /// Returns whether <paramref name="behaviour"/> is enabled, treating a null or
        /// destroyed reference as "not enabled" rather than throwing. Note Unity overloads
        /// <c>==</c> so a destroyed object compares equal to null here.
        /// </summary>
        public static bool IsBehaviourEnabled(this Behaviour behaviour)
        {
            return behaviour != null && behaviour.enabled;
        }
    }
}
