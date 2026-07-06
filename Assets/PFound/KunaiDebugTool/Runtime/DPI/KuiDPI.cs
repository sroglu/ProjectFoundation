using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// THE single source of overlay scale. Everything sizes through <see cref="Px"/> / <see cref="PxInt"/>,
    /// so no other file computes a scale factor — change the model here and it applies everywhere.
    ///
    /// Final scale = auto DPI scale × user multiplier:
    ///   • auto: <c>Screen.dpi / BaseDpi</c>, clamped. BaseDpi is the Android dp baseline (160), not 96 —
    ///     96 made a 440-dpi phone ~4.6× (clamped 4×), which filled the screen. 160 + a 2.5 cap keeps it readable.
    ///   • user: <see cref="KuiSettings.UserScale"/>, driven live by the in-overlay scale control via
    ///     <see cref="SetUserScale"/>. The control only stores+recomputes here; it does no math itself.
    /// </summary>
    internal static class KuiDPI
    {
        const float BaseDpi      = 160f;
        const float MinAutoScale = 0.5f;
        const float MaxAutoScale = 2.5f;
        public const float MinUserScale = 0.5f;
        public const float MaxUserScale = 2.0f;

        static float _scale = 1f;   // final = auto × user

        public static float Scale => _scale;

        public static void UpdateScale(KuiSettings settings)
        {
            float auto = settings.DpiScaleOverride.HasValue
                ? settings.DpiScaleOverride.Value
                : Mathf.Clamp(AutoDpi() / BaseDpi, MinAutoScale, MaxAutoScale);

            float user = Mathf.Clamp(settings.UserScale, MinUserScale, MaxUserScale);
            _scale = auto * user;
        }

        /// <summary>Set the user multiplier (clamped) and recompute — the scale control's only entry point.</summary>
        public static void SetUserScale(KuiSettings settings, float userScale)
        {
            settings.UserScale = Mathf.Clamp(userScale, MinUserScale, MaxUserScale);
            UpdateScale(settings);
        }

        static float AutoDpi()
        {
            float dpi = Screen.dpi;
            return dpi > 0f ? dpi : BaseDpi;
        }

        public static float Px(float baseValue) => baseValue * _scale;

        public static int PxInt(float baseValue) => Mathf.RoundToInt(baseValue * _scale);
    }
}
