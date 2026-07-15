using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.EditorHelpers
{
    /// <summary>
    /// Declares game-specific ScriptableObject assets a module needs to exist at the project level.
    /// <see cref="GameSpecificAssetGuard"/> discovers implementers by reflection and auto-creates
    /// any missing assets.
    /// </summary>
    public interface IGameSpecificAssetProvider
    {
        IEnumerable<GameSpecificAssetRegistration> GetRegistrations();
    }

    /// <summary>One required asset: a ScriptableObject type and the project path it must live at.</summary>
    public readonly struct GameSpecificAssetRegistration
    {
        public readonly Type AssetType;
        public readonly string AssetPath;

        /// <summary>Optional Resources-loadable name (for assets that must also resolve via Resources.Load).</summary>
        public readonly string ResourcesName;

        /// <summary>Optional post-create configure step, run on a freshly created asset instance.</summary>
        public readonly Action<ScriptableObject> Factory;

        public GameSpecificAssetRegistration(Type assetType, string assetPath, string resourcesName = null, Action<ScriptableObject> factory = null)
        {
            AssetType = assetType;
            AssetPath = assetPath;
            ResourcesName = resourcesName;
            Factory = factory;
        }

        /// <summary>Registration for ScriptableObject <typeparamref name="T"/> at <paramref name="assetPath"/>, with an optional Resources name and post-create configure step.</summary>
        public static GameSpecificAssetRegistration For<T>(string assetPath, string resourcesName = null, Action<T> factory = null) where T : ScriptableObject
        {
            Action<ScriptableObject> boxed = factory == null ? null : so => factory((T)so);
            return new GameSpecificAssetRegistration(typeof(T), assetPath, resourcesName, boxed);
        }
    }
}
