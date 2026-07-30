using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal struct KuiCommandBuffer : System.IDisposable
    {
        public NativeList<KuiDrawCommand> Commands;
        public NativeList<char> TextChars;

        public KuiCommandBuffer(int initialCommandCapacity, int initialCharCapacity)
        {
            Commands = new NativeList<KuiDrawCommand>(initialCommandCapacity, Allocator.Persistent);
            TextChars = new NativeList<char>(initialCharCapacity, Allocator.Persistent);
        }

        public void BeginFrame()
        {
            if (!Commands.IsCreated) return;
            Commands.Clear();
            TextChars.Clear();
        }

        public void PushRect(float4 rect, Color32 color, float4 clipRect, byte layer = 0)
        {
            Commands.Add(new KuiDrawCommand
            {
                Rect = rect,
                PackedColor = UnsafeUtility.As<Color32, uint>(ref color),
                CmdType = KuiDrawCommand.Type.Rect,
                ClipRect = clipRect,
                Layer = layer
            });
        }

        /// <summary>
        /// Oriented thin quad from <paramref name="p0"/> to <paramref name="p1"/>
        /// with the given pixel <paramref name="thickness"/>. Same atlas, same
        /// shader, same draw call as <see cref="PushRect"/> — only the four
        /// emitted vertices are non-axis-aligned. Clipping is by bounding-box
        /// cull (entirely-outside lines are dropped); partially-clipped lines
        /// may extend up to thickness/2 px beyond the clip border.
        /// </summary>
        public void PushLine(float2 p0, float2 p1, float thickness, Color32 color, float4 clipRect, byte layer = 0)
        {
            Commands.Add(new KuiDrawCommand
            {
                Rect = new float4(p0.x, p0.y, p1.x, p1.y),
                PackedColor = UnsafeUtility.As<Color32, uint>(ref color),
                CmdType = KuiDrawCommand.Type.Line,
                ClipRect = clipRect,
                Layer = layer,
                Thickness = thickness,
            });
        }

        public void PushLabel(string text, float4 rect, Color32 color, float4 clipRect, byte layer = 0)
        {
            PushLabel(text, 0, text?.Length ?? 0, rect, color, clipRect, layer);
        }

        public void PushLabel(string text, int start, int count, float4 rect, Color32 color, float4 clipRect, byte layer = 0)
        {
            if (string.IsNullOrEmpty(text) || count <= 0) return;
            if (start < 0) start = 0;
            int end = start + count;
            if (end > text.Length) end = text.Length;
            int len = end - start;
            if (len <= 0) return;

            int offset = TextChars.Length;
            for (int i = start; i < end; i++)
                TextChars.Add(text[i]);

            Commands.Add(new KuiDrawCommand
            {
                Rect = rect,
                PackedColor = UnsafeUtility.As<Color32, uint>(ref color),
                CmdType = KuiDrawCommand.Type.Label,
                TextOffset = offset,
                TextLength = len,
                ClipRect = clipRect,
                Layer = layer
            });
        }

        // Char-buffer overload — lets widgets like KuiTextField render a
        // mutable char[] backing store without allocating a string per frame.
        public void PushLabel(char[] text, int start, int count, float4 rect, Color32 color, float4 clipRect, byte layer = 0)
        {
            if (text == null || count <= 0) return;
            if (start < 0) start = 0;
            int end = start + count;
            if (end > text.Length) end = text.Length;
            int len = end - start;
            if (len <= 0) return;

            int offset = TextChars.Length;
            for (int i = start; i < end; i++)
                TextChars.Add(text[i]);

            Commands.Add(new KuiDrawCommand
            {
                Rect = rect,
                PackedColor = UnsafeUtility.As<Color32, uint>(ref color),
                CmdType = KuiDrawCommand.Type.Label,
                TextOffset = offset,
                TextLength = len,
                ClipRect = clipRect,
                Layer = layer
            });
        }

        public void Dispose()
        {
            if (Commands.IsCreated) Commands.Dispose();
            if (TextChars.IsCreated) TextChars.Dispose();
        }
    }
}
