using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Editor.Themes.Papercut
{
    /// <summary>
    /// Editor window that loads <c>PapercutShowcase.uxml</c> for visual review.
    /// Acts as the host for the Showcase tree without requiring a runtime Scene.
    /// The Showcase is statically posed (no reactive state) — every Pressed /
    /// Disabled / Inactive cell is encoded by USS classes in the UXML directly,
    /// so no controller wiring is needed at runtime.
    /// </summary>
    public sealed class PapercutShowcaseWindow : EditorWindow
    {
        private const string ShowcaseUxmlPath =
            "Assets/PFound/UISystem/UXML/Papercut/PapercutShowcase.uxml";

        [MenuItem("Tools/Papercut/Open Showcase")]
        public static void Open()
        {
            var win = GetWindow<PapercutShowcaseWindow>(false, "Papercut Showcase", true);
            win.minSize = new Vector2(900, 1100);
            win.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShowcaseUxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label($"Showcase UXML not found at {ShowcaseUxmlPath}"));
                return;
            }
            tree.CloneTree(rootVisualElement);
        }
    }
}
