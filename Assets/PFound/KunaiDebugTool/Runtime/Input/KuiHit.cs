using Unity.Mathematics;

namespace Kunai
{
    /// <summary>
    /// Clip-aware pointer hit-test. A widget is "hovered" only when the pointer is inside BOTH its rect
    /// AND the active clip rect. Without the clip term, content scrolled out of view or cut off by a
    /// resized (shrunk) window stays interactive at its phantom position — tapping the now-empty area
    /// triggers the invisible widget. Render already respects the clip; input must too.
    /// </summary>
    internal static class KuiHit
    {
        public static bool In(float2 p, float4 rect, float4 clip)
        {
            // Single choke point for ALL widget hit-tests: only the active (topmost, non-dragging)
            // window accepts input, so overlapped lower windows and drag-release frames don't react.
            var ctx = KuiContext.Instance;
            if (ctx != null && !ctx.CurrentWindowInputActive) return false;

            return p.x >= rect.x && p.x <= rect.x + rect.z
                && p.y >= rect.y && p.y <= rect.y + rect.w
                && p.x >= clip.x && p.x <= clip.x + clip.z
                && p.y >= clip.y && p.y <= clip.y + clip.w;
        }
    }
}
