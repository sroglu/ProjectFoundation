using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PFound.ScreenRouter;

namespace PFound.SampleGame.Editor
{
    /// <summary>
    /// Builds the <see cref="SampleScreen"/> prefab and registers a <c>ScreenDefinition</c> for it in
    /// the sample <c>ScreenRouterConfig</c>, so the demo scene's <c>ScreenRouterHost</c> can actually
    /// <c>SwitchScreen&lt;SampleScreen&gt;</c> on Play. Editor-only, idempotent — safe to re-run.
    /// </summary>
    internal static class SampleSceneBuilder
    {
        private const string PrefabPath = "Assets/SampleGame/SampleScreen.prefab";
        private const string ConfigPath = "Assets/GameSpecific/ScreenRouter/ScreenRouterConfig.asset";

        [MenuItem("Tools/PFound/Sample Game/Build SampleScreen + Register")]
        public static void Build()
        {
            var prefab = BuildScreenPrefab();
            RegisterInConfig(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SampleGame] Built {PrefabPath} and registered SampleScreen in the ScreenRouterConfig.");
        }

        private static GameObject BuildScreenPrefab()
        {
            var root = new GameObject("SampleScreen",
                typeof(RectTransform), typeof(CanvasGroup),
                typeof(CanvasGroupContentRenderer), typeof(SampleScreen));
            Stretch((RectTransform)root.transform);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.22f, 1f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RegisterInConfig(GameObject prefab)
        {
            var config = AssetDatabase.LoadAssetAtPath<ScreenRouterConfig>(ConfigPath);

            // Definitions serialize INLINE inside the single config asset (no per-screen sub-asset):
            // build the ScreenDefinition and assign it into the config's `_screens` list directly.
            var definition = ScreenDefinition.CreateRuntime(typeof(SampleScreen), prefab, PoolingType.Ephemeral);
            SampleAssetSetup.SetField(config, "_screens", new List<ScreenDefinition> { definition });
            config.InvalidateCaches();
            EditorUtility.SetDirty(config);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
