using System.IO;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    /// <summary>
    /// Adds UISystem component creation shortcuts to the Unity Project context menu.
    /// Assets > Create > UISystem > Button (Filled / Outlined)
    /// Assets > Create > UISystem > Card (Elevated / Filled / Outlined)
    /// Assets > UISystem > Open UISystem Editor
    /// </summary>
    internal static class UISystemMenuItems
    {
        [MenuItem("Assets/UISystem/Open UISystem Editor", priority = 290)]
        public static void OpenUISystemEditor()
            => UISystemEditorWindow.ShowWindow();

        private const string StylePathButton     = "../../Styles/Components/button.uss";
        private const string StylePathCard       = "../../Styles/Components/card.uss";
        private const string StylePathStateLayer = "../../Styles/state-layer.uss";

        // ------------------------------------------------------------------ //
        //  Filled Button                                                        //
        // ------------------------------------------------------------------ //

        [MenuItem("Assets/Create/UISystem/Button (Filled)", priority = 200)]
        private static void CreateFilledButton()
            => CreateButtonUxml("NewFilledButton.uxml", "Filled");

        [MenuItem("Assets/Create/UISystem/Button (Filled)", validate = true)]
        private static bool ValidateCreateFilledButton()
            => GetSelectedFolder() != null;

        // ------------------------------------------------------------------ //
        //  Outlined Button                                                      //
        // ------------------------------------------------------------------ //

        [MenuItem("Assets/Create/UISystem/Button (Outlined)", priority = 201)]
        private static void CreateOutlinedButton()
            => CreateButtonUxml("NewOutlinedButton.uxml", "Outlined");

        [MenuItem("Assets/Create/UISystem/Button (Outlined)", validate = true)]
        private static bool ValidateCreateOutlinedButton()
            => GetSelectedFolder() != null;

        // ------------------------------------------------------------------ //
        //  Elevated Card                                                        //
        // ------------------------------------------------------------------ //

        [MenuItem("Assets/Create/UISystem/Card (Elevated)", priority = 210)]
        private static void CreateElevatedCard()
            => CreateCardUxml("NewElevatedCard.uxml", "Elevated");

        [MenuItem("Assets/Create/UISystem/Card (Elevated)", validate = true)]
        private static bool ValidateCreateElevatedCard()
            => GetSelectedFolder() != null;

        // ------------------------------------------------------------------ //
        //  Filled Card                                                          //
        // ------------------------------------------------------------------ //

        [MenuItem("Assets/Create/UISystem/Card (Filled)", priority = 211)]
        private static void CreateFilledCard()
            => CreateCardUxml("NewFilledCard.uxml", "Filled");

        [MenuItem("Assets/Create/UISystem/Card (Filled)", validate = true)]
        private static bool ValidateCreateFilledCard()
            => GetSelectedFolder() != null;

        // ------------------------------------------------------------------ //
        //  Outlined Card                                                        //
        // ------------------------------------------------------------------ //

        [MenuItem("Assets/Create/UISystem/Card (Outlined)", priority = 212)]
        private static void CreateOutlinedCard()
            => CreateCardUxml("NewOutlinedCard.uxml", "Outlined");

        [MenuItem("Assets/Create/UISystem/Card (Outlined)", validate = true)]
        private static bool ValidateCreateOutlinedCard()
            => GetSelectedFolder() != null;

        // ------------------------------------------------------------------ //
        //  Helpers                                                              //
        // ------------------------------------------------------------------ //

        private static void CreateCardUxml(string fileName, string variant)
        {
            string folder = GetSelectedFolder() ?? "Assets";
            string path   = Path.Combine(folder, fileName);
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            string content =
$@"<ui:UXML xmlns:ui=""UnityEngine.UIElements""
         xmlns:components=""PFound.UISystem.Components"">
    <ui:Style src=""{StylePathCard}"" />
    <ui:Style src=""{StylePathStateLayer}"" />
    <components:M3Card variant=""{variant}"">
        <ui:VisualElement class=""m3-card__header"">
            <ui:Label text=""Card Title"" class=""m3-card__headline m3-title"" />
            <ui:Label text=""Subhead"" class=""m3-card__subhead m3-body"" />
        </ui:VisualElement>
        <ui:VisualElement class=""m3-card__content"">
            <ui:Label text=""Supporting text."" class=""m3-card__supporting-text m3-body"" />
        </ui:VisualElement>
    </components:M3Card>
</ui:UXML>
";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[UISystem] Created {variant} card at {path}");
        }

        private static void CreateButtonUxml(string fileName, string variant)
        {
            string folder = GetSelectedFolder() ?? "Assets";
            string path   = Path.Combine(folder, fileName);

            // Avoid overwriting an existing file
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            string content =
$@"<ui:UXML xmlns:ui=""UnityEngine.UIElements""
         xmlns:components=""PFound.UISystem.Components"">
    <ui:Style src=""{StylePathButton}"" />
    <ui:Style src=""{StylePathStateLayer}"" />
    <components:M3Button variant=""{variant}"" text=""Button"" />
</ui:UXML>
";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            // Ping the newly created asset in the Project window
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[UISystem] Created {variant} button at {path}");
        }

        /// <summary>
        /// Returns the selected folder path in the Project window,
        /// or null if nothing is selected.
        /// </summary>
        private static string GetSelectedFolder()
        {
            if (Selection.activeObject == null) return "Assets";

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) return "Assets";

            return Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        }
    }
}
