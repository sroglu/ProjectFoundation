using System.Runtime.InteropServices;
using UnityEngine;

namespace Kunai
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct KuiVertex
    {
        public Vector3 Position;
        public Color32 Color32;
        public Vector2 UV;
    }
}
