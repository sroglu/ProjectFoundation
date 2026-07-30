#if UNITY_EDITOR
using PFound.Utilities.EditorHelpers;
using UnityEditor;
using UnityEngine;

namespace PFound.LocalizationService.Editor
{
    /// <summary>
    /// Inspector for <see cref="GoogleSheetsConfig"/>. Shows only the fields
    /// relevant to the selected <c>SourceType</c> (LocalCsv vs. GoogleSheets)
    /// and ends with the Import button rendered by <see cref="ImportableAssetEditor"/>.
    /// </summary>
    [CustomEditor(typeof(GoogleSheetsConfig))]
    public class GoogleSheetsConfigEditor : ImportableAssetEditor
    {
        SerializedProperty _sourceType;
        SerializedProperty _translationsCsv;
        SerializedProperty _parametersCsv;
        SerializedProperty _googleSheetsUrl;
        SerializedProperty _parametersSheetName;
        SerializedProperty _outputDirectory;

        void OnEnable()
        {
            _sourceType = serializedObject.FindProperty("SourceType");
            _translationsCsv = serializedObject.FindProperty("TranslationsCsv");
            _parametersCsv = serializedObject.FindProperty("ParametersCsv");
            _googleSheetsUrl = serializedObject.FindProperty("GoogleSheetsUrl");
            _parametersSheetName = serializedObject.FindProperty("ParametersSheetName");
            _outputDirectory = serializedObject.FindProperty("OutputDirectory");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_sourceType, new GUIContent("Source Type"));
            EditorGUILayout.Space(8);

            var sourceType = (LocalizationSourceType)_sourceType.enumValueIndex;
            switch (sourceType)
            {
                case LocalizationSourceType.LocalCsv:
                    EditorGUILayout.PropertyField(_translationsCsv, new GUIContent("Translations CSV"));
                    EditorGUILayout.PropertyField(_parametersCsv, new GUIContent("Parameters CSV (optional)"));
                    break;

                case LocalizationSourceType.GoogleSheets:
                    EditorGUILayout.PropertyField(_googleSheetsUrl, new GUIContent("Google Sheets URL"));
                    EditorGUILayout.PropertyField(_parametersSheetName, new GUIContent("Parameters Sheet Name (optional)"));
                    EditorGUILayout.LabelField(" ", "Must exactly match an existing tab name. Google Sheets silently falls back to the first tab on typos — leave empty if unsure.", EditorStyles.miniLabel);
                    EditorGUILayout.HelpBox(
                        "Sheet must be shared: Share > Anyone with the link.\n" +
                        "Translations are fetched from the FIRST tab — place your consolidation tab there.",
                        MessageType.None);
                    break;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(_outputDirectory, new GUIContent("Output Directory"));

            serializedObject.ApplyModifiedProperties();

            DrawImportButton(target as ImportableAsset);
        }
    }
}
#endif
