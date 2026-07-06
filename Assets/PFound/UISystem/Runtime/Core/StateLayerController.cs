using PFound.UISystem.Components.M3;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Manages M3-style interaction state feedback for a target VisualElement.
    /// Drives hover (0.08), pressed (0.10), and focused (0.10) state overlays
    /// by setting <see cref="M3Surface.StateOverlayOpacity"/> directly —
    /// which clips the overlay to the rounded rect boundary.
    ///
    /// Also applies the <c>.m3-disabled</c> USS class (opacity 0.38) when
    /// <see cref="Disabled"/> is true and blocks all pointer/focus events.
    ///
    /// Not a MonoBehaviour — construct it inline inside a VisualElement
    /// constructor and call <see cref="Attach"/> to wire the callbacks.
    ///
    /// Usage:
    ///   var ctrl = new StateLayerController(mySdfElement, myRipple);
    ///   ctrl.Attach();
    ///   // later:
    ///   ctrl.Detach();
    /// </summary>
    public class StateLayerController
    {
        // ------------------------------------------------------------------ //
        //  Constants                                                           //
        // ------------------------------------------------------------------ //
        // Slightly above M3 baseline so press feedback reads without the system
        // cursor change + click sound that web M3 relies on for "I clicked it".
        private const float OpacityHovered  = 0.10f;
        private const float OpacityPressed  = 0.20f;
        private const float OpacityFocused  = 0.12f;
        private const float OpacityIdle     = 0.00f;

        private const string DisabledClass  = "m3-disabled";

        // ------------------------------------------------------------------ //
        //  State                                                               //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _target;
        private readonly M3Surface     _sdfTarget;  // null if target is not M3Surface
        private readonly RippleElement _ripple;     // nullable

        private bool _isHovered;
        private bool _isPressed;
        private bool _isFocused;
        private bool _attached;
        private bool _disabled;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// When true: all pointer/focus events are ignored and the target element
        /// receives the <c>.m3-disabled</c> USS class (opacity 0.38).
        /// Setting back to false clears the class and re-enables events.
        /// </summary>
        public bool Disabled
        {
            get => _disabled;
            set
            {
                if (_disabled == value) return;
                _disabled = value;

                if (_disabled)
                {
                    _target.AddToClassList(DisabledClass);
                    // Reset visual state so overlay doesn't persist when re-enabled
                    _isHovered = false;
                    _isPressed = false;
                    _isFocused = false;
                    UpdateOverlay();
                }
                else
                {
                    _target.RemoveFromClassList(DisabledClass);
                }
            }
        }

        /// <summary>
        /// Tint color of the state overlay. Defaults to white.
        /// Assigned to <see cref="M3Surface.StateOverlayColor"/> if target is an M3Surface.
        /// </summary>
        public Color OverlayColor
        {
            get => _sdfTarget != null ? _sdfTarget.StateOverlayColor : Color.white;
            set { if (_sdfTarget != null) _sdfTarget.StateOverlayColor = value; }
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a StateLayerController for the given target element.
        /// </summary>
        /// <param name="target">The VisualElement that receives interaction events.
        /// If it is an <see cref="M3Surface"/>, overlay opacity is set directly.</param>
        /// <param name="ripple">Optional RippleElement child.
        /// <see cref="RippleElement.StartRipple"/> is called on PointerDown.</param>
        public StateLayerController(VisualElement target, RippleElement ripple = null)
        {
            _target    = target;
            _sdfTarget = target as M3Surface;
            _ripple    = ripple;
        }

        // ------------------------------------------------------------------ //
        //  Attach / Detach                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Registers all 6 pointer/focus event callbacks on the target element.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;

            _target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _target.RegisterCallback<FocusInEvent>(OnFocusIn);
            _target.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }

        /// <summary>
        /// Unregisters all 6 callbacks. Call before the target element is removed
        /// from the panel or when the owning component is disposed.
        /// </summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;

            _target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            _target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _target.UnregisterCallback<FocusInEvent>(OnFocusIn);
            _target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }

        // ------------------------------------------------------------------ //
        //  Event Handlers                                                      //
        // ------------------------------------------------------------------ //

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_disabled) return;
            _isHovered = true;
            UpdateOverlay();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_disabled) return;
            _isHovered = false;
            _isPressed = false;
            UpdateOverlay();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_disabled) return;
            _isPressed = true;
            UpdateOverlay();
            _ripple?.StartRipple(evt.localPosition);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_disabled) return;
            _isPressed = false;
            UpdateOverlay();
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (_disabled) return;
            _isFocused = true;
            UpdateOverlay();
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (_disabled) return;
            _isFocused = false;
            UpdateOverlay();
        }

        // ------------------------------------------------------------------ //
        //  Overlay Logic                                                       //
        // ------------------------------------------------------------------ //

        private void UpdateOverlay()
        {
            if (_sdfTarget == null) return;

            float opacity;
            if (_isPressed || _isFocused)
                opacity = OpacityPressed;
            else if (_isHovered)
                opacity = OpacityHovered;
            else
                opacity = OpacityIdle;

            _sdfTarget.StateOverlayOpacity = opacity;
        }
    }
}
