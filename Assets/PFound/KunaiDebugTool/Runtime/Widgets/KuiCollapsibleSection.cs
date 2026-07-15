using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Collapsible header row used by Inspector + System Info to group related
    /// rows. Pairs <see cref="DrawHeader"/> with no explicit End — the caller
    /// decides whether to render children based on the returned expanded state.
    /// </summary>
    internal static class KuiCollapsibleSection
    {
        public const float HeaderHeight = 22f;
        // Use the bundled Nerd Font glyphs — KuiIcons.ChevronDown / ChevronRight
        // are already present in the atlas; literal `▾` / `▸` aren't.
        static readonly string ChevronExpanded  = KuiIcons.ChevronDown;
        static readonly string ChevronCollapsed = KuiIcons.ChevronRight;

        // Returns the (possibly toggled) expanded state. The caller passes the
        // current value in and writes the return value back. KUI.BeginCollapsible
        // wraps this and exposes a ref-bool API.
        public static bool DrawHeader(int widgetId, string title, bool expanded, KuiContext ctx)
        {
            float h    = KuiDPI.Px(HeaderHeight);
            float4 row = ctx.Layout.NextRect(h);
            float4 clip = ctx.Layout.CurrentClip;

            var mouse = ctx.InputHandler.State.MousePosition;
            bool hovered = KuiHit.In(mouse, row, clip);
            bool clicked = hovered && ctx.InputHandler.State.IsTap;

            if (clicked) expanded = !expanded;

            Color32 bg = hovered ? KuiStyles.ButtonHover : KuiStyles.TitleBar;
            ctx.CommandBuffer.PushRect(row, bg, clip);

            float pad = KuiDPI.Px(KuiStyles.Padding);

            // Chevron — fixed-width slot on the left.
            float chevronW = KuiDPI.Px(14f);
            ctx.CommandBuffer.PushLabel(expanded ? ChevronExpanded : ChevronCollapsed,
                new float4(row.x + pad, row.y + KuiDPI.Px(2f), chevronW, h),
                KuiStyles.Text, clip);

            // Title — takes remaining width.
            float titleX = row.x + pad + chevronW;
            ctx.CommandBuffer.PushLabel(title ?? string.Empty,
                new float4(titleX, row.y + KuiDPI.Px(2f), row.z - (titleX - row.x) - pad, h),
                KuiStyles.Text, clip);

            return expanded;
        }
    }
}
