using PFound.TweenPresetLibrary.Core;
using UnityEditor;
using UnityEngine;

namespace PFound.TweenPresetLibrary.Editor
{
    /// <summary>
    /// A non-destructive preview of a <see cref="TweenPreset"/>: per-playable-channel timing, end value, and the
    /// eased 0..1 curve sampled from the Core <see cref="EaseEvaluator"/> (overshoot drawn within the plot). Visualizes
    /// what the preset does without touching a scene object — the runtime applier is the apply-to-target path.
    /// </summary>
    public sealed class TweenPresetPreviewWindow : EditorWindow
    {
        private const int Samples = 48;
        private TweenPreset _preset;

        public static void Open()
        {
            var window = GetWindow<TweenPresetPreviewWindow>(false, "Tween Preset Preview");
            if (Selection.activeObject is TweenPreset selected) window._preset = selected;
            window.Show();
        }

        private void OnGUI()
        {
            _preset = (TweenPreset)EditorGUILayout.ObjectField("Preset", _preset, typeof(TweenPreset), false);
            if (_preset == null)
            {
                EditorGUILayout.HelpBox("Assign a TweenPreset to preview its channels.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Max duration", _preset.MaxDuration.ToString("0.###") + " s");
            EditorGUILayout.LabelField("Has any playable channel", _preset.HasAny.ToString());
            EditorGUILayout.Space();

            DrawChannel("Fade", _preset.Fade);
            DrawChannel("Scale", _preset.Scale);
            DrawChannel("Move", _preset.Move);
            DrawChannel("Rotate", _preset.Rotate);
        }

        private void DrawChannel<T>(string label, TweenChannel<T> channel) where T : struct
        {
            EditorGUILayout.Space();
            if (!channel.IsPlayable)
            {
                EditorGUILayout.LabelField(label, "— not playable —");
                return;
            }

            EditorGUILayout.LabelField(label,
                $"ease {channel.Ease}, delay {channel.Delay:0.###}s, duration {channel.Duration:0.###}s → {channel.EndValue}");
            DrawCurve(GUILayoutUtility.GetRect(100, 64), channel.Ease);
        }

        private static void DrawCurve(Rect rect, Ease ease)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

            // Sample once to find the value range (Back/Elastic overshoot [0,1]) so the plot fits.
            float min = 0f, max = 1f;
            for (int i = 0; i <= Samples; i++)
            {
                float v = EaseEvaluator.Evaluate(ease, i / (float)Samples);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float range = Mathf.Max(1e-4f, max - min);

            const float pad = 4f;
            var pts = new Vector3[Samples + 1];
            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                float v = EaseEvaluator.Evaluate(ease, t);
                float x = Mathf.Lerp(rect.x + pad, rect.xMax - pad, t);
                float y = Mathf.Lerp(rect.yMax - pad, rect.y + pad, (v - min) / range);
                pts[i] = new Vector3(x, y, 0f);
            }

            Handles.BeginGUI();
            Handles.color = new Color(0.4f, 0.8f, 1f);
            Handles.DrawAAPolyLine(2f, pts);
            Handles.EndGUI();
        }
    }
}
