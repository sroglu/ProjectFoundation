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
            var definition = ScreenDefinition.CreateRuntime(typeof(SampleScreen), prefab, PoolingType.Ephemeral);
            definition.name = "SampleScreen (ScreenDefinition)";
            AssetDatabase.AddObjectToAsset(definition, config);

            var serialized = new SerializedObject(config);
            var screens = serialized.FindProperty("_screens");
            screens.arraySize = 1;
            screens.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
