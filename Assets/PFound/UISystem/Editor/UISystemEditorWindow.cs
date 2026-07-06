using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor
{
    /// <summary>
    /// Quick visual check tool showing all M3 components in a scrollable grid
    /// with theme toggle. Open via: Assets > UISystem > Open UISystem Editor
    /// </summary>
    public sealed class UISystemEditorWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool    _darkTheme;

        // M3 baseline color pairs [bg, fg] for light and dark themes
        private static readonly Color[] s_LightBg = { Hex("#6750A4"), Hex("#625B71"), Hex("#7D5260"), Hex("#B3261E"), Hex("#FFFBFE") };
        private static readonly Color[] s_DarkBg  = { Hex("#D0BCFF"), Hex("#CCC2DC"), Hex("#EFB8C8"), Hex("#F2B8B5"), Hex("#1C1B1F") };

        private static readonly string[] s_ComponentNames =
        {
            // Actions
            "M3Button (Filled)",
            "M3Button (Outlined)",
            "M3Button (Text)",
            "M3Button (Elevated)",
            "M3Button (Tonal)",
            "M3FAB (Regular)",
            "M3FAB (Small)",
            "M3FAB (Large)",
            "M3FAB (Extended)",
            "M3SegmentedButton",
            // Selection
            "M3Checkbox",
            "M3RadioButton",
            "M3Toggle",
            "M3Chip (Assist)",
            "M3Chip (Filter)",
            "M3Chip (Input)",
            "M3Slider",
            // Input
            "M3TextField (Filled)",
            "M3TextField (Outlined)",
            "M3SearchBar",
            "M3DatePicker",
            "M3TimePicker",
            // Containment
            "M3Card (Elevated)",
            "M3Card (Filled)",
            "M3Card (Outlined)",
            "M3Dialog",
            "M3BottomSheet",
            "M3Menu",
            "M3Tooltip",
            "M3List",
            "M3Divider",
            // Communication
            "M3Snackbar",
            "M3Badge",
            "M3ProgressIndicator",
            // Navigation
            "M3NavigationBar",
            "M3NavigationDrawer",
            "M3NavigationRail",
            "M3TopAppBar",
            "M3BottomAppBar",
            "M3Tabs",
        };

        public static void ShowWindow()
        {
            var window = GetWindow<UISystemEditorWindow>("UISystem Editor");
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawComponentGrid();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("M3 Components", EditorStyles.boldLabel, GUILayout.Width(160f));
            GUILayout.FlexibleSpace();
            bool newDark = GUILayout.Toggle(_darkTheme, _darkTheme ? "Dark Theme" : "Light Theme", EditorStyles.toolbarButton, GUILayout.Width(100f));
            if (newDark != _darkTheme)
            {
                _darkTheme = newDark;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComponentGrid()
        {
            Color[] palette = _darkTheme ? s_DarkBg : s_LightBg;
            Color surface   = _darkTheme ? Hex("#1C1B1F") : Hex("#FFFBFE");
            Color onSurface = _darkTheme ? Hex("#E6E1E5") : Hex("#1C1B1F");

            const int cols     = 3;
            const float cellH  = 72f;
            const float pad    = 8f;
            float viewW = EditorGUIUtility.currentViewWidth - 16f;
            float cellW = (viewW - pad * (cols + 1)) / cols;

            int idx = 0;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(pad);

            foreach (string name in s_ComponentNames)
            {
                Rect cell = GUILayoutUtility.GetRect(cellW, cellH);

                // Card bg
                EditorGUI.DrawRect(cell, surface);
                DrawBorder(cell, onSurface * new Color(1,1,1,0.12f));

                // Color swatch strip
                Color accent = palette[idx % palette.Length];
                EditorGUI.DrawRect(new Rect(cell.x, cell.y, 4f, cell.height), accent);

                // Component name
                Rect nameRect = new Rect(cell.x + 10f, cell.y + 8f, cell.width - 14f, 18f);
                EditorGUI.LabelField(nameRect, name, new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = onSurface } });

                // Placeholder "preview" block
                Rect previewRect = new Rect(cell.x + 10f, cell.y + 30f, cell.width - 14f, 24f);
                EditorGUI.DrawRect(previewRect, accent * new Color(1,1,1,0.15f));
                EditorGUI.LabelField(previewRect,
                    "Open scene to preview",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = onSurface * new Color(1,1,1,0.5f) } });

                idx++;

                if (idx % cols == 0)
                {
                    GUILayout.Space(pad);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(pad);
                }
                else
                {
                    GUILayout.Space(pad);
                }
            }

            GUILayout.Space(pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(pad);
        }

        private static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - 1, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.yMin, 1f, r.height), c);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
