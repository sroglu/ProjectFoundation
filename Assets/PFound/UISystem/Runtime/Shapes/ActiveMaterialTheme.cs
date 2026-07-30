using System;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Static holder for the currently-active <see cref="ThemeMaterialRegistry"/>.
    /// <see cref="ThemedSdfPanel"/> instances subscribe to <see cref="OnChanged"/> and rebind
    /// their materials when the active registry is swapped. Setting <see cref="Active"/> to the
    /// same value is a no-op; setting to <c>null</c> clears every themed panel's material to null.
    /// </summary>
    /// <remarks>
    /// <para>Runtime-only state (not persisted). Consumers that want theme persistence should
    /// pair this with <c>UserPrefs</c> at the bootstrap layer.</para>
    /// <para>Single-threaded — Unity main-thread only. No locking.</para>
    /// </remarks>
    public static class ActiveMaterialTheme
    {
        private static ThemeMaterialRegistry _active;

        /// <summary>Raised AFTER <see cref="Active"/> is assigned to a new value.</summary>
        public static event Action<ThemeMaterialRegistry> OnChanged;

        /// <summary>The currently-active theme registry. Assign to swap themes globally.</summary>
        public static ThemeMaterialRegistry Active
        {
            get => _active;
            set
            {
                if (_active == value) return;
                _active = value;
                OnChanged?.Invoke(_active);
            }
        }

        /// <summary>
        /// Convenience accessor — equivalent to <c>Active?.Get(key)</c>.
        /// Returns <c>null</c> when no theme is active or the key is unmapped.
        /// </summary>
        public static Material Get(string key)
        {
            return _active != null ? _active.Get(key) : null;
        }
    }
}
