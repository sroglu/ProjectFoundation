using Unity.Collections;
using Unity.Mathematics;

namespace Kunai
{
    internal static class BmFontParser
    {
        public static void Parse(string fntText, int atlasWidth, int atlasHeight,
            NativeArray<KuiGlyph> asciiCache,
            NativeParallelHashMap<int, KuiGlyph> extendedCache,
            out int baseFontSize)
        {
            baseFontSize = 14;
            float invW = 1f / atlasWidth;
            float invH = 1f / atlasHeight;

            var lines = fntText.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                var line = lines[li].Trim();

                if (line.StartsWith("info "))
                {
                    int size = ParseInt(line, "size=");
                    // BMFont convention: negative size means pixel units (vs. point units when positive).
                    baseFontSize = size < 0 ? -size : size;
                    continue;
                }

                if (!line.StartsWith("char ")) continue;

                int id = ParseInt(line, "id=");
                if (id < 0) continue;

                int x = ParseInt(line, "x=");
                int y = ParseInt(line, "y=");
                int w = ParseInt(line, "width=");
                int h = ParseInt(line, "height=");
                int xoff = ParseInt(line, "xoffset=");
                int yoff = ParseInt(line, "yoffset=");
                int xadv = ParseInt(line, "xadvance=");

                float u0 = x * invW;
                float v0 = y * invH;
                float u1 = (x + w) * invW;
                float v1 = (y + h) * invH;

                // Unity textures: V=0 at bottom, BMFont: V=0 at top → flip V
                float vFlip0 = 1f - v0;
                float vFlip1 = 1f - v1;

                var glyph = new KuiGlyph
                {
                    UvTopLeft = new float2(u0, vFlip0),
                    UvTopRight = new float2(u1, vFlip0),
                    UvBottomLeft = new float2(u0, vFlip1),
                    UvBottomRight = new float2(u1, vFlip1),
                    Size = new float2(w, h),
                    Offset = new float2(xoff, yoff),
                    Advance = xadv
                };

                if (id < 128) asciiCache[id] = glyph;
                else extendedCache[id] = glyph;
            }
        }

        static int ParseInt(string line, string key)
        {
            int start = line.IndexOf(key);
            if (start < 0) return 0;
            start += key.Length;
            int end = start;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-'))
                end++;
            if (end == start) return 0;
            int.TryParse(line.Substring(start, end - start), out int val);
            return val;
        }
    }
}
