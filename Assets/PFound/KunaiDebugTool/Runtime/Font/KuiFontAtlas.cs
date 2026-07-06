using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal class KuiFontAtlas : IDisposable
    {
        public NativeArray<KuiGlyph> AsciiCache;
        public NativeParallelHashMap<int, KuiGlyph> ExtendedCache;
        public int BaseFontSize = 14;
        public bool IsLoaded { get; private set; }

        public KuiFontAtlas()
        {
            AsciiCache = new NativeArray<KuiGlyph>(128, Allocator.Persistent);
            ExtendedCache = new NativeParallelHashMap<int, KuiGlyph>(64, Allocator.Persistent);
        }

        public void Load(Texture2D atlas, TextAsset fntAsset)
        {
            if (atlas == null || fntAsset == null)
            {
                Debug.LogWarning("[Kunai] Font atlas or metrics missing — text rendering disabled.");
                GenerateFallbackGlyphs();
                return;
            }

            BmFontParser.Parse(fntAsset.text, atlas.width, atlas.height, AsciiCache, ExtendedCache, out BaseFontSize);
            IsLoaded = true;
        }

        void GenerateFallbackGlyphs()
        {
            var fallback = new KuiGlyph
            {
                UvTopLeft = float2.zero,
                UvTopRight = float2.zero,
                UvBottomLeft = float2.zero,
                UvBottomRight = float2.zero,
                Size = new float2(8, 14),
                Offset = float2.zero,
                Advance = 8
            };

            for (int i = 0; i < 128; i++)
                AsciiCache[i] = fallback;

            IsLoaded = false;
        }

        public float MeasureTextWidth(string text, float fontScale)
        {
            float width = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                int c = text[i];
                KuiGlyph glyph;
                if (c < 128) glyph = AsciiCache[c];
                else if (!ExtendedCache.TryGetValue(c, out glyph)) glyph = AsciiCache['?'];
                width += glyph.Advance * fontScale;
            }
            return width;
        }

        public void Dispose()
        {
            if (AsciiCache.IsCreated) AsciiCache.Dispose();
            if (ExtendedCache.IsCreated) ExtendedCache.Dispose();
        }
    }
}
