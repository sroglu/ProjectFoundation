using System;
using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Snackbar component.
    ///
    /// Structure:
    ///   VisualElement (this)     — inverse-surface bg, 4dp corners
    ///   Label (_messageLabel)    — text (inverse-on-surface)
    ///   Label (_actionBtn)       — optional text action (inverse-primary)
    ///   Label (_closeIcon)       — optional close X (inverse-on-surface)
    ///
    /// M3 Spec:
    ///   Colors: inverse-surface bg, inverse-on-surface text, inverse-primary action
    ///   Corners: 4dp
    ///   Position: bottom-center, 8dp margin
    ///   Auto-dismiss: configurable duration
    ///
    /// All colors are driven by USS custom properties (--m3-inverse-*)
    /// which swap automatically when ThemeManager changes the theme stylesheet.
    ///
    /// USS: snackbar.uss
    ///
    /// Usage via M3SnackbarManager (preferred), or directly:
    ///   var sb = new M3Snackbar { Text = "Done", ActionText = "Undo" };
    ///   sb.Show(root);
    /// </summary>
    public class M3Snackbar : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass    = "m3-snackbar";
        private const string MsgClass     = "m3-snackbar__message";
        private const string ActionClass  = "m3-snackbar__action";
        private const string CloseClass   = "m3-snackbar__close";
        private const string CloseIcon    = "\ue5cd"; // Material Symbols: close

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly Label    _messageLabel;
        private readonly Label    _actionBtn;
        private readonly Label    _closeIcon;

        // ------------------------------------------------------------------ //
        //  Backing fields                                                      //
        // ------------------------------------------------------------------ //
        private string _text        = string.Empty;
        private string _actionText  = string.Empty;
        private bool   _showClose   = false;
        private int    _durationMs  = 4000;
        private IVisualElementScheduledItem _dismissSchedule;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        public event Action OnAction;
        public event Action OnDismissed;

        public string Text
        {
            get => _text;
            set { _text = value; _messageLabel.text = value ?? string.Empty; }
        }

        public string ActionText
        {
            get => _actionText;
            set
            {
                _actionText = value;
                _actionBtn.text = value ?? string.Empty;
                _actionBtn.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public bool ShowClose
        {
            get => _showClose;
            set
            {
                _showClose = value;
                _closeIcon.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public int DurationMs
        {
            get => _durationMs;
            set => _durationMs = value;
        }

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3Snackbar()
        {
            AddToClassList(BaseClass);
            pickingMode = PickingMode.Position;

            // --- Message label ---
            _messageLabel = new M3Label(string.Empty);
            _messageLabel.AddToClassList("m3-body");
            _messageLabel.AddToClassList(MsgClass);
            _messageLabel.style.flexGrow    = 1;
            _messageLabel.style.whiteSpace  = WhiteSpace.Normal;
            _messageLabel.pickingMode       = PickingMode.Ignore;
            Add(_messageLabel);

            // --- Action button (plain Label — colors driven by USS .m3-snackbar__action) ---
            _actionBtn = new M3Label(string.Empty);
            _actionBtn.AddToClassList(ActionClass);
            _actionBtn.style.fontSize = 14f;
            _actionBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _actionBtn.style.display     = DisplayStyle.None;
            _actionBtn.style.paddingLeft   = 12f;
            _actionBtn.style.paddingRight  = 12f;
            _actionBtn.style.paddingTop    = 8f;
            _actionBtn.style.paddingBottom = 8f;
            _actionBtn.pickingMode = PickingMode.Position;
            _actionBtn.RegisterCallback<ClickEvent>(_ =>
            {
                OnAction?.Invoke();
                Dismiss();
            });
            Add(_actionBtn);

            // --- Close icon ---
            _closeIcon = new M3Label(CloseIcon);
            _closeIcon.AddToClassList("m3-icon");
            _closeIcon.AddToClassList(CloseClass);
            _closeIcon.style.display     = DisplayStyle.None;
            _closeIcon.pickingMode       = PickingMode.Position;
            _closeIcon.RegisterCallback<ClickEvent>(_ => Dismiss());
            Add(_closeIcon);
        }

        // ------------------------------------------------------------------ //
        //  Show / Dismiss                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Show the snackbar inside the given parent element.</summary>
        public void Show(VisualElement parent)
        {
            if (this.parent != null)
                RemoveFromHierarchy();

            parent.Add(this);

            // Cancel any previous auto-dismiss
            _dismissSchedule?.Pause();
            if (_durationMs > 0)
                _dismissSchedule = schedule.Execute(Dismiss).StartingIn(_durationMs);
        }


        /// <summary>Dismiss and fire OnDismissed.</summary>
        public void Dismiss()
        {
            _dismissSchedule?.Pause();
            _dismissSchedule = null;
            RemoveFromHierarchy();
            OnDismissed?.Invoke();
        }
    }
}
