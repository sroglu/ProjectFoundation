using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components.M3
{
    /// <summary>
    /// A drop-in <see cref="Label"/> that resets its <c>style.unityMaterial</c> to the
    /// default UI Toolkit text material at construction.
    /// </summary>
    /// <remarks>
    /// When a <c>Label</c> lives inside an <c>SdfShape</c> / <c>M3Surface</c> ancestor,
    /// UIR propagates the parent's custom <c>unityMaterial</c> (the SDF shape shader) to
    /// its descendants — so the label's glyph mesh ends up rendered through the SDF
    /// shader and comes out as a "/_ /_" stripe. Calling <c>StyleKeyword.Initial</c>
    /// on the label opts out of that inheritance and routes the text back through the
    /// default UI text material.
    ///
    /// Use this anywhere an M3 component places a label as descendant of an
    /// <c>SdfShape</c> root. Identical behaviour to <see cref="Label"/> otherwise.
    /// </remarks>
    [UxmlElement]
    public partial class M3Label : Label
    {
        public M3Label() { Init(); }
        public M3Label(string text) : base(text) { Init(); }

        private void Init()
        {
            style.unityMaterial = new StyleMaterialDefinition { keyword = StyleKeyword.Initial };
        }

        // ------------------------------------------------------------------ //
        //  MaterialSymbols icon font                                           //
        // ------------------------------------------------------------------ //
        //
        // USS-driven font (var(--m3-icon-font), .m3-icon rule) doesn't reach
        // descendants of an overlay popped to panel.visualTree (the same scope
        // quirk that affects layout / colour USS rules on popped menus / dialogs).
        // Components that render icon glyphs in those contexts (M3MenuItem,
        // M3DatePicker nav arrows, …) pin the font in C# via this helper.

        private static Font _materialSymbolsFont;

        public static void ApplyMaterialSymbolsFont(Label label)
        {
            if (_materialSymbolsFont == null)
                _materialSymbolsFont = Resources.Load<Font>("UISystem/MaterialSymbols-Filled");
            if (_materialSymbolsFont != null)
                label.style.unityFontDefinition = new StyleFontDefinition(_materialSymbolsFont);
        }
    }
}
