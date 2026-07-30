using PFound.UISystem.Data;
using PFound.UISystem.Enums;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    /// <summary>
    /// Custom inspector for ThemeData ScriptableObjects.
    /// Draws the default Odin inspector plus a visual color-swatch preview of the
    /// full 27-role color palette in a 3-column grid, organized into semantic groups.
    /// </summary>
    [CustomEditor(typeof(ThemeData))]
    public class ThemeDataEditor : OdinEditor
    {
        private bool _showPalettePreview = true;

        private static readonly ColorRole[] s_Roles = (ColorRole[])System.Enum.GetValues(typeof(ColorRole));

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(8f);
            _showPalettePreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPalettePreview, "Color Palette Preview");
            if (_showPalettePreview)
            {
                DrawColorSwatches();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorSwatches()
        {
            var themeData = (ThemeData)target;

            const int columns   = 3;
            const float swatchH = 50f;
            const float labelH  = 18f;
            const float padding = 4f;

            float totalWidth = EditorGUIUtility.currentViewWidth - 24f;
            float cellWidth  = (totalWidth - padding * (columns - 1)) / columns;

            int col = 0;
            Rect rowStart = GUILayoutUtility.GetRect(totalWidth, swatchH + labelH + padding);
            rowStart.width = cellWidth;

            foreach (var role in s_Roles)
            {
                Color c = themeData.GetColor(role);

                // Swatch rect
                Rect swatchRect = new Rect(rowStart.x, rowStart.y, cellWidth, swatchH);
                EditorGUI.DrawRect(swatchRect, c);

                // Border
                DrawRectBorder(swatchRect, new Color(0f, 0f, 0f, 0.2f));

                // Label rect
                Rect labelRect = new Rect(rowStart.x, rowStart.y + swatchH, cellWidth, labelH);
                EditorGUI.LabelField(labelRect, role.ToString(), EditorStyles.centeredGreyMiniLabel);

                col++;
                if (col < columns)
                {
                    rowStart.x += cellWidth + padding;
                }
                else
                {
                    col = 0;
                    float nextY = rowStart.y + swatchH + labelH + padding;
                    GUILayoutUtility.GetRect(totalWidth, swatchH + labelH + padding);
                    rowStart = new Rect(
                        EditorGUIUtility.currentViewWidth - totalWidth - 12f,
                        nextY,
                        cellWidth,
                        swatchH + labelH);
                }
            }
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin,   rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax-1, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.xMin,   rect.yMin, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax-1, rect.yMin, 1f, rect.height), color);
        }
    }
}
