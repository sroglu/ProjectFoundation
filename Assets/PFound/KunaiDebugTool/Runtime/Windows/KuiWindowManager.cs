using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal static class KuiWindowManager
    {
        enum DragMode { None, Move, ResizeLeft, ResizeRight, ResizeTop, ResizeBottom, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight, BubbleMove }

        static DragMode _dragMode = DragMode.None;
        static int _dragWindowId = -1;
        static float2 _dragOffset;

        // True while a window is being dragged or resized. Read by scroll
        // widgets so a touch that's busy moving a window doesn't ALSO scroll
        // the viewport beneath the finger.
        public static bool IsDragging => _dragMode != DragMode.None;

        // True for the single frame a drag/resize ends — so the release isn't ALSO consumed as a
        // widget click under the pointer. Reset every frame at the top of UpdateInteraction.
        public static bool ConsumedRelease { get; private set; }

        public static void UpdateInteraction(List<KuiWindowState> states, KuiInputHandler input, KuiSettings settings)
        {
            ConsumedRelease = false;
            var mouse = input.State.MousePosition;

            if (input.State.MouseUp)
            {
                if (_dragMode != DragMode.None)
                {
                    // Bubble drags float freely (no edge/neighbour snap); only real windows snap.
                    if (_dragMode != DragMode.BubbleMove)
                        ApplySnap(states, _dragWindowId, settings.SnapThreshold);
                    _dragMode = DragMode.None;
                    _dragWindowId = -1;
                    ConsumedRelease = true;
                    return;
                }

                // No drag in progress: a TAP on a bubble's restore button un-minimizes it. The rest of
                // the bubble is a drag handle (handled on press below), so tapping the title does NOT
                // restore — only this button does.
                if (input.State.IsTap)
                {
                    for (int i = 0; i < states.Count; i++)
                    {
                        var s = states[i];
                        if (!s.IsVisible || !s.IsMinimized) continue;
                        if (PointIn(mouse, BubbleRestoreButtonRect(s)))
                        {
                            s.IsMinimized = false;
                            states[i] = s;
                            ConsumedRelease = true;
                            break;
                        }
                    }
                }
                return;
            }

            if (_dragMode != DragMode.None && input.State.MouseHeld)
            {
                int idx = FindWindowById(states, _dragWindowId);
                if (idx >= 0)
                {
                    var s = states[idx];
                    if (_dragMode == DragMode.BubbleMove)
                    {
                        s.BubblePosition = new Vector2(mouse.x - _dragOffset.x, mouse.y - _dragOffset.y);
                        ClampBubbleToScreen(ref s);
                    }
                    else
                    {
                        ApplyDrag(ref s, mouse, _dragMode);
                        ClampToScreen(ref s);
                    }
                    states[idx] = s;
                }
                return;
            }

            if (!input.State.MouseDown) return;

            // Press on a minimized bubble starts dragging it — EXCEPT on the restore button, which is a
            // tap action (handled on release above). Bubbles are checked before real windows so a bubble
            // sitting over a window still grabs the press.
            for (int i = states.Count - 1; i >= 0; i--)
            {
                var s = states[i];
                if (!s.IsVisible || !s.IsMinimized) continue;
                if (!PointIn(mouse, BubbleRect(s))) continue;
                if (PointIn(mouse, BubbleRestoreButtonRect(s))) return;   // restore button → no drag

                _dragMode = DragMode.BubbleMove;
                _dragWindowId = s.Id;
                _dragOffset = mouse - new float2(s.BubblePosition.x, s.BubblePosition.y);
                return;
            }

            for (int i = states.Count - 1; i >= 0; i--)
            {
                var s = states[i];
                if (!s.IsVisible || s.IsMinimized) continue;

                float grab = KuiDPI.Px(KuiStyles.ResizeGrabWidth);
                float titleH = KuiDPI.Px(KuiStyles.TitleBarHeight);
                var r = s.Rect;

                var mode = DetectGrabZone(mouse, r, grab, titleH);
                if (mode == DragMode.None) continue;

                _dragMode = mode;
                _dragWindowId = s.Id;
                _dragOffset = mouse - new float2(r.x, r.y);

                if (mode == DragMode.Move)
                {
                    // Visible minimize button (drawn by KuiContext) sits in the title bar just left of
                    // the top-right resize grip. Pressing it folds the window to its bubble instead of
                    // starting a move. Same rect is used to render and to hit-test (MinimizeButtonRect).
                    var mb = MinimizeButtonRect(r);
                    if (mouse.x >= mb.x && mouse.x <= mb.x + mb.z && mouse.y >= mb.y && mouse.y <= mb.y + mb.w)
                    {
                        _dragMode = DragMode.None;
                        s.IsMinimized = true;
                        s.BubblePosition = new Vector2(r.x, r.y);
                        states[i] = s;
                    }
                }
                break;
            }
        }

        // Title-bar minimize button rect, just LEFT of the top-right resize grip. Single source of
        // geometry for both rendering (KuiContext.DrawWindowChrome) and the press hit-test above —
        // "what you see is what you press". A square sized to the title bar height.
        public static float4 MinimizeButtonRect(Rect r)
        {
            float g = KuiDPI.Px(KuiStyles.ResizeGrabWidth);
            float titleH = KuiDPI.Px(KuiStyles.TitleBarHeight);
            float x = r.x + r.width - g - titleH;
            return new float4(x, r.y, titleH, titleH);
        }

        // Minimized-window bubble geometry — shared by rendering (KuiContext.DrawMinimizedBubble) and
        // the drag / restore hit-tests here. The bubble is a title-bar-height strip; its rightmost
        // square is the restore (un-minimize) button, the rest is the drag handle.
        public static float4 BubbleRect(KuiWindowState s)
        {
            float h = KuiDPI.Px(KuiStyles.TitleBarHeight);
            return new float4(s.BubblePosition.x, s.BubblePosition.y, KuiDPI.Px(BubbleWidthPx), h);
        }

        public static float4 BubbleRestoreButtonRect(KuiWindowState s)
        {
            float h = KuiDPI.Px(KuiStyles.TitleBarHeight);
            return new float4(s.BubblePosition.x + KuiDPI.Px(BubbleWidthPx) - h, s.BubblePosition.y, h, h);
        }

        const float BubbleWidthPx = 140f;

        static bool PointIn(float2 p, float4 r)
            => p.x >= r.x && p.x <= r.x + r.z && p.y >= r.y && p.y <= r.y + r.w;

        static void ClampBubbleToScreen(ref KuiWindowState s)
        {
            float w = KuiDPI.Px(BubbleWidthPx);
            float h = KuiDPI.Px(KuiStyles.TitleBarHeight);
            var p = s.BubblePosition;
            p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - w));
            p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - h));
            s.BubblePosition = p;
        }

        static DragMode DetectGrabZone(float2 mouse, Rect r, float grab, float titleH)
        {
            bool inRect = mouse.x >= r.x && mouse.x <= r.x + r.width && mouse.y >= r.y && mouse.y <= r.y + r.height;
            if (!inRect) return DragMode.None;

            bool left = mouse.x < r.x + grab;
            bool right = mouse.x > r.x + r.width - grab;
            bool top = mouse.y < r.y + grab;
            bool bottom = mouse.y > r.y + r.height - grab;

            // Resize ONLY from the two visible corner handles (top-right, bottom-left). Edge/other-corner
            // grab bands hijacked taps on small screens, making move + scroll hard. Move = title bar.
            if (top && right) return DragMode.ResizeTopRight;
            if (bottom && left) return DragMode.ResizeBottomLeft;

            if (mouse.y < r.y + titleH) return DragMode.Move;

            return DragMode.None;
        }

        static void ApplyDrag(ref KuiWindowState s, float2 mouse, DragMode mode)
        {
            var r = s.Rect;
            float minW = s.MinSize.x;
            float minH = s.MinSize.y;

            switch (mode)
            {
                case DragMode.Move:
                    r.x = mouse.x - _dragOffset.x;
                    r.y = mouse.y - _dragOffset.y;
                    break;
                case DragMode.ResizeRight:
                    r.width = math.max(minW, mouse.x - r.x);
                    break;
                case DragMode.ResizeBottom:
                    r.height = math.max(minH, mouse.y - r.y);
                    break;
                case DragMode.ResizeLeft:
                    float newX = mouse.x;
                    float newW = r.x + r.width - newX;
                    if (newW >= minW) { r.x = newX; r.width = newW; }
                    break;
                case DragMode.ResizeTop:
                    float newY = mouse.y;
                    float newH = r.y + r.height - newY;
                    if (newH >= minH) { r.y = newY; r.height = newH; }
                    break;
                case DragMode.ResizeBottomRight:
                    r.width = math.max(minW, mouse.x - r.x);
                    r.height = math.max(minH, mouse.y - r.y);
                    break;
                case DragMode.ResizeTopLeft:
                {
                    float nx = mouse.x; float nw = r.x + r.width - nx;
                    float ny = mouse.y; float nh = r.y + r.height - ny;
                    if (nw >= minW) { r.x = nx; r.width = nw; }
                    if (nh >= minH) { r.y = ny; r.height = nh; }
                    break;
                }
                case DragMode.ResizeTopRight:
                {
                    r.width = math.max(minW, mouse.x - r.x);
                    float ny = mouse.y; float nh = r.y + r.height - ny;
                    if (nh >= minH) { r.y = ny; r.height = nh; }
                    break;
                }
                case DragMode.ResizeBottomLeft:
                {
                    float nx = mouse.x; float nw = r.x + r.width - nx;
                    if (nw >= minW) { r.x = nx; r.width = nw; }
                    r.height = math.max(minH, mouse.y - r.y);
                    break;
                }
            }

            s.Rect = r;
        }

        static void ApplySnap(List<KuiWindowState> states, int windowId, float threshold)
        {
            int idx = FindWindowById(states, windowId);
            if (idx < 0) return;

            float snap = KuiDPI.Px(threshold);
            var s = states[idx];
            var r = s.Rect;
            float sw = Screen.width;
            float sh = Screen.height;

            if (math.abs(r.x) < snap) r.x = 0;
            if (math.abs(r.y) < snap) r.y = 0;
            if (math.abs(r.x + r.width - sw) < snap) r.x = sw - r.width;
            if (math.abs(r.y + r.height - sh) < snap) r.y = sh - r.height;

            for (int i = 0; i < states.Count; i++)
            {
                if (i == idx || !states[i].IsVisible || states[i].IsMinimized) continue;
                var other = states[i].Rect;

                if (math.abs(r.x - (other.x + other.width)) < snap) r.x = other.x + other.width;
                if (math.abs(r.x + r.width - other.x) < snap) r.x = other.x - r.width;
                if (math.abs(r.y - (other.y + other.height)) < snap) r.y = other.y + other.height;
                if (math.abs(r.y + r.height - other.y) < snap) r.y = other.y - r.height;
            }

            s.Rect = r;
            states[idx] = s;
        }

        static void ClampToScreen(ref KuiWindowState s)
        {
            float titleH = KuiDPI.Px(KuiStyles.TitleBarHeight);
            var r = s.Rect;
            if (r.y + titleH < 0) r.y = -titleH + 1;
            if (r.y > Screen.height - titleH) r.y = Screen.height - titleH;
            if (r.x + r.width < 30) r.x = 30 - r.width;
            if (r.x > Screen.width - 30) r.x = Screen.width - 30;
            s.Rect = r;
        }

        static int FindWindowById(List<KuiWindowState> states, int id)
        {
            for (int i = 0; i < states.Count; i++)
                if (states[i].Id == id) return i;
            return -1;
        }
    }
}
