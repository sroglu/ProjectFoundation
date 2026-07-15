using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.ScreenTools
{
    /// <summary>
    /// Helpers for laying out UI against the device screen: safe-area fitting,
    /// centered rectangles, and identifying which hardware display is active.
    /// </summary>
    public static class ScreenToolkit
    {
        /// <summary>
        /// Stretches <paramref name="target"/> so its anchors match the supplied
        /// pixel-space <paramref name="safeArea"/> within a screen of
        /// <paramref name="screenSize"/> pixels. Offsets are zeroed so the rect
        /// tracks the anchors exactly. Pass the parent canvas as the anchor
        /// reference; the target's anchors become fractions of the screen.
        /// </summary>
        public static void ApplySafeArea(RectTransform target, Rect safeArea, Vector2 screenSize)
        {
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Applies the current <see cref="Screen.safeArea"/> using the live
        /// screen resolution.
        /// </summary>
        public static void ApplySafeArea(RectTransform target)
        {
            ApplySafeArea(target, Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }

        /// <summary>
        /// Rectangle of <paramref name="size"/> centered inside a container of
        /// <paramref name="containerSize"/> whose origin is at (0, 0).
        /// </summary>
        public static Rect GetCenteredRect(Vector2 size, Vector2 containerSize)
        {
            Vector2 position = (containerSize - size) * 0.5f;
            return new Rect(position, size);
        }

        /// <summary>
        /// Rectangle of <paramref name="size"/> centered inside
        /// <paramref name="container"/>, respecting the container's origin.
        /// </summary>
        public static Rect GetCenteredRect(Vector2 size, Rect container)
        {
            Vector2 position = container.position + (container.size - size) * 0.5f;
            return new Rect(position, size);
        }

        /// <summary>
        /// Rectangle of <paramref name="size"/> centered in the live screen.
        /// </summary>
        public static Rect GetCenteredRect(Vector2 size)
        {
            return GetCenteredRect(size, new Vector2(Screen.width, Screen.height));
        }

        /// <summary>
        /// Index of the display currently hosting the main window within the
        /// platform's display layout. Returns 0 when the layout cannot be
        /// resolved (for example on platforms exposing a single display).
        /// </summary>
        public static int GetActiveDisplayIndex()
        {
            DisplayInfo active = Screen.mainWindowDisplayInfo;
            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);

            for (int i = 0; i < layout.Count; i++)
            {
                if (layout[i].Equals(active))
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
