using PFound.UISystem.Core;
using UnityEngine.UIElements;
using PFound.UISystem.Components.M3;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// M3-style Bottom App Bar — bottom action bar with FAB slot and icon buttons.
    ///
    /// Composition:
    ///   M3Surface (this) — elevated bar surface (elevation 2)
    ///   VisualElement (_iconsArea) — left icon buttons slot
    ///   VisualElement (_fabSlot) — right FAB slot
    ///
    /// M3 spec:
    ///   Height: 80dp
    ///   Background: --m3-surface-container
    ///   Elevation: level 2
    ///   FAB: right-aligned, optional
    ///
    /// USS: bottom-app-bar.uss. Colors via var(--m3-*) tokens.
    /// </summary>
    [UxmlElement]
    public partial class M3BottomAppBar : M3ComponentBase
    {
        // ------------------------------------------------------------------ //
        //  USS class constants                                                 //
        // ------------------------------------------------------------------ //
        private const string BaseClass     = "m3-bottom-app-bar";
        private const string IconsClass    = "m3-bottom-app-bar__icons";
        private const string FabSlotClass  = "m3-bottom-app-bar__fab-slot";
        private const string IconClass     = "m3-bottom-app-bar__icon";

        // ------------------------------------------------------------------ //
        //  Children                                                            //
        // ------------------------------------------------------------------ //
        private readonly VisualElement _iconsArea;
        private readonly VisualElement _fabSlot;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Slot to add icon buttons (Label with m3-icon class).</summary>
        public VisualElement IconsArea => _iconsArea;

        /// <summary>Slot to add an M3FAB element.</summary>
        public VisualElement FabSlot => _fabSlot;

        // ------------------------------------------------------------------ //
        //  Constructor                                                         //
        // ------------------------------------------------------------------ //

        public M3BottomAppBar()
        {
            AddToClassList(BaseClass);

            _iconsArea = new VisualElement();
            _iconsArea.AddToClassList(IconsClass);

            _fabSlot = new VisualElement();
            _fabSlot.AddToClassList(FabSlotClass);

            Add(_iconsArea);
            Add(_fabSlot);
        }

        // ------------------------------------------------------------------ //
        //  Public helpers                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>Adds an icon button to the icons area. Returns the created Label.</summary>
        public Label AddIconButton(string iconCodepoint)
        {
            var icon = new M3Label(iconCodepoint);
            icon.AddToClassList("m3-icon");
            icon.AddToClassList(IconClass);
            _iconsArea.Add(icon);
            return icon;
        }

        protected override void RefreshThemeColors() { /* Colors via USS var(--m3-*) */ }
    }
}
