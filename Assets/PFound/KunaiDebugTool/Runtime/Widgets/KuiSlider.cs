using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal static class KuiSlider
    {
        static int _activeId;
        static int _nextId;

        public static void ResetIds() => _nextId = 0;

        public static float Draw(float value, float min, float max, KuiContext ctx)
        {
            int id = ++_nextId;
            float height = KuiDPI.Px(KuiStyles.SliderHeight);
            var rect = ctx.Layout.NextRect(height);
            var clip = ctx.Layout.CurrentClip;
            var mouse = ctx.InputHandler.State.MousePosition;

            float trackPad = KuiDPI.Px(4f);
            float trackX = rect.x + trackPad;
            float trackW = rect.z - trackPad * 2;

            bool hovered = KuiHit.In(mouse, rect, clip);

            if (hovered && ctx.InputHandler.State.MouseDown)
                _activeId = id;

            if (_activeId == id)
            {
                if (ctx.InputHandler.State.MouseHeld)
                {
                    float t = math.clamp((mouse.x - trackX) / trackW, 0f, 1f);
                    value = math.lerp(min, max, t);
                }
                else
                {
                    _activeId = 0;
                }
            }

            float fillT = math.clamp((value - min) / (max - min), 0f, 1f);
            float fillW = trackW * fillT;
            float thumbW = KuiDPI.Px(8f);

            ctx.CommandBuffer.PushRect(new float4(rect.x, rect.y, rect.z, height), KuiStyles.SliderTrack, clip);
            ctx.CommandBuffer.PushRect(new float4(trackX, rect.y + KuiDPI.Px(2f), fillW, height - KuiDPI.Px(4f)), KuiStyles.SliderFill, clip);
            ctx.CommandBuffer.PushRect(new float4(trackX + fillW - thumbW * 0.5f, rect.y, thumbW, height), KuiStyles.SliderThumb, clip);

            return value;
        }
    }
}
