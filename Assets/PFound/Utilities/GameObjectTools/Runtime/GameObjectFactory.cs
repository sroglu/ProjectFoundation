using System;
using UnityEngine;

namespace PFound.Utilities.GameObjectTools
{
    /// <summary>
    /// Helpers for spawning GameObjects: fetching-or-creating named children,
    /// building objects with a fixed component set, and wrapping Unity's
    /// primitive creation so a parent can be supplied inline.
    /// </summary>
    public static class GameObjectFactory
    {
        /// <summary>
        /// Returns the existing direct child with <paramref name="childName"/>, or
        /// creates a fresh empty child under <paramref name="parent"/> when none exists.
        /// The lookup only scans the immediate children, never the deeper hierarchy.
        /// </summary>
        public static GameObject GetOrCreateChild(this Transform parent, string childName)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (string.IsNullOrEmpty(childName)) throw new ArgumentException("Child name must be provided.", nameof(childName));

            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child.gameObject;
            }

            var created = new GameObject(childName);
            created.transform.SetParent(parent, false);
            return created;
        }

        /// <summary>
        /// Convenience overload that accepts and returns the parent GameObject.
        /// </summary>
        public static GameObject GetOrCreateChild(this GameObject parent, string childName)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return parent.transform.GetOrCreateChild(childName);
        }

        /// <summary>
        /// Creates a detached GameObject with <paramref name="name"/> and attaches one
        /// component of each type in <paramref name="componentTypes"/>. Types must derive
        /// from <see cref="Component"/>.
        /// </summary>
        public static GameObject CreateWithComponents(string name, params Type[] componentTypes)
        {
            GameObject go = componentTypes == null || componentTypes.Length == 0
                ? new GameObject(name)
                : new GameObject(name, componentTypes);
            return go;
        }

        /// <summary>
        /// Creates a GameObject with the given components already parented (and its
        /// local transform kept identity) under <paramref name="parent"/>.
        /// </summary>
        public static GameObject CreateWithComponents(Transform parent, string name, params Type[] componentTypes)
        {
            GameObject go = CreateWithComponents(name, componentTypes);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Creates a Unity primitive of <paramref name="primitive"/> and, when supplied,
        /// parents it under <paramref name="parent"/> preserving local identity transform.
        /// </summary>
        public static GameObject CreatePrimitive(PrimitiveType primitive, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }
    }
}
