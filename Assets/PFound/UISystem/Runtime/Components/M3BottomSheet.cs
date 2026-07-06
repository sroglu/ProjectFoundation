using System;
using PFound.UISystem.Core;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Modal Bottom Sheet — slides up from the bottom of the screen.
    ///
    /// Composition:
    ///   VisualElement (_scrim) — dimming overlay, tap to dismiss
    ///   VisualElement (_sheet) — sheet surface
    ///     VisualElement (_dragHandle) — 32×4dp drag indicator
    ///     VisualElement (_content) — slot for consumer content
    ///
    /// M3 spec:
    ///   Background: --m3-surface-container-low
    ///   Top corners: 28dp radius
    ///   Drag handle: 32×4dp, --m3-on-surface-variant, 2dp radius
    ///
    /// USS: bottom-sheet.uss. Colors via var(--m3-*) tokens.
    ///
    /// Usage:
    ///   var sheet = new M3BottomSheet();
    ///   sheet.ContentContainer.Add(myContent);
    ///   sheet.Show(panelRoot);
    /// </summary>
    public class M3BottomSheet : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass       = "m3-bottom-sheet";
        private const string OpenClass       = "m3-bottom-sheet--open";
        private const string ScrimClass      = "m3-bottom-sheet__scrim";
        private const string SheetClass      = "m3-bottom-sheet__sheet";
        private const string HandleClass     = "m3-bottom-sheet__handle";
        private const string ContentClass    = "m3-bottom-sheet__content";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _scrim;
        private readonly VisualElement _sheet;
        private readonly VisualElement _dragHandle;
        private readonly VisualElement _content;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Fired when the bottom sheet is dismissed.</summary>
        public event Action OnDismissed;

        /// <summary>Container to add content into the sheet.</summary>
        public VisualElement ContentContainer => _content;

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3BottomSheet()
        {
            AddToClassList(BaseClass);
            style.position = Position.Absolute;
            style.left   = 0;
            style.top    = 0;
            style.right  = 0;
            style.bottom = 0;
            style.display = DisplayStyle.None;

            // Scrim
            _scrim = new VisualElement();
            _scrim.AddToClassList(ScrimClass);
            _scrim.RegisterCallback<ClickEvent>(_ => Dismiss());

            // Sheet
            _sheet = new VisualElement();
            _sheet.AddToClassList(SheetClass);

            _dragHandle = new VisualElement();
            _dragHandle.AddToClassList(HandleClass);

            _content = new VisualElement();
            _content.AddToClassList(ContentClass);

            _sheet.Add(_dragHandle);
            _sheet.Add(_content);

            Add(_scrim);
            Add(_sheet);
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Shows the bottom sheet as a child of the given parent element.</summary>
        public void Show(VisualElement parent)
        {
            parent.Add(this);
            style.display = DisplayStyle.Flex;
            AddToClassList(OpenClass);
        }

        /// <summary>Dismisses and removes the bottom sheet from its parent.</summary>
        public void Dismiss()
        {
            RemoveFromClassList(OpenClass);
            style.display = DisplayStyle.None;
            RemoveFromHierarchy();
            OnDismissed?.Invoke();
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }
    }
}
