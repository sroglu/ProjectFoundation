using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Per-scroll state captured between <see cref="KUI.BeginScroll"/> and
    /// <see cref="KUI.EndScroll"/>. Plain value type — no allocations, no
    /// IDisposable boxing.
    /// </summary>
    public struct KuiScrollHandle
    {
        internal float4 Viewport;
        internal float StartCursorY;
        internal bool Active;
    }

    internal static class KuiScrollImpl
    {
        public static KuiScrollHandle Begin(KuiContext ctx, ref Vector2 scrollPos, float4 viewportRect)
        {
            ctx.Layout.BeginScroll(viewportRect, scrollPos);

            var input = ctx.InputHandler.State;

            var mouse = input.MousePosition;
            bool mouseInViewport = mouse.x >= viewportRect.x && mouse.x <= viewportRect.x + viewportRect.z
                                && mouse.y >= viewportRect.y && mouse.y <= viewportRect.y + viewportRect.w;
            if (mouseInViewport)
            {
                float scrollSpeed = KuiDPI.Px(20f);
                scrollPos.y -= input.ScrollDelta * scrollSpeed;
            }

            // Touch swipe → scroll. Touch doesn't generate Input.mouseScrollDelta
            // on mobile, so we apply the per-frame finger delta directly.
            // Skipped while a window drag/resize is active so a finger that's
            // moving a window doesn't scroll the content beneath it.
            if (input.TouchCount > 0 && !KuiWindowManager.IsDragging)
            {
                var t = input.TouchPosition;
                bool touchInViewport = t.x >= viewportRect.x && t.x <= viewportRect.x + viewportRect.z
                                    && t.y >= viewportRect.y && t.y <= viewportRect.y + viewportRect.w;
                // Direct manipulation: content follows the finger. TouchDelta.y is +down (top-left
                // coords), so dragging down must DECREASE the down-scroll offset (reveal content above).
                if (touchInViewport && input.TouchPhase == (int)TouchPhase.Moved)
                    scrollPos.y -= input.TouchDelta.y;
            }

            if (scrollPos.y < 0) scrollPos.y = 0;

            return new KuiScrollHandle
            {
                Viewport     = viewportRect,
                StartCursorY = ctx.Layout.CursorY,
                Active       = true,
            };
        }

        public static void End(KuiContext ctx, ref Vector2 scrollPos, KuiScrollHandle h)
        {
            if (!h.Active) return;

            // CursorY tracks the logical layout cursor; the scroll offset only
            // shifts where widgets render, not where the cursor lands. So total
            // content height is just how far CursorY advanced from scroll start.
            float contentHeight = ctx.Layout.CursorY - h.StartCursorY;
            ctx.Layout.EndScroll();

            float maxScroll = contentHeight - h.Viewport.w;
            if (maxScroll < 0) maxScroll = 0;
            if (scrollPos.y > maxScroll) scrollPos.y = maxScroll;
            if (scrollPos.y < 0) scrollPos.y = 0;

            if (contentHeight > h.Viewport.w)
            {
                float barH = math.max(KuiDPI.Px(20f), h.Viewport.w * (h.Viewport.w / contentHeight));
                float barY = h.Viewport.y + (scrollPos.y / contentHeight) * h.Viewport.w;
                float barX = h.Viewport.x + h.Viewport.z - KuiDPI.Px(KuiStyles.ScrollBarWidth);
                ctx.CommandBuffer.PushRect(
                    new float4(barX, barY, KuiDPI.Px(KuiStyles.ScrollBarWidth), barH),
                    KuiStyles.ScrollBar,
                    new float4(h.Viewport.x, h.Viewport.y, h.Viewport.z, h.Viewport.w));
            }
        }
    }
}
