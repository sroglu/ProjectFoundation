using UnityEditor;
using UnityEngine;
using PFound.Render.RenderContext;

namespace PFound.Render.RenderContext.Editor
{
    [CustomEditor(typeof(RenderContextSinkBehaviour))]
    public sealed class RenderContextSinkBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying) return;

            var t = (RenderContextSinkBehaviour)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Diagnostics", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("IsAlive", t.IsAlive);
                var tex = t.Texture;
                EditorGUILayout.LabelField("Texture", tex != null ? $"{tex.width}×{tex.height} ({tex.format})" : "<null>");
                var cam = t.Camera;
                EditorGUILayout.ObjectField("Camera", cam, typeof(Camera), allowSceneObjects: true);
                var root = t.ContentRoot;
                EditorGUILayout.ObjectField("ContentRoot", root, typeof(Transform), allowSceneObjects: true);
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
