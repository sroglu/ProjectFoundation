using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Overlay VisualElement that renders an expanding ripple circle.
    /// Add as a child of any interactive element. Drive via StartRipple(localPosition).
    ///
    /// Typical usage:
    ///   var ripple = new RippleElement();
    ///   button.Add(ripple);
    ///   button.RegisterCallback<PointerDownEvent>(e => ripple.StartRipple(e.localPosition));
    /// </summary>
    public class RippleElement : VisualElement
    {
        private const float RippleDurationMs = 350f;
        private const float FadeOutDurationMs = 200f;

        private Vector2 _center;
        private float   _radius;      // 0–1 normalized to element diagonal
        private float   _alpha;
        private bool    _running;
        private float   _elapsedMs;
        private float   _fadeElapsedMs;

        private IVisualElementScheduledItem _expandTimer;
        private IVisualElementScheduledItem _fadeTimer;

        /// <summary>Tint color of the ripple. Defaults to white (matching M3 OnSurface).</summary>
        public Color RippleColor { get; set; } = Color.white;

        /// <summary>Peak opacity of the ripple overlay. M3 pressed state = 0.10.</summary>
        public float PeakOpacity { get; set; } = 0.10f;

        public RippleElement()
        {
            style.position = Position.Absolute;
            style.left     = 0;
            style.right    = 0;
            style.top      = 0;
            style.bottom   = 0;
            pickingMode    = PickingMode.Ignore; // don't block events
            // SdfShape ancestors (e.g. M3Surface inside M3Button) assign
            // style.unityMaterial = SDF material; UIR propagates that to descendants,
            // so the painter2D ripple mesh would be rendered through the SDF shader and
            // vanish. Reset back to the default UI material so the ripple paints.
            style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
            // overflow:hidden + per-instance border-radius lets the parent component
            // (e.g. M3Button) clip the painter2D circle to the rounded pill silhouette
            // instead of letting it spill into a halo around the cursor. The owner is
            // responsible for setting the border-radius via SetClipRadius below.
            style.overflow = Overflow.Hidden;
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>
        /// Set a uniform border-radius on the ripple so its overflow:hidden clip
        /// matches the owner component's silhouette. Owner components (M3Button,
        /// M3Chip, …) call this whenever their own corner radius changes.
        /// </summary>
        public void SetClipRadius(float radius)
        {
            style.borderTopLeftRadius     = radius;
            style.borderTopRightRadius    = radius;
            style.borderBottomLeftRadius  = radius;
            style.borderBottomRightRadius = radius;
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Start the ripple animation from the given local position.
        /// Cancels any in-progress ripple before starting a new one.
        /// </summary>
        public void StartRipple(Vector2 localPosition)
        {
            // Cancel any in-progress timers
            _expandTimer?.Pause();
            _fadeTimer?.Pause();
            _expandTimer = null;
            _fadeTimer   = null;

            _center        = localPosition;
            _radius        = 0f;
            _alpha         = PeakOpacity;
            _elapsedMs     = 0f;
            _fadeElapsedMs = 0f;
            _running       = true;

            _expandTimer = schedule.Execute(Tick).Every(16).Until(() => !_running);
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ //
        //  Animation                                                           //
        // ------------------------------------------------------------------ //
        private void Tick(TimerState ts)
        {
            if (!_running) return;

            _elapsedMs += ts.deltaTime;
            float t = Mathf.Clamp01(_elapsedMs / RippleDurationMs);

            // Ease-out expansion (M3 Emphasized easing)
            _radius = Mathf.Lerp(0f, 1f, 1f - Mathf.Pow(1f - t, 3f));

            if (_radius >= 0.99f)
            {
                _running = false;
                FadeOut();
            }

            MarkDirtyRepaint();
        }

        private void FadeOut()
        {
            _fadeElapsedMs = 0f;
            _fadeTimer = schedule.Execute(FadeTick).Every(16).Until(() => _alpha <= 0f);
        }

        private void FadeTick(TimerState ts)
        {
            _alpha -= (PeakOpacity / FadeOutDurationMs) * ts.deltaTime;
            if (_alpha < 0f) _alpha = 0f;
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ //
        //  Rendering                                                           //
        // ------------------------------------------------------------------ //
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_radius <= 0f || _alpha <= 0f) return;

            // M3 ripple grows from the click point to the FURTHEST corner of the
            // button — never beyond. Using diag/2 (the old code) lets the ripple
            // bleed past the button silhouette into the surrounding background.
            Rect rect = contentRect;
            float dx0 = _center.x, dx1 = rect.width  - _center.x;
            float dy0 = _center.y, dy1 = rect.height - _center.y;
            float fx  = Mathf.Max(dx0, dx1);
            float fy  = Mathf.Max(dy0, dy1);
            float maxR = Mathf.Sqrt(fx * fx + fy * fy);
            float radius = _radius * maxR;

            var painter = ctx.painter2D;
            var c       = RippleColor;
            c.a         = _alpha;
            painter.fillColor = c;
            painter.BeginPath();
            painter.Arc(_center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Fill(FillRule.OddEven);
        }
    }
}
