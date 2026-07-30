using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
using System.Text;
#endif

namespace Kunai
{
    /// <summary>
    /// Mobile-debug helper. Draws a small ring at the active touch / mouse
    /// position when <see cref="KuiSettings.EnableTouchIndicator"/> is true.
    /// NOT a <see cref="KuWindow"/> — calls <see cref="KuiCommandBuffer.PushRect"/>
    /// directly at the very END of <see cref="KuiContext"/>.TickInner so the
    /// ring draws on top of every window without needing a layer flag.
    ///
    /// Editor-only Ctrl+Space+P bonus: raycasts the EventSystem, finds the
    /// topmost uGUI element under the pointer, logs its full transform path
    /// and pings it in the hierarchy via <see cref="EditorGUIUtility.PingObject"/>.
    /// </summary>
    internal static class KuiTouchOverlay
    {
        public static void Render(KuiContext ctx, float screenW, float screenH)
        {
            if (ctx == null || ctx.Settings == null || !ctx.Settings.EnableTouchIndicator) return;

            // Pick the active pointer: touch wins on mobile, mouse on desktop.
            float2 pos;
            if (ctx.InputHandler.State.TouchCount > 0)
            {
                pos = ctx.InputHandler.State.TouchPosition;
            }
            else
            {
                pos = ctx.InputHandler.State.MousePosition;
            }

            // Ignore off-screen / sentinel positions.
            if (pos.x < 0 || pos.x > screenW || pos.y < 0 || pos.y > screenH) return;

            DrawRing(ctx, pos, screenW, screenH);

#if UNITY_EDITOR
            HandleEditorPing(pos, screenH);
#endif
        }

        // Approximates a circle with N thin rotated rects placed around the
        // perimeter. We don't have rotation in PushRect (axis-aligned only),
        // so each segment is a small square placed at the perimeter point —
        // visually "round-ish" at typical 24 px radii.
        static void DrawRing(KuiContext ctx, float2 center, float screenW, float screenH)
        {
            float r = KuiDPI.Px(ctx.Settings.TouchIndicatorRadiusPx);
            int   n = ctx.Settings.TouchIndicatorSegments;
            if (n < 4) n = 4;

            float dot = math.max(2f, KuiDPI.Px(3f));
            var clip = new float4(0, 0, screenW, screenH);
            var col  = KuiTouchColor;

            for (int i = 0; i < n; i++)
            {
                float t = (i / (float)n) * (math.PI * 2f);
                float x = center.x + math.cos(t) * r - dot * 0.5f;
                float y = center.y + math.sin(t) * r - dot * 0.5f;
                ctx.CommandBuffer.PushRect(new float4(x, y, dot, dot), col, clip);
            }

            // Center cross — gives a precise "this pixel" reference. Two thin
            // rects forming a +.
            float crossLen = KuiDPI.Px(8f);
            float crossThick = math.max(1f, KuiDPI.Px(1f));
            ctx.CommandBuffer.PushRect(
                new float4(center.x - crossLen * 0.5f, center.y - crossThick * 0.5f, crossLen, crossThick),
                col, clip);
            ctx.CommandBuffer.PushRect(
                new float4(center.x - crossThick * 0.5f, center.y - crossLen * 0.5f, crossThick, crossLen),
                col, clip);
        }

#if UNITY_EDITOR
        // Fires once on key-down. Triggered by Ctrl+Space+P (Cmd+Space+P on Mac).
        // Walks the current EventSystem's raycasters to find the topmost UI
        // element under the pointer, logs its transform path, pings it in the
        // hierarchy panel.
        static void HandleEditorPing(float2 pos, float screenH)
        {
            bool ctrl = KuiInput.GetKey(KeyCode.LeftControl) || KuiInput.GetKey(KeyCode.RightControl)
                     || KuiInput.GetKey(KeyCode.LeftCommand) || KuiInput.GetKey(KeyCode.RightCommand);
            bool space = KuiInput.GetKey(KeyCode.Space);
            bool pDown = KuiInput.GetKeyDown(KeyCode.P);
            if (!ctrl || !space || !pDown) return;

            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogWarning("[KuiTouch] No EventSystem in scene — Ctrl+Space+P needs uGUI input.");
                return;
            }

            // EventSystem expects screen coords with origin at bottom-left.
            // KuiInputState stores them with origin at top-left (matches our
            // ortho projection). Flip Y back for the raycast.
            var pd = new PointerEventData(es) { position = new Vector2(pos.x, screenH - pos.y) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(pd, hits);
            if (hits.Count == 0)
            {
                Debug.Log("[KuiTouch] no UI element under pointer at " + pos);
                return;
            }

            var top = hits[0].gameObject;
            EditorGUIUtility.PingObject(top);
            Selection.activeGameObject = top;
            Debug.Log("[KuiTouch] picked: " + GetPath(top.transform));
        }

        static string GetPath(Transform t)
        {
            var sb = new StringBuilder(64);
            sb.Append(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
#endif

        static readonly Color32 KuiTouchColor = new(255, 220, 0, 220);   // bright yellow ring
    }
}
