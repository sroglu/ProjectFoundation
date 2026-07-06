using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    [BurstCompile]
    internal struct VertexWriteJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<KuiDrawCommand> Commands;
        [ReadOnly] public NativeArray<int> VertexOffsets;
        [ReadOnly] public NativeArray<int> VertexCounts;
        [ReadOnly] public NativeArray<char> TextChars;
        [ReadOnly] public NativeArray<KuiGlyph> AsciiCache;
        [ReadOnly] public NativeParallelHashMap<int, KuiGlyph> ExtendedCache;
        public float FontScale;
        public float LineHeight;
        public float ScreenHeight;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<KuiVertex> Vertices;

        static readonly Vector2 SentinelUV = new(-1f, -1f);

        [BurstCompile]
        public void Execute(int index)
        {
            int count = VertexCounts[index];
            if (count == 0) return;

            int writePos = VertexOffsets[index];
            var cmd = Commands[index];

            float clipXMin = cmd.ClipRect.x;
            float clipYMin = cmd.ClipRect.y;
            float clipXMax = cmd.ClipRect.x + cmd.ClipRect.z;
            float clipYMax = cmd.ClipRect.y + cmd.ClipRect.w;

            uint cUint = cmd.PackedColor;
            Color32 color = UnsafeUtility.As<uint, Color32>(ref cUint);

            if (cmd.CmdType == KuiDrawCommand.Type.Rect)
            {
                WriteRect(writePos, cmd, clipXMin, clipYMin, clipXMax, clipYMax, color);
            }
            else if (cmd.CmdType == KuiDrawCommand.Type.Label)
            {
                WriteLabel(writePos, cmd, clipXMin, clipYMin, clipXMax, clipYMax, color);
            }
            else if (cmd.CmdType == KuiDrawCommand.Type.Line)
            {
                WriteLine(writePos, cmd, clipXMin, clipYMin, clipXMax, clipYMax, color);
            }
        }

        // Oriented thin quad: 4 corners offset perpendicular to (p1-p0) by
        // half-thickness. Same vertex format as Rect — same atlas, same
        // shader, same draw call. Sentinel UV (-1,-1) keeps the fragment in
        // colour-only mode.
        //
        // Per-vertex clipping: Liang-Barsky parametric clip on the original
        // (axis-aligned) line segment against the clip rect, executed BEFORE
        // computing the oriented quad. The clipped endpoints replace
        // (p0, p1); the quad still extends ±halfT perpendicular, so the
        // resulting geometry can leak up to halfT px past the clip border —
        // imperceptible at typical line thickness, prevents the multi-pixel
        // overflow caused by a partially-outside line being drawn in full.
        void WriteLine(int writePos, KuiDrawCommand cmd,
            float clipXMin, float clipYMin, float clipXMax, float clipYMax, Color32 color)
        {
            float p0x = cmd.Rect.x;
            float p0y = cmd.Rect.y;
            float p1x = cmd.Rect.z;
            float p1y = cmd.Rect.w;

            float dx = p1x - p0x;
            float dy = p1y - p0y;

            // Liang-Barsky parametric clip. After the four ClipAxis calls,
            // (t0, t1) parametrise the visible portion of the line; if t0>t1
            // the segment is entirely outside the clip (degenerate).
            float t0 = 0f, t1 = 1f;
            bool ok = true;
            ok = ok && ClipAxis(-dx, p0x - clipXMin, ref t0, ref t1);
            ok = ok && ClipAxis( dx, clipXMax - p0x, ref t0, ref t1);
            ok = ok && ClipAxis(-dy, p0y - clipYMin, ref t0, ref t1);
            ok = ok && ClipAxis( dy, clipYMax - p0y, ref t0, ref t1);

            if (!ok)
            {
                Vertices[writePos + 0] = default;
                Vertices[writePos + 1] = default;
                Vertices[writePos + 2] = default;
                Vertices[writePos + 3] = default;
                return;
            }

            float cp0x = p0x + dx * t0;
            float cp0y = p0y + dy * t0;
            float cp1x = p0x + dx * t1;
            float cp1y = p0y + dy * t1;

            float cdx = cp1x - cp0x;
            float cdy = cp1y - cp0y;
            float len = math.sqrt(cdx * cdx + cdy * cdy);
            if (len < 1e-4f)
            {
                Vertices[writePos + 0] = default;
                Vertices[writePos + 1] = default;
                Vertices[writePos + 2] = default;
                Vertices[writePos + 3] = default;
                return;
            }

            // Perpendicular unit vector × half-thickness. CCW-rotate
            // (-dy, dx) so the resulting quad ordering matches Rect's
            // (which is needed for the shared 0,1,2 / 0,2,3 index pattern).
            float halfT = cmd.Thickness * 0.5f;
            float invLen = 1f / len;
            float nx = -cdy * invLen * halfT;
            float ny =  cdx * invLen * halfT;

            float v0x = cp0x + nx, v0y = cp0y + ny;
            float v1x = cp1x + nx, v1y = cp1y + ny;
            float v2x = cp1x - nx, v2y = cp1y - ny;
            float v3x = cp0x - nx, v3y = cp0y - ny;

            float sy0 = ScreenHeight - v0y;
            float sy1 = ScreenHeight - v1y;
            float sy2 = ScreenHeight - v2y;
            float sy3 = ScreenHeight - v3y;

            Vertices[writePos + 0] = new KuiVertex { Position = new Vector3(v0x, sy0, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 1] = new KuiVertex { Position = new Vector3(v1x, sy1, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 2] = new KuiVertex { Position = new Vector3(v2x, sy2, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 3] = new KuiVertex { Position = new Vector3(v3x, sy3, 0), Color32 = color, UV = SentinelUV };
        }

        // Liang-Barsky single-axis test: clamps (t0, t1) to the parametric
        // sub-range of the line that lies inside the half-plane defined by
        // (p, q). Returns false when the segment is fully outside that
        // half-plane (caller short-circuits the line).
        static bool ClipAxis(float p, float q, ref float t0, ref float t1)
        {
            if (p == 0f) return q >= 0f;
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        void WriteRect(int writePos, KuiDrawCommand cmd,
            float clipXMin, float clipYMin, float clipXMax, float clipYMax, Color32 color)
        {
            float x0 = math.max(cmd.Rect.x, clipXMin);
            float y0 = math.max(cmd.Rect.y, clipYMin);
            float x1 = math.min(cmd.Rect.x + cmd.Rect.z, clipXMax);
            float y1 = math.min(cmd.Rect.y + cmd.Rect.w, clipYMax);

            float sy0 = ScreenHeight - y1;
            float sy1 = ScreenHeight - y0;

            Vertices[writePos + 0] = new KuiVertex { Position = new Vector3(x0, sy0, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 1] = new KuiVertex { Position = new Vector3(x1, sy0, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 2] = new KuiVertex { Position = new Vector3(x1, sy1, 0), Color32 = color, UV = SentinelUV };
            Vertices[writePos + 3] = new KuiVertex { Position = new Vector3(x0, sy1, 0), Color32 = color, UV = SentinelUV };
        }

        void WriteLabel(int writePos, KuiDrawCommand cmd,
            float clipXMin, float clipYMin, float clipXMax, float clipYMax, Color32 color)
        {
            float cursorX = cmd.Rect.x;
            float baseY = cmd.Rect.y;
            int wp = writePos;

            for (int i = 0; i < cmd.TextLength; i++)
            {
                char c = TextChars[cmd.TextOffset + i];

                // Newline: reset cursor, advance baseline. Emit zero-area quad.
                // CR is swallowed silently. Both still consume one quad slot
                // because VertexCountJob reserved TextLength*4 verts up front.
                if (c == '\n' || c == '\r')
                {
                    Vertices[wp + 0] = default;
                    Vertices[wp + 1] = default;
                    Vertices[wp + 2] = default;
                    Vertices[wp + 3] = default;
                    wp += 4;
                    if (c == '\n')
                    {
                        cursorX = cmd.Rect.x;
                        baseY += LineHeight * FontScale;
                    }
                    continue;
                }

                KuiGlyph glyph;
                if (c < 128) glyph = AsciiCache[c];
                else if (!ExtendedCache.TryGetValue(c, out glyph)) glyph = AsciiCache['?'];

                float gx = cursorX + glyph.Offset.x * FontScale;
                float gy = baseY + glyph.Offset.y * FontScale;
                float gw = glyph.Size.x * FontScale;
                float gh = glyph.Size.y * FontScale;

                float x0 = math.max(gx, clipXMin);
                float y0 = math.max(gy, clipYMin);
                float x1 = math.min(gx + gw, clipXMax);
                float y1 = math.min(gy + gh, clipYMax);

                if (x1 > x0 && y1 > y0)
                {
                    float tx0 = (x0 - gx) / gw;
                    float ty0 = (y0 - gy) / gh;
                    float tx1 = (x1 - gx) / gw;
                    float ty1 = (y1 - gy) / gh;

                    float u0 = math.lerp(glyph.UvTopLeft.x, glyph.UvTopRight.x, tx0);
                    float u1 = math.lerp(glyph.UvTopLeft.x, glyph.UvTopRight.x, tx1);
                    float v0 = math.lerp(glyph.UvTopLeft.y, glyph.UvBottomLeft.y, ty0);
                    float v1 = math.lerp(glyph.UvTopLeft.y, glyph.UvBottomLeft.y, ty1);

                    float sy0 = ScreenHeight - y1;
                    float sy1 = ScreenHeight - y0;

                    Vertices[wp + 0] = new KuiVertex { Position = new Vector3(x0, sy0, 0), Color32 = color, UV = new Vector2(u0, v1) };
                    Vertices[wp + 1] = new KuiVertex { Position = new Vector3(x1, sy0, 0), Color32 = color, UV = new Vector2(u1, v1) };
                    Vertices[wp + 2] = new KuiVertex { Position = new Vector3(x1, sy1, 0), Color32 = color, UV = new Vector2(u1, v0) };
                    Vertices[wp + 3] = new KuiVertex { Position = new Vector3(x0, sy1, 0), Color32 = color, UV = new Vector2(u0, v0) };
                }
                else
                {
                    Vertices[wp + 0] = default;
                    Vertices[wp + 1] = default;
                    Vertices[wp + 2] = default;
                    Vertices[wp + 3] = default;
                }

                wp += 4;
                cursorX += glyph.Advance * FontScale;
            }
        }
    }
}
