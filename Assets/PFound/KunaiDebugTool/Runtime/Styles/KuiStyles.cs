using UnityEngine;

namespace Kunai
{
    internal static class KuiStyles
    {
        public static readonly Color32 Background      = new(30, 30, 30, 230);
        public static readonly Color32 Panel           = new(40, 40, 50, 255);
        public static readonly Color32 TitleBar        = new(50, 50, 65, 255);
        public static readonly Color32 Border          = new(70, 70, 90, 255);
        public static readonly Color32 Text            = new(220, 220, 220, 255);
        public static readonly Color32 TextDim         = new(150, 150, 150, 255);
        public static readonly Color32 Button          = new(51, 51, 76, 255);
        public static readonly Color32 ButtonHover     = new(65, 65, 95, 255);
        public static readonly Color32 ButtonPress     = new(40, 40, 60, 255);
        public static readonly Color32 Toggle          = new(80, 160, 80, 255);
        public static readonly Color32 ToggleOff       = new(60, 60, 70, 255);
        public static readonly Color32 SliderTrack     = new(50, 50, 60, 255);
        public static readonly Color32 SliderFill      = new(80, 120, 200, 255);
        public static readonly Color32 SliderThumb     = new(200, 200, 210, 255);
        public static readonly Color32 Separator       = new(60, 60, 75, 180);
        public static readonly Color32 ScrollBar       = new(80, 80, 100, 150);
        public static readonly Color32 WindowMinBubble = new(60, 60, 80, 200);
        public static readonly Color32 ResizeHandle    = new(95, 110, 150, 255);   // visible corner grip
        public static readonly Color32 ResizeHandleGrip = new(200, 205, 220, 255); // grip lines on the handle

        public const float Padding = 6f;
        public const float Spacing = 4f;
        public const float TitleBarHeight = 30f;
        public const float ButtonHeight = 28f;
        public const float ToggleSize = 16f;
        public const float SliderHeight = 20f;
        public const float SeparatorHeight = 1f;
        public const float ScrollBarWidth = 6f;
        // Edge/corner grab band for resizing. Wide enough to hit reliably with a finger on touch
        // (5f was mouse-era and nearly impossible to grab on a phone).
        public const float ResizeGrabWidth = 30f;
        public const float MinWindowWidth = 150f;
        public const float MinWindowHeight = 80f;
    }
}
