using Unity.Mathematics;

namespace Kunai
{
    internal struct KuiInputState
    {
        public float2 MousePosition;
        public bool MouseDown;
        public bool MouseHeld;
        public bool MouseUp;
        // Pointer position captured on the press (MouseDown) edge, top-left origin.
        // Stays put for the whole press so widgets can compare press vs release.
        public float2 PressPosition;
        // True for the single frame a press is released (MouseUp) AS A TAP: the
        // pointer travelled less than the drag threshold between press and release.
        // This is THE click signal — never use raw MouseUp to mean "clicked", or a
        // drag that ends over a widget (e.g. a scroll swipe at the content limit,
        // where nothing visibly moved) gets mistaken for a click.
        public bool IsTap;
        public float ScrollDelta;
        public float2 TouchPosition;
        // Per-frame finger movement in screen pixels, top-left origin (matches
        // MousePosition / TouchPosition). Zero when no touch is active.
        // Used by scroll widgets to translate touch swipe into scroll motion
        // since touch doesn't generate Input.mouseScrollDelta on mobile.
        public float2 TouchDelta;
        public int TouchPhase;
        public int TouchCount;
    }
}
