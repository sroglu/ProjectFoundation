using UnityEngine;

namespace Kunai
{
    internal static class KuiButton
    {
        public static bool Draw(string text, KuiContext ctx, float width = -1f)
        {
            float height = KuiDPI.Px(KuiStyles.ButtonHeight);
            var rect = width > 0
                ? ctx.Layout.NextRect(KuiDPI.Px(width), height)
                : ctx.Layout.NextRect(height);
            var clip = ctx.Layout.CurrentClip;
            var mouse = ctx.InputHandler.State.MousePosition;

            bool hovered = KuiHit.In(mouse, rect, clip);
            bool pressed = hovered && ctx.InputHandler.State.MouseHeld;
            bool clicked = hovered && ctx.InputHandler.State.IsTap;

            Color32 bgColor = pressed ? KuiStyles.ButtonPress
                            : hovered ? KuiStyles.ButtonHover
                            : KuiStyles.Button;

            ctx.CommandBuffer.PushRect(rect, bgColor, clip);

            float textX = rect.x + KuiDPI.Px(KuiStyles.Padding);
            float textY = rect.y + KuiDPI.Px(4f);
            ctx.CommandBuffer.PushLabel(text,
                new Unity.Mathematics.float4(textX, textY, rect.z - KuiDPI.Px(KuiStyles.Padding * 2), rect.w),
                KuiStyles.Text, clip);

            return clicked;
        }
    }
}
