using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PFound.UISystem.Editor.PageBuilder
{
    /// <summary>
    /// IMGUI helper that draws the M3 component palette with category foldouts.
    /// </summary>
    internal sealed class ComponentPalette
    {
        internal event Action<ComponentRegistry.ComponentInfo> OnComponentSelected;

        private readonly Dictionary<string, bool> _foldoutStates = new();
        private Vector2 _scrollPosition;

        internal ComponentPalette()
        {
            foreach (string category in ComponentRegistry.Categories)
                _foldoutStates[category] = true;
        }

        internal void OnGUI()
        {
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Add Container button at the top
            if (GUILayout.Button("+ Container", EditorStyles.miniButton))
                OnComponentSelected?.Invoke(ContainerInfo);

            EditorGUILayout.Space(4f);

            foreach (string category in ComponentRegistry.Categories)
            {
                _foldoutStates[category] = EditorGUILayout.Foldout(
                    _foldoutStates[category], category, true, EditorStyles.foldoutHeader);

                if (!_foldoutStates[category]) continue;

                EditorGUI.indentLevel++;
                foreach (var info in ComponentRegistry.GetByCategory(category))
                {
                    if (GUILayout.Button(info.DisplayName, EditorStyles.miniButton))
                        OnComponentSelected?.Invoke(info);
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Special ComponentInfo for adding a plain layout container.
        /// ComponentType is VisualElement, Factory returns null — handled specially by UxmlExporter.
        /// </summary>
        internal static readonly ComponentRegistry.ComponentInfo ContainerInfo =
            new("Container", "Layout", typeof(UnityEngine.UIElements.VisualElement), () => null);
    }
}
