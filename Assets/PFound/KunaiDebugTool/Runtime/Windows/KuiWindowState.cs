using UnityEngine;

namespace Kunai
{
    internal struct KuiWindowState
    {
        public int Id;
        public string Title;
        public Rect Rect;
        public Vector2 MinSize;
        public bool IsMinimized;
        public Vector2 BubblePosition;
        public int ZOrder;
        public bool IsVisible;
    }
}
