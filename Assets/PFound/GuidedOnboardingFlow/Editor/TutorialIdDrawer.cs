using PFound.GuidedOnboardingFlow.Core;
using UnityEditor;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.EditorTools
{
    /// <summary>
    /// Draws a <see cref="TutorialId"/> as its underlying integer handle with a "#" affordance, and warns
    /// when a definition is still left at the reserved 0 ("none") value so authors don't ship an unset id.
    /// </summary>
    [CustomPropertyDrawer(typeof(TutorialId))]
    public sealed class TutorialIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty handle = property.FindPropertyRelative("handle");

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            var tag = new Rect(position.x, position.y, 16f, position.height);
            var field = new Rect(position.x + 18f, position.y, position.width - 18f, position.height);

            Color prev = GUI.color;
            if (handle.intValue == 0)
                GUI.color = new Color(1f, 0.7f, 0.4f);
            GUI.Label(tag, "#");
            GUI.color = prev;

            handle.intValue = EditorGUI.IntField(field, handle.intValue);

            EditorGUI.EndProperty();
        }
    }
}
