using PFound.UISystem.Editor.ColorGeneration;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    /// <summary>
    /// Editor window for generating M3 Dynamic Color theme assets from a seed color.
    /// Open via: Assets > UISystem > Generate Theme from Seed Color
    /// </summary>
    public sealed class DynamicColorGeneratorWindow : EditorWindow
    {
        private Color  _seedColor   = new Color(0.404f, 0.314f, 0.643f); // #6750A4 M3 baseline
        private string _outputPath  = "Assets/GameSpecific/UISystem/Themes/";
        private string _baseName    = "Generated";

        [MenuItem("Assets/UISystem/Generate Theme from Seed Color", priority = 300)]
        public static void ShowWindow()
        {
            var window = GetWindow<DynamicColorGeneratorWindow>("M3 Dynamic Color");
            window.minSize = new Vector2(360f, 240f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("M3 Dynamic Color Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Generates a light + dark ThemeData pair from a single seed color.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(12f);

            _seedColor  = EditorGUILayout.ColorField("Seed Color", _seedColor);
            _outputPath = EditorGUILayout.TextField("Output Folder", _outputPath);
            _baseName   = EditorGUILayout.TextField("Asset Name", _baseName);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                $"Will create:\n  {_outputPath}{_baseName}Light.asset\n  {_outputPath}{_baseName}Dark.asset",
                MessageType.Info);

            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Generate Theme Assets", GUILayout.Height(32f)))
            {
                string folder = _outputPath.TrimEnd('/') + "/";
                DynamicColorGenerator.GenerateFromSeed(_seedColor, folder, _baseName);
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Use M3 Baseline Purple (#6750A4)"))
                _seedColor = new Color(0.404f, 0.314f, 0.643f);
        }
    }
}
