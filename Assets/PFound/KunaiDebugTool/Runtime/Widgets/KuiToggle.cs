using UnityEngine;

namespace Kunai
{
    internal static class KuiToggle
    {
        public static bool Draw(bool value, string label, KuiContext ctx, float width = -1f)
        {
            float size = KuiDPI.Px(KuiStyles.ToggleSize);
            float height = KuiDPI.Px(KuiStyles.ButtonHeight);
            var rect = width > 0
                ? ctx.Layout.NextRect(KuiDPI.Px(width), height)
                : ctx.Layout.NextRect(height);
            var clip = ctx.Layout.CurrentClip;
            var mouse = ctx.InputHandler.State.MousePosition;

            bool clicked = ctx.InputHandler.State.IsTap && KuiHit.In(mouse, rect, clip);

            if (clicked) value = !value;

            float checkY = rect.y + (height - size) * 0.5f;
            Color32 boxColor = value ? KuiStyles.Toggle : KuiStyles.ToggleOff;
            ctx.CommandBuffer.PushRect(new Unity.Mathematics.float4(rect.x, checkY, size, size), boxColor, clip);

            float textX = rect.x + size + KuiDPI.Px(KuiStyles.Padding);
            float textY = rect.y + KuiDPI.Px(4f);
            ctx.CommandBuffer.PushLabel(label,
                new Unity.Mathematics.float4(textX, textY, rect.z - size - KuiDPI.Px(KuiStyles.Padding), rect.w),
                KuiStyles.Text, clip);

            return value;
        }
    }
}
