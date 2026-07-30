using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    internal class KuiLayout
    {
        public float CursorX;
        public float CursorY;
        public float ContentWidth;
        public float ContentStartX;
        public float ContentStartY;
        public KuiClipStack ClipStack;

        float _groupStartX;
        float _groupMaxHeight;
        bool _inHorizontalGroup;

        float _scrollOffsetX;
        float _scrollOffsetY;

        public KuiLayout()
        {
            ClipStack = new KuiClipStack(8);
        }

        public void BeginFrame(float screenWidth, float screenHeight)
        {
            CursorX = 0;
            CursorY = 0;
            ContentWidth = screenWidth;
            ClipStack.Clear();
            ClipStack.Push(new float4(0, 0, screenWidth, screenHeight));
        }

        public void BeginWindow(Rect windowRect, float4 parentClip)
        {
            float pad = KuiDPI.Px(KuiStyles.Padding);
            float titleH = KuiDPI.Px(KuiStyles.TitleBarHeight);

            ContentStartX = windowRect.x + pad;
            ContentStartY = windowRect.y + titleH + pad;
            ContentWidth = windowRect.width - pad * 2;
            CursorX = ContentStartX;
            CursorY = ContentStartY;

            ClipStack.Push(new float4(windowRect.x, windowRect.y, windowRect.width, windowRect.height));
            _scrollOffsetX = 0;
            _scrollOffsetY = 0;
        }

        public void EndWindow()
        {
            ClipStack.Pop();
            _inHorizontalGroup = false;
        }

        public float4 NextRect(float height)
        {
            float spacing = KuiDPI.Px(KuiStyles.Spacing);

            if (_inHorizontalGroup)
            {
                float4 rect = new(CursorX, CursorY - _scrollOffsetY, ContentWidth, height);
                if (height > _groupMaxHeight) _groupMaxHeight = height;
                return rect;
            }

            float4 r = new(CursorX - _scrollOffsetX, CursorY - _scrollOffsetY, ContentWidth, height);
            CursorY += height + spacing;
            return r;
        }

        public float4 NextRect(float width, float height)
        {
            float spacing = KuiDPI.Px(KuiStyles.Spacing);

            if (_inHorizontalGroup)
            {
                float4 rect = new(CursorX - _scrollOffsetX, CursorY - _scrollOffsetY, width, height);
                CursorX += width + spacing;
                if (height > _groupMaxHeight) _groupMaxHeight = height;
                return rect;
            }

            float4 r = new(CursorX - _scrollOffsetX, CursorY - _scrollOffsetY, width, height);
            CursorY += height + spacing;
            return r;
        }

        public void BeginGroup()
        {
            _inHorizontalGroup = true;
            _groupStartX = CursorX;
            _groupMaxHeight = 0;
        }

        public void EndGroup()
        {
            _inHorizontalGroup = false;
            CursorX = ContentStartX;
            CursorY += _groupMaxHeight + KuiDPI.Px(KuiStyles.Spacing);
            _groupMaxHeight = 0;
        }

        public void BeginScroll(float4 viewportRect, Vector2 scrollPos)
        {
            ClipStack.Push(viewportRect);
            _scrollOffsetX = scrollPos.x;
            _scrollOffsetY = scrollPos.y;
        }

        public void EndScroll()
        {
            ClipStack.Pop();
            _scrollOffsetX = 0;
            _scrollOffsetY = 0;
        }

        public float4 CurrentClip => ClipStack.Current;
    }
}
