using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Horizontally-scrollable strip of toggleable chips. Used by D2 Console
    /// for the category filter row. <see cref="Draw"/> returns the index of
    /// the chip toggled this frame, or -1.
    /// </summary>
    internal static class KuiChipStrip
    {
        public const float DefaultHeight   = 22f;
        public const float ChipPadding     = 8f;     // horizontal text padding
        public const float ChipSpacing     = 4f;     // gap between chips
        public const float ChipScrollSpeed = 24f;    // wheel sensitivity, px/notch

        // Same monospace approximation as KuiTextField — font advance metrics
        // aren't exposed outside the Burst job yet.
        const float MonoCharWidthRatio = 0.55f;

        public static int Draw(int widgetId, IReadOnlyList<string> labels, IList<bool> states,
                               ref Vector2 scroll, KuiContext ctx, float height = 0f)
        {
            if (labels == null || states == null) return -1;
            if (labels.Count != states.Count)
                throw new System.ArgumentException("labels.Count must equal states.Count");

            int count = labels.Count;

            float h    = height > 0f ? KuiDPI.Px(height) : KuiDPI.Px(DefaultHeight);
            float4 row = ctx.Layout.NextRect(h);
            float4 outerClip = ctx.Layout.CurrentClip;

            // Tight clip = strip rect ∩ outer (window/scroll) clip. Without this
            // chips that scroll past the strip edge bleed into neighbouring rows.
            float4 stripClip = IntersectClip(row, outerClip);

            // Background — slightly darker than the panel so chips have contrast.
            ctx.CommandBuffer.PushRect(row, KuiStyles.SliderTrack, outerClip);

            var mouse   = ctx.InputHandler.State.MousePosition;
            // Clip-aware: a strip clipped away by a shrunk window must not react (scroll/click). Chip
            // hit-tests below all gate on inStrip, so this guards them too.
            bool inStrip = KuiHit.In(mouse, row, outerClip);

            // Mouse wheel scrolls horizontally when the cursor is over the strip.
            if (inStrip && Mathf.Abs(ctx.InputHandler.State.ScrollDelta) > 0.001f)
            {
                scroll.x -= ctx.InputHandler.State.ScrollDelta * KuiDPI.Px(ChipScrollSpeed);
            }

            // Touch drag scrolls the strip horizontally (no mouse wheel on device). Horizontal-dominant
            // only, so a vertical drag still scrolls the console list beneath instead of the strip.
            var st = ctx.InputHandler.State;
            if (inStrip && st.TouchCount > 0 && st.TouchPhase == (int)TouchPhase.Moved
                && !KuiWindowManager.IsDragging
                && Mathf.Abs(st.TouchDelta.x) > Mathf.Abs(st.TouchDelta.y))
            {
                scroll.x -= st.TouchDelta.x;
            }

            float charW   = KuiDPI.Px(ctx.Settings.BaseFontSize) * MonoCharWidthRatio;
            float pad     = KuiDPI.Px(ChipPadding);
            float spacing = KuiDPI.Px(ChipSpacing);
            float chipH   = h - KuiDPI.Px(4f);
            float chipY   = row.y + KuiDPI.Px(2f);

            // First pass — compute total content width so we can clamp scroll.
            float totalW = 0f;
            for (int i = 0; i < count; i++)
            {
                var label = labels[i] ?? string.Empty;
                totalW += label.Length * charW + pad * 2f;
                if (i < count - 1) totalW += spacing;
            }
            float maxScroll = math.max(0f, totalW - row.z);
            scroll.x = math.clamp(scroll.x, 0f, maxScroll);

            // Second pass — draw + hit-test.
            int toggled = -1;
            float originX = row.x - scroll.x;
            float cursor  = originX;

            bool wantClick = ctx.InputHandler.State.IsTap && inStrip;

            for (int i = 0; i < count; i++)
            {
                var label = labels[i] ?? string.Empty;
                float chipW = label.Length * charW + pad * 2f;

                // Skip chips entirely outside the strip viewport — saves a few
                // PushRect calls on long category lists.
                bool offLeft  = cursor + chipW < row.x;
                bool offRight = cursor > row.x + row.z;
                if (!offLeft && !offRight)
                {
                    Color32 bg = states[i] ? KuiStyles.SliderFill : KuiStyles.Button;

                    bool overChip = wantClick
                                 && mouse.x >= cursor && mouse.x <= cursor + chipW
                                 && mouse.y >= chipY  && mouse.y <= chipY + chipH;
                    if (overChip)
                    {
                        states[i] = !states[i];
                        toggled = i;
                        bg = states[i] ? KuiStyles.SliderFill : KuiStyles.Button;
                    }

                    ctx.CommandBuffer.PushRect(new float4(cursor, chipY, chipW, chipH), bg, stripClip);
                    ctx.CommandBuffer.PushLabel(label,
                        new float4(cursor + pad, chipY + KuiDPI.Px(2f), chipW - pad * 2f, chipH),
                        KuiStyles.Text, stripClip);
                }

                cursor += chipW + spacing;
            }

            return toggled;
        }

        static float4 IntersectClip(float4 a, float4 b)
        {
            float x0 = math.max(a.x, b.x);
            float y0 = math.max(a.y, b.y);
            float x1 = math.min(a.x + a.z, b.x + b.z);
            float y1 = math.min(a.y + a.w, b.y + b.w);
            return new float4(x0, y0, math.max(0f, x1 - x0), math.max(0f, y1 - y0));
        }
    }
}
