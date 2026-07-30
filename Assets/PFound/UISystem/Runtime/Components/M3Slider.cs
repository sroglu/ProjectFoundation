using System;
using PFound.UISystem.Components.M3;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Slider component (2024 spec).
    ///
    /// M3 Spec (updated):
    ///   Track: 16dp height, 8dp corner radius (trackHeight/2)
    ///   Thumb: 4dp width x 44dp height, 2dp corner radius (vertical capsule)
    ///   Gap: 6dp between thumb and track edges
    ///   Inside corner: 2dp (track corners near the gap)
    ///   Stop indicators: 4dp dots for discrete/stepped sliders
    ///   Active track: --m3-primary
    ///   Inactive track: --m3-surface-variant
    ///   Thumb: --m3-primary
    ///   Stop indicators on active: --m3-on-primary, on inactive: --m3-on-surface-variant
    /// </summary>
    [UxmlElement]
    public partial class M3Slider : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass           = "m3-slider";
        private const string TrackWrapClass      = "m3-slider__track-wrap";
        private const string TrackActiveClass    = "m3-slider__track-active";
        private const string TrackInactiveClass  = "m3-slider__track-inactive";
        private const string ThumbClass          = "m3-slider__thumb";
        private const string ValueLabelClass     = "m3-slider__value-label";

        // ------------------------------------------------------------------ //
        //  Dimensions (M3 2024 spec)                                           //
        // ------------------------------------------------------------------ //
        private const float TrackHeight       = 16f;
        private const float TrackRadius       = 8f;   // trackHeight / 2
        private const float InsideCorner      = 2f;   // track corner near gap
        private const float ThumbWidth        = 4f;
        private const float ThumbHeight       = 44f;
        private const float ThumbRadius       = 2f;
        private const float ThumbTrackGap     = 6f;
        private const float TouchTarget       = 48f;
        private const float StopIndicatorSize = 4f;

        // Resolved theme colors
        private Color _themePrimary;
        private Color _themeSurfaceVariant;
        private Color _themeOnSurface;

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement        _trackWrap;
        private readonly M3Surface       _trackActive;
        private readonly M3Surface       _trackInactive;
        private readonly VisualElement        _thumb;
        private readonly VisualElement        _stopIndicatorContainer;
        private readonly Label                _valueLabel;
        private readonly RippleElement        _ripple;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private float _value    = 0f;
        private float _min      = 0f;
        private float _max      = 1f;
        private float _step     = 0f;   // 0 = continuous
        private bool  _showValueLabel = false;
        private bool  _dragging = false;
        private int   _pointerId = -1;
        private float _dragStartValue;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when value changes during drag and on pointer release.</summary>
        public event Action<float> OnValueChanged;

        [UxmlAttribute("value")]
        public float Value
        {
            get => _value;
            set
            {
                float clamped = Mathf.Clamp(value, _min, _max);
                if (_step > 0)
                    clamped = Mathf.Round((clamped - _min) / _step) * _step + _min;
                if (Mathf.Approximately(_value, clamped)) return;
                _value = clamped;
                UpdateTrackLayout();
                OnValueChanged?.Invoke(_value);
            }
        }

        [UxmlAttribute("min")]
        public float Min
        {
            get => _min;
            set { _min = value; Value = _value; }
        }

        [UxmlAttribute("max")]
        public float Max
        {
            get => _max;
            set { _max = value; Value = _value; }
        }

        [UxmlAttribute("step")]
        public float Step
        {
            get => _step;
            set { _step = value; RebuildStopIndicators(); Value = _value; }
        }

        [UxmlAttribute("show-value-label")]
        public bool ShowValueLabel
        {
            get => _showValueLabel;
            set
            {
                _showValueLabel = value;
                _valueLabel.style.display = value && _dragging ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        [UxmlAttribute("disabled")]
        public new bool Disabled
        {
            get => base.Disabled;
            set
            {
                base.Disabled = value;
                UpdateTrackLayout();
            }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Slider()
        {
            AddToClassList(BaseClass);
            pickingMode  = PickingMode.Position;
            style.height = TouchTarget;
            style.flexDirection = FlexDirection.Row;
            style.alignItems    = Align.Center;

            // --- Track wrap ---
            _trackWrap = new VisualElement();
            _trackWrap.AddToClassList(TrackWrapClass);
            _trackWrap.style.flexGrow   = 1;
            _trackWrap.style.height     = TouchTarget;
            _trackWrap.style.position   = Position.Relative;
            _trackWrap.style.alignItems = Align.Center;
            _trackWrap.pickingMode      = PickingMode.Position;

            // --- Active track ---
            _trackActive = new M3Surface { pickingMode = PickingMode.Ignore };
            _trackActive.AddToClassList(TrackActiveClass);
            _trackActive.style.height   = TrackHeight;
            _trackActive.style.position = Position.Absolute;
            _trackActive.style.left     = 0;
            SetCornerRadius(_trackActive, TrackRadius);

            // --- Inactive track ---
            _trackInactive = new M3Surface { pickingMode = PickingMode.Ignore };
            _trackInactive.AddToClassList(TrackInactiveClass);
            _trackInactive.style.height   = TrackHeight;
            _trackInactive.style.position = Position.Absolute;
            _trackInactive.style.right    = 0;
            SetCornerRadius(_trackInactive, TrackRadius);

            // --- Thumb (vertical capsule) ---
            _thumb = new VisualElement();
            _thumb.AddToClassList(ThumbClass);
            _thumb.style.width    = ThumbWidth;
            _thumb.style.height   = ThumbHeight;
            _thumb.style.position = Position.Absolute;
            _thumb.pickingMode   = PickingMode.Position;

            // Thumb visual (SDFRect for rounded capsule)
            var thumbVisual = new M3Surface { CornerRadius = ThumbRadius, pickingMode = PickingMode.Ignore };
            thumbVisual.style.width  = ThumbWidth;
            thumbVisual.style.height = ThumbHeight;
            thumbVisual.style.borderTopLeftRadius     = ThumbRadius;
            thumbVisual.style.borderTopRightRadius    = ThumbRadius;
            thumbVisual.style.borderBottomLeftRadius  = ThumbRadius;
            thumbVisual.style.borderBottomRightRadius = ThumbRadius;
            _thumb.Add(thumbVisual);

            // --- Ripple + StateLayer on thumb ---
            _ripple = new RippleElement();
            _thumb.Add(_ripple);
            InitStateLayer(_thumb, _ripple);

            // --- Stop indicator container (for discrete sliders) ---
            _stopIndicatorContainer = new VisualElement();
            _stopIndicatorContainer.style.position = Position.Absolute;
            _stopIndicatorContainer.style.left   = 0;
            _stopIndicatorContainer.style.right  = 0;
            _stopIndicatorContainer.style.height = TrackHeight;
            _stopIndicatorContainer.pickingMode  = PickingMode.Ignore;

            // --- Value label ---
            _valueLabel = new M3Label();
            _valueLabel.AddToClassList("m3-label");
            _valueLabel.AddToClassList(ValueLabelClass);
            _valueLabel.style.display    = DisplayStyle.None;
            _valueLabel.style.position   = Position.Absolute;
            _valueLabel.style.fontSize   = 12f;
            _valueLabel.pickingMode      = PickingMode.Ignore;

            _trackWrap.Add(_trackActive);
            _trackWrap.Add(_trackInactive);
            _trackWrap.Add(_stopIndicatorContainer);
            _trackWrap.Add(_thumb);
            _trackWrap.Add(_valueLabel);

            Add(_trackWrap);

            // --- Pointer events for drag ---
            _trackWrap.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _trackWrap.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _trackWrap.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _trackWrap.RegisterCallback<PointerCancelEvent>(OnPointerCancel);

        }

        // ------------------------------------------------------------------ //
        //  Helper                                                              //
        // ------------------------------------------------------------------ //

        private static void SetCornerRadius(M3Surface el, float r)
        {
            el.CornerRadius = r;
            el.style.borderTopLeftRadius     = r;
            el.style.borderTopRightRadius    = r;
            el.style.borderBottomLeftRadius  = r;
            el.style.borderBottomRightRadius = r;
        }

        // ------------------------------------------------------------------ //
        //  Track Layout                                                        //
        // ------------------------------------------------------------------ //

        private void UpdateTrackLayout()
        {
            float trackWidth = _trackWrap.resolvedStyle.width;
            if (trackWidth <= 0) return;

            float range = _max - _min;
            float t = range > 0 ? (_value - _min) / range : 0f;

            // Thumb center position (padded so thumb doesn't exceed track bounds)
            float padding = TrackHeight / 2f; // half-capsule at track ends
            float thumbCenter = Mathf.Lerp(padding, trackWidth - padding, t);

            // Gap: 6dp on each side of thumb
            float halfThumb = ThumbWidth / 2f;
            float gapLeft  = thumbCenter - halfThumb - ThumbTrackGap;
            float gapRight = thumbCenter + halfThumb + ThumbTrackGap;

            // Active track: from 0 to gapLeft
            float activeWidth = Mathf.Max(0, gapLeft);
            _trackActive.style.width = activeWidth;
            _trackActive.style.left  = 0;
            // Left side: full round, right side (near gap): inside corner
            _trackActive.CornerRadiusTL = TrackRadius;
            _trackActive.CornerRadiusBL = TrackRadius;
            _trackActive.CornerRadiusTR = activeWidth > InsideCorner ? InsideCorner : 0;
            _trackActive.CornerRadiusBR = activeWidth > InsideCorner ? InsideCorner : 0;
            _trackActive.style.borderTopLeftRadius     = TrackRadius;
            _trackActive.style.borderBottomLeftRadius   = TrackRadius;
            _trackActive.style.borderTopRightRadius    = activeWidth > InsideCorner ? InsideCorner : 0;
            _trackActive.style.borderBottomRightRadius = activeWidth > InsideCorner ? InsideCorner : 0;

            // Inactive track: from gapRight to end
            float inactiveWidth = Mathf.Max(0, trackWidth - gapRight);
            _trackInactive.style.width = inactiveWidth;
            _trackInactive.style.right = 0;
            // Left side (near gap): inside corner, right side: full round
            _trackInactive.CornerRadiusTL = inactiveWidth > InsideCorner ? InsideCorner : 0;
            _trackInactive.CornerRadiusBL = inactiveWidth > InsideCorner ? InsideCorner : 0;
            _trackInactive.CornerRadiusTR = TrackRadius;
            _trackInactive.CornerRadiusBR = TrackRadius;
            _trackInactive.style.borderTopLeftRadius     = inactiveWidth > InsideCorner ? InsideCorner : 0;
            _trackInactive.style.borderBottomLeftRadius   = inactiveWidth > InsideCorner ? InsideCorner : 0;
            _trackInactive.style.borderTopRightRadius    = TrackRadius;
            _trackInactive.style.borderBottomRightRadius = TrackRadius;

            // Thumb position
            _thumb.style.left = thumbCenter - halfThumb;
            // Vertically center the thumb (it's taller than track)
            float trackTop = (TouchTarget - TrackHeight) / 2f;
            float thumbTop = (TouchTarget - ThumbHeight) / 2f;
            _thumb.style.top = thumbTop;
            _trackActive.style.top   = trackTop;
            _trackInactive.style.top = trackTop;
            _stopIndicatorContainer.style.top = trackTop;

            // Value label: centered above thumb using translate for width-independent centering
            if (_showValueLabel)
            {
                _valueLabel.text = _step > 0 ? _value.ToString("F0") : _value.ToString("F2");
                _valueLabel.style.left = thumbCenter;
                _valueLabel.style.top  = thumbTop - 28f;
                _valueLabel.style.translate = new Translate(Length.Percent(-50), 0);
            }

            // Colors — M3 spec: disabled uses onSurface blended with surface
            if (base.Disabled)
            {
                // Blend onSurface with surface at given ratio for opaque disabled colors
                Color s = _themeSurfaceVariant; // approximate surface
                Color o = _themeOnSurface;
                Color disabledActive   = new Color(
                    Mathf.Lerp(s.r, o.r, 0.38f),
                    Mathf.Lerp(s.g, o.g, 0.38f),
                    Mathf.Lerp(s.b, o.b, 0.38f), 1f);
                Color disabledInactive = new Color(
                    Mathf.Lerp(s.r, o.r, 0.12f),
                    Mathf.Lerp(s.g, o.g, 0.12f),
                    Mathf.Lerp(s.b, o.b, 0.12f), 1f);
                _trackActive.FillColorOverride   = disabledActive;
                _trackInactive.FillColorOverride = disabledInactive;
                if (_thumb.childCount > 0 && _thumb[0] is M3Surface dThumb)
                    dThumb.FillColorOverride = disabledActive;
            }
            else
            {
                _trackActive.FillColorOverride   = _themePrimary;
                _trackInactive.FillColorOverride = _themeSurfaceVariant;
                if (_thumb.childCount > 0 && _thumb[0] is M3Surface thumbVis)
                    thumbVis.FillColorOverride = _themePrimary;
            }
            StateLayer.OverlayColor = _themePrimary;

            // Update stop indicator colors
            UpdateStopIndicatorColors(thumbCenter);
        }

        // ------------------------------------------------------------------ //
        //  Stop Indicators (discrete/stepped sliders)                          //
        // ------------------------------------------------------------------ //

        private void RebuildStopIndicators()
        {
            _stopIndicatorContainer.Clear();
            if (_step <= 0) return;

            float range = _max - _min;
            if (range <= 0) return;

            int steps = Mathf.RoundToInt(range / _step);
            for (int i = 0; i <= steps; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("m3-slider__stop-dot");
                dot.style.width    = StopIndicatorSize;
                dot.style.height   = StopIndicatorSize;
                dot.style.position = Position.Absolute;
                dot.style.borderTopLeftRadius     = StopIndicatorSize / 2f;
                dot.style.borderTopRightRadius    = StopIndicatorSize / 2f;
                dot.style.borderBottomLeftRadius  = StopIndicatorSize / 2f;
                dot.style.borderBottomRightRadius = StopIndicatorSize / 2f;
                dot.pickingMode = PickingMode.Ignore;
                _stopIndicatorContainer.Add(dot);
            }

            // Position will be set in UpdateTrackLayout via UpdateStopIndicatorColors
        }

        private void UpdateStopIndicatorColors(float thumbCenter)
        {
            if (_step <= 0 || _stopIndicatorContainer.childCount == 0) return;

            float trackWidth = _trackWrap.resolvedStyle.width;
            if (trackWidth <= 0) return;

            float range = _max - _min;
            int steps = Mathf.RoundToInt(range / _step);
            float padding = TrackHeight / 2f;
            float halfThumb = ThumbWidth / 2f;

            for (int i = 0; i <= steps; i++)
            {
                if (i >= _stopIndicatorContainer.childCount) break;
                var dot = _stopIndicatorContainer[i];

                float stepT = steps > 0 ? (float)i / steps : 0f;
                float cx = Mathf.Lerp(padding, trackWidth - padding, stepT);

                dot.style.left = cx - StopIndicatorSize / 2f;
                dot.style.top  = (TrackHeight - StopIndicatorSize) / 2f;

                // Hide dot if it's under the thumb + gap area
                bool underThumb = Mathf.Abs(cx - thumbCenter) < (halfThumb + ThumbTrackGap + 1f);
                dot.style.display = underThumb ? DisplayStyle.None : DisplayStyle.Flex;

                // Color: on active track = onPrimary, on inactive = onSurfaceVariant (via USS classes)
                bool onActive = cx < thumbCenter;
                dot.EnableInClassList("m3-slider__stop-dot--active", onActive);
            }
        }

        // ------------------------------------------------------------------ //
        //  Theme-aware color resolution                                        //
        // ------------------------------------------------------------------ //

        protected override void RefreshThemeColors()
        {
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            _themePrimary        = theme.GetColor(ColorRole.Primary);
            _themeSurfaceVariant = theme.GetColor(ColorRole.SurfaceVariant);
            _themeOnSurface      = theme.GetColor(ColorRole.OnSurface);

            UpdateTrackLayout();
        }

        // ------------------------------------------------------------------ //
        //  Pointer Drag                                                        //
        // ------------------------------------------------------------------ //

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (base.Disabled) return;
            _dragging   = true;
            _pointerId  = evt.pointerId;
            _dragStartValue = _value;
            _trackWrap.CapturePointer(_pointerId);
            SetValueFromPointer(evt.localPosition.x);

            if (_showValueLabel)
                _valueLabel.style.display = DisplayStyle.Flex;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId) return;
            SetValueFromPointer(evt.localPosition.x);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId) return;
            EndDrag(evt.localPosition.x);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!_dragging) return;
            EndDrag(null);
        }

        private void EndDrag(float? localX)
        {
            _dragging = false;
            if (_trackWrap.HasPointerCapture(_pointerId))
                _trackWrap.ReleasePointer(_pointerId);
            _pointerId = -1;

            if (localX.HasValue)
                SetValueFromPointer(localX.Value);
            else
                Value = _dragStartValue; // Restore pre-drag value on cancel

            if (_showValueLabel)
                _valueLabel.style.display = DisplayStyle.None;
        }

        private void SetValueFromPointer(float localX)
        {
            float trackWidth = _trackWrap.resolvedStyle.width;
            if (trackWidth <= 0) return;

            float padding = TrackHeight / 2f;
            float usable = trackWidth - padding * 2f;
            float t = usable > 0 ? (localX - padding) / usable : 0f;
            t = Mathf.Clamp01(t);
            Value = _min + t * (_max - _min);
        }
    }
}
