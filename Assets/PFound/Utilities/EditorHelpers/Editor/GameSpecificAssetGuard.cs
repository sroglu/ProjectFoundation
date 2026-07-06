using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.Utilities.EditorHelpers
{
    /// <summary>
    /// On load, discovers every <see cref="IGameSpecificAssetProvider"/> via reflection and creates
    /// any registered ScriptableObject asset that is missing at its declared path. Editor-only
    /// automation so a module's required project assets exist without manual setup.
    /// </summary>
    [InitializeOnLoad]
    public static class GameSpecificAssetGuard
    {
        static GameSpecificAssetGuard() => EditorApplication.delayCall += EnsureRegisteredAssets;

        [MenuItem("Tools/PFound/Ensure GameSpecific Assets")]
        public static void EnsureRegisteredAssets()
        {
            bool created = false;
            foreach (var providerType in TypeCache.GetTypesDerivedFrom<IGameSpecificAssetProvider>())
            {
                if (providerType.IsAbstract || providerType.IsInterface) continue;
                if (providerType.GetConstructor(Type.EmptyTypes) == null) continue;

                var provider = (IGameSpecificAssetProvider)Activator.CreateInstance(providerType);
                foreach (var registration in provider.GetRegistrations())
                    created |= EnsureAsset(registration);
            }
            if (created) AssetDatabase.SaveAssets();
        }

        private static bool EnsureAsset(GameSpecificAssetRegistration registration)
        {
            if (registration.AssetType == null || string.IsNullOrEmpty(registration.AssetPath)) return false;
            if (AssetDatabase.LoadMainAssetAtPath(registration.AssetPath) != null) return false;

            var dir = Path.GetDirectoryName(registration.AssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var asset = ScriptableObject.CreateInstance(registration.AssetType);
            AssetDatabase.CreateAsset(asset, registration.AssetPath);
            Debug.Log($"[PFound] Created missing GameSpecific asset: {registration.AssetPath}");
            return true;
        }
    }
}
