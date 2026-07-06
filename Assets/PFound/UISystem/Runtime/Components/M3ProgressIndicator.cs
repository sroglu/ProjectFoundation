using System;
using PFound.UISystem.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Progress Indicator — linear and circular variants.
    ///
    /// Composition (Linear):
    ///   VisualElement (this) — full-width track container
    ///   VisualElement (_track) — background track element
    ///   VisualElement (_indicator) — animated progress fill
    ///
    /// Composition (Circular):
    ///   VisualElement (this) — sizing wrapper
    ///   VisualElement (_circleContainer) — vector callback host for arc drawing
    ///
    /// M3 spec:
    ///   Linear: 4dp height, full-width, rounded ends
    ///   Circular: 48dp default, 4dp stroke width
    ///   Determinate only: progress 0–1
    ///   Colors: track=--m3-secondary-container, indicator=--m3-primary
    ///
    /// USS: progress-indicator.uss. All colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3ProgressIndicator : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass             = "m3-progress";
        private const string LinearClass           = "m3-progress--linear";
        private const string CircularClass         = "m3-progress--circular";
        private const string TrackClass            = "m3-progress__track";
        private const string IndicatorClass        = "m3-progress__indicator";
        private const string CircleContainerClass  = "m3-progress__circle";

        // M3 spec dimensions
        private const float LinearHeight    = 4f;
        private const float CircularSize    = 48f;
        private const float StrokeWidth     = 4f;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private VisualElement _track;
        private VisualElement _indicator;
        private VisualElement _circleContainer;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private ProgressVariant _variant   = ProgressVariant.Linear;
        private float           _progress;                 // 0–1

        // Cached theme colors (used for the arc draw callback)
        private Color _themePrimary            = new Color(0.404f, 0.314f, 0.643f);
        private Color _themeSecondaryContainer = new Color(0.878f, 0.843f, 0.961f);

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public enum ProgressVariant { Linear, Circular }

        /// <summary>Linear or Circular variant.</summary>
        [UxmlAttribute("variant")]
        public ProgressVariant Variant
        {
            get => _variant;
            set
            {
                if (_variant == value) return;
                _variant = value;
                RebuildLayout();
            }
        }

        /// <summary>Progress value 0–1.</summary>
        [UxmlAttribute("progress")]
        public float Progress
        {
            get => _progress;
            set
            {
                _progress = Mathf.Clamp01(value);
                ApplyProgress();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3ProgressIndicator()
        {
            AddToClassList(BaseClass);
            RebuildLayout();
        }

        // ------------------------------------------------------------------ //
        //  Layout                                                              //
        // ------------------------------------------------------------------ //

        private void RebuildLayout()
        {
            Clear();
            _track = _indicator = _circleContainer = null;

            if (_variant == ProgressVariant.Linear)
            {
                AddToClassList(LinearClass);
                RemoveFromClassList(CircularClass);
                BuildLinear();
            }
            else
            {
                AddToClassList(CircularClass);
                RemoveFromClassList(LinearClass);
                BuildCircular();
            }

            ApplyProgress();
        }

        private void BuildLinear()
        {
            _track = new VisualElement();
            _track.AddToClassList(TrackClass);

            _indicator = new VisualElement();
            _indicator.AddToClassList(IndicatorClass);
            _track.Add(_indicator);

            Add(_track);
        }

        private void BuildCircular()
        {
            _circleContainer = new VisualElement();
            _circleContainer.AddToClassList(CircleContainerClass);
            _circleContainer.generateVisualContent += DrawCircularProgress;
            Add(_circleContainer);
        }

        // ------------------------------------------------------------------ //
        //  Progress application                                               //
        // ------------------------------------------------------------------ //

        private void ApplyProgress()
        {
            if (_variant == ProgressVariant.Linear && _indicator != null)
            {
                _indicator.style.width = new StyleLength(new Length(_progress * 100f, LengthUnit.Percent));
            }
            else if (_variant == ProgressVariant.Circular && _circleContainer != null)
            {
                _circleContainer.MarkDirtyRepaint();
            }
        }

        // ------------------------------------------------------------------ //
        //  Circular vector drawing (icon escape hatch)                        //
        // ------------------------------------------------------------------ //

        private void DrawCircularProgress(MeshGenerationContext ctx)
        {
            float w = _circleContainer.layout.width;
            float h = _circleContainer.layout.height;
            if (w < 1f || h < 1f) return;

            var p      = ctx.painter2D;
            float cx   = w / 2f;
            float cy   = h / 2f;
            float r    = (Mathf.Min(w, h) - StrokeWidth) / 2f;

            // Track circle (background)
            p.strokeColor  = _themeSecondaryContainer;
            p.lineWidth    = StrokeWidth;
            p.lineCap      = LineCap.Butt;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, 0f, 360f);
            p.Stroke();

            // Progress arc (foreground)
            p.strokeColor = _themePrimary;
            p.lineWidth   = StrokeWidth;
            p.lineCap     = LineCap.Round;

            float startAngle = -90f; // start at top
            float sweepAngle = _progress * 360f;
            if (sweepAngle < 1f) return; // nothing to draw

            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, startAngle, startAngle + sweepAngle);
            p.Stroke();
        }

        // ------------------------------------------------------------------ //
        //  Theme                                                               //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimary            = theme.GetColor(Enums.ColorRole.Primary);
            _themeSecondaryContainer = theme.GetColor(Enums.ColorRole.SecondaryContainer);

            _circleContainer?.MarkDirtyRepaint();
        }
    }
}
