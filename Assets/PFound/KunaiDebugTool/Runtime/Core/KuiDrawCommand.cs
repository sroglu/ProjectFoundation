using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Kunai
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct KuiDrawCommand
    {
        public enum Type : byte
        {
            Rect    = 0,
            Label   = 1,
            SetClip = 2,
            // Line: oriented thin quad between two points. Reuses Rect as
            // (p0.x, p0.y, p1.x, p1.y) and consumes Thickness for width.
            // Same vertex format / shader / draw call as Rect — no new pass.
            Line    = 3,
        }

        [FieldOffset(0)]  public float4 Rect;
        [FieldOffset(16)] public uint   PackedColor;
        [FieldOffset(20)] public Type   CmdType;

        [FieldOffset(24)] public int    TextOffset;
        [FieldOffset(28)] public int    TextLength;

        [FieldOffset(32)] public float4 ClipRect;
        [FieldOffset(48)] public byte   Layer;

        // Line thickness in pixels (Line type only; ignored by Rect/Label).
        // Sits in the unused tail of the 64-byte struct so layout is unchanged.
        [FieldOffset(52)] public float  Thickness;
    }
}
