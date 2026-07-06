using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Toggle (Switch) component.
    ///
    /// Composition:
    ///   VisualElement (this)  — touch target wrapper (48dp min)
    ///   M3Surface (_track) — pill-shaped track (52×32dp, 16dp corners)
    ///   M3Surface (_thumb) — circular handle, animates position and size
    ///   RippleElement (_ripple) — press ripple on thumb area
    ///   StateLayerController (_stateLayer) — hover/press/focus feedback on thumb
    ///
    /// M3 Switch spec:
    ///   Track: 52×32dp, pill (border-radius: 16dp)
    ///   Thumb: 16dp (off) → 24dp (on), 28dp (pressed)
    ///   Track unselected: surface-container-highest + 2dp outline
    ///   Track selected: primary, no outline
    ///   Thumb unselected: outline color
    ///   Thumb selected: on-primary
    ///
    /// USS: toggle.uss. All colors via var(--m3-*) tokens.
    ///
    /// Usage (C#):
    ///   var toggle = new M3Toggle { Value = true };
    ///   toggle.OnValueChanged += val => Debug.Log(val);
    ///
    /// Usage (UXML):
    ///   &lt;components:M3Toggle value="true" /&gt;
    /// </summary>
    [UxmlElement]
    public partial class M3Toggle : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass      = "m3-toggle";
        private const string TrackClass     = "m3-toggle__track";
        private const string ThumbClass     = "m3-toggle__thumb";
        private const string CheckedClass   = "m3-toggle--checked";
        private const string UncheckedClass = "m3-toggle--unchecked";

        // Resolved theme colors (read from ThemeData via ThemeManager)
        private Color _themeOutline;
        private Color _themeOnSurface;
        private Color _themePrimary;
        private Color _themeOnPrimary;
        private Color _themeSurfaceContainerHighest;

        // ------------------------------------------------------------------ //
        //  Dimensions (M3 spec)                                                //
        // ------------------------------------------------------------------ //
        private const float TrackWidth      = 52f;
        private const float TrackHeight     = 32f;
        private const float TrackRadius     = 16f; // pill shape
        private const float ThumbSizeOff    = 16f;
        private const float ThumbSizeOn     = 24f;
        private const float ThumbSizePress  = 28f;
        private const float TrackOutline    = 2f;  // unselected track border

        // Thumb center positions (from left edge of track)
        // Unselected: track padding (TrackHeight/2) from left
        // Selected:   TrackWidth - TrackHeight/2 from left
        private const float ThumbOffX = TrackHeight / 2f;           // 16dp
        private const float ThumbOnX  = TrackWidth - TrackHeight / 2f; // 36dp

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly M3Surface  _track;
        private readonly M3Surface  _thumb;
        private readonly VisualElement   _checkIcon;
        private readonly RippleElement   _ripple;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private bool _value;
        private bool _pressed;
        private IVisualElementScheduledItem _anim;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when value changes. Arg is the new value.</summary>
        public event Action<bool> OnValueChanged;

        /// <summary>Current on/off state.</summary>
        [UxmlAttribute("value")]
        public bool Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                ApplyVisualState();
                OnValueChanged?.Invoke(_value);
            }
        }

        /// <summary>When true, dims the toggle and ignores input.</summary>
        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set => base.Disabled = value;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Toggle()
        {
            // Touch target wrapper
            AddToClassList(BaseClass);
            style.alignSelf = Align.FlexStart;
            pickingMode = PickingMode.Position;
            focusable = true;

            // --- Track ---
            _track = new M3Surface
            {
                CornerRadius = TrackRadius,
                pickingMode  = PickingMode.Position,
            };
            _track.AddToClassList(TrackClass);
            _track.style.width  = TrackWidth;
            _track.style.height = TrackHeight;
            _track.style.borderTopLeftRadius     = TrackRadius;
            _track.style.borderTopRightRadius    = TrackRadius;
            _track.style.borderBottomLeftRadius  = TrackRadius;
            _track.style.borderBottomRightRadius = TrackRadius;

            // --- Ripple (on thumb area) ---
            _ripple = new RippleElement();
            _track.Add(_ripple);

            // --- Thumb ---
            _thumb = new M3Surface
            {
                CornerRadius = ThumbSizeOn / 2f, // circular: radius = size/2
                pickingMode  = PickingMode.Ignore,
            };
            _thumb.AddToClassList(ThumbClass);
            _thumb.style.position       = Position.Absolute;
            _thumb.style.justifyContent = Justify.Center;
            _thumb.style.alignItems     = Align.Center;

            // Checkmark icon drawn via vector callback (visible when on, hidden when off)
            _checkIcon = new VisualElement();
            _checkIcon.style.position = Position.Absolute;
            _checkIcon.style.left     = 0;
            _checkIcon.style.right    = 0;
            _checkIcon.style.top      = 0;
            _checkIcon.style.bottom   = 0;
            _checkIcon.pickingMode    = PickingMode.Ignore;
            _checkIcon.style.opacity  = 0f;
            _checkIcon.generateVisualContent += ctx =>
            {
                float w = _checkIcon.layout.width;
                float h = _checkIcon.layout.height;
                if (w < 1f || h < 1f) return;

                var p = ctx.painter2D;
                // M3 checkmark: 3 points forming an L-shape tick
                // Normalized to element bounds, centered, ~60% of size
                float s  = Mathf.Min(w, h) * 0.55f;
                float ox = (w - s) / 2f;
                float oy = (h - s) / 2f;

                p.strokeColor = _value ? _themePrimary : Color.clear;
                p.lineWidth = Mathf.Max(2f, s * 0.15f);
                p.lineCap   = LineCap.Round;
                p.lineJoin  = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(ox + s * 0.15f, oy + s * 0.55f));
                p.LineTo(new Vector2(ox + s * 0.40f, oy + s * 0.80f));
                p.LineTo(new Vector2(ox + s * 0.85f, oy + s * 0.20f));
                p.Stroke();
            };
            _thumb.Add(_checkIcon);

            _track.Add(_thumb);

            // --- State layer on track (covers thumb area) ---
            InitStateLayer(_track, _ripple);

            // --- Events ---
            _track.RegisterCallback<ClickEvent>(OnTrackClicked);
            _track.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _track.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _track.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            Add(_track);
            ApplyVisualState(animate: false);
        }

        // ------------------------------------------------------------------ //
        //  Visual State                                                        //
        // ------------------------------------------------------------------ //

        private void ApplyVisualState(bool animate = true)
        {
            if (_value)
            {
                RemoveFromClassList(UncheckedClass);
                AddToClassList(CheckedClass);
                _track.RemoveFromClassList(UncheckedClass);
                _track.AddToClassList(CheckedClass);
            }
            else
            {
                RemoveFromClassList(CheckedClass);
                AddToClassList(UncheckedClass);
                _track.RemoveFromClassList(CheckedClass);
                _track.AddToClassList(UncheckedClass);
            }

            _track.OutlineColor = _themeOutline;

            // Target values
            float targetSize    = _pressed ? ThumbSizePress : (_value ? ThumbSizeOn : ThumbSizeOff);
            float targetX       = _value ? ThumbOnX : ThumbOffX;
            float targetLeft    = targetX - targetSize / 2f;
            var targetTrackCol  = _value ? _themePrimary : _themeSurfaceContainerHighest;
            var targetThumbCol  = _value ? _themeOnPrimary : _themeOutline;
            float targetCheck   = _value ? 1f : 0f;
            float targetOutline = _value ? 0f : TrackOutline;

            // State layer overlay color: on-surface for both states (per M3 spec)
            StateLayer.OverlayColor = _themeOnSurface;

            _anim?.Pause();
            _anim = null;

            // Snap palette-resolved colours immediately. Lerping them per-frame would
            // burn one new SdfShapePalette slot per intermediate colour (16-slot cap →
            // overflow after a handful of toggles). Geometry + outline thickness still
            // animate smoothly via the per-instance vertex stream.
            _track.FillColorOverride = targetTrackCol;
            _thumb.FillColorOverride = targetThumbCol;

            if (!animate || panel == null)
            {
                ApplyThumbValues(targetSize, targetLeft, targetCheck, targetOutline);
                return;
            }

            // Capture current values
            float curSize    = _thumb.resolvedStyle.width > 0 ? _thumb.resolvedStyle.width : targetSize;
            float curLeft    = _thumb.resolvedStyle.left;
            float curCheck   = _checkIcon.resolvedStyle.opacity;
            float curOutline = _track.OutlineThickness;

            _anim = M3Animate.Float(this, 0f, 1f, 200f, t =>
            {
                ApplyThumbValues(
                    Mathf.Lerp(curSize, targetSize, t),
                    Mathf.Lerp(curLeft, targetLeft, t),
                    Mathf.Lerp(curCheck, targetCheck, t),
                    Mathf.Lerp(curOutline, targetOutline, t));
            });
        }

        private void ApplyThumbValues(float size, float left, float checkOpacity, float outlineThickness)
        {
            float r = size / 2f;
            _track.OutlineThickness = outlineThickness;
            _thumb.style.width  = size;
            _thumb.style.height = size;
            _thumb.CornerRadius = r;
            _thumb.style.borderTopLeftRadius     = r;
            _thumb.style.borderTopRightRadius    = r;
            _thumb.style.borderBottomLeftRadius  = r;
            _thumb.style.borderBottomRightRadius = r;
            _thumb.style.left = left;
            _thumb.style.top  = (TrackHeight - size) / 2f;
            _checkIcon.style.opacity = checkOpacity;
            _checkIcon.MarkDirtyRepaint();
            _track.MarkDirtyRepaint();
            _thumb.MarkDirtyRepaint();
        }

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themeOutline   = theme.GetColor(Enums.ColorRole.Outline);
            _themeOnSurface = theme.GetColor(Enums.ColorRole.OnSurface);
            _themePrimary   = theme.GetColor(Enums.ColorRole.Primary);
            _themeOnPrimary = theme.GetColor(Enums.ColorRole.OnPrimary);
            _themeSurfaceContainerHighest = theme.GetColor(Enums.ColorRole.SurfaceContainerHighest);

            ApplyVisualState(animate: false);
            _checkIcon.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                      //
        // ------------------------------------------------------------------ //

        private void OnTrackClicked(ClickEvent evt)
        {
            if (base.Disabled) return;
            Value = !_value;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (base.Disabled) return;
            _pressed = true;
            ApplyThumbSize();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (base.Disabled) return;
            _pressed = false;
            ApplyThumbSize();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_pressed)
            {
                _pressed = false;
                ApplyThumbSize();
            }
        }

        private void ApplyThumbSize()
        {
            _anim?.Pause();
            _anim = null;

            float thumbSize = _pressed ? ThumbSizePress : (_value ? ThumbSizeOn : ThumbSizeOff);
            float thumbRadius = thumbSize / 2f;
            _thumb.style.width  = thumbSize;
            _thumb.style.height = thumbSize;
            _thumb.CornerRadius = thumbRadius;
            _thumb.style.borderTopLeftRadius     = thumbRadius;
            _thumb.style.borderTopRightRadius    = thumbRadius;
            _thumb.style.borderBottomLeftRadius  = thumbRadius;
            _thumb.style.borderBottomRightRadius = thumbRadius;

            float thumbX = _value ? ThumbOnX : ThumbOffX;
            _thumb.style.left = thumbX - thumbSize / 2f;
            _thumb.style.top  = (TrackHeight - thumbSize) / 2f;
        }
    }
}
