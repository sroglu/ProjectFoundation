namespace PFound.UISystem.Utils
{
    /// <summary>
    /// Unicode codepoint constants for Material Symbols glyphs.
    ///
    /// Use with the m3-icon or m3-icon-outlined USS class:
    ///   label.AddToClassList("m3-icon");
    ///   label.text = MaterialSymbols.Add;
    ///
    /// Full codepoint list: Assets/Typography/Fonts/MaterialSymbols/codepoints
    /// Material Symbols reference: https://fonts.google.com/icons
    /// </summary>
    public static class MaterialSymbols
    {
        // ── Navigation ────────────────────────────────────────────────────────
        public const string ArrowBack        = "\ue5c4";
        public const string ArrowForward     = "\ue5c8";
        public const string ArrowDropDown    = "\ue5c5";
        public const string ArrowDropUp      = "\ue5c7";
        public const string ChevronLeft      = "\ue5cb";
        public const string ChevronRight     = "\ue5cc";
        public const string Close            = "\ue5cd";
        public const string ExpandMore       = "\ue5cf";
        public const string ExpandLess       = "\ue5ce";
        public const string Menu             = "\ue5d2";
        public const string MoreVert         = "\ue5d4";
        public const string MoreHoriz        = "\ue5d3";

        // ── Action ────────────────────────────────────────────────────────────
        public const string Add              = "\ue145";
        public const string Remove           = "\ue15b";
        public const string Edit             = "\ue3c9";
        public const string Delete           = "\ue872";
        public const string Search           = "\ue8b6";
        public const string Settings         = "\ue8b8";
        public const string Share            = "\ue80d";
        public const string FilterList       = "\ue152";
        public const string Sort             = "\ue164";
        public const string Refresh          = "\ue5d5";
        public const string Done             = "\ue876";
        public const string Check            = "\ue5ca";
        public const string Clear            = "\ue14c";

        // ── Content ───────────────────────────────────────────────────────────
        public const string ContentCopy      = "\ue14d";
        public const string ContentCut       = "\ue14e";
        public const string ContentPaste     = "\ue14f";

        // ── Communication ─────────────────────────────────────────────────────
        public const string Email            = "\ue0be";
        public const string Phone            = "\ue0cd";
        public const string Chat             = "\ue0b7";
        public const string Notifications    = "\ue7f4";

        // ── Social ────────────────────────────────────────────────────────────
        public const string Person           = "\ue7fd";
        public const string Group            = "\ue7ef";
        public const string Favorite         = "\ue87d";
        public const string FavoriteBorder   = "\ue87e";
        public const string Star             = "\ue838";
        public const string StarBorder       = "\ue83a";

        // ── Media ─────────────────────────────────────────────────────────────
        public const string PlayArrow        = "\ue037";
        public const string Pause            = "\ue034";
        public const string Stop             = "\ue047";
        public const string SkipNext         = "\ue044";
        public const string SkipPrevious     = "\ue045";
        public const string VolumeUp         = "\ue050";
        public const string VolumeMute       = "\ue04e";

        // ── File & Folder ─────────────────────────────────────────────────────
        public const string Folder           = "\ue2c7";
        public const string FolderOpen       = "\ue2c8";
        public const string AttachFile       = "\ue226";
        public const string Download         = "\uf090";
        public const string Upload           = "\uf09b";

        // ── UI Components ─────────────────────────────────────────────────────
        public const string Info             = "\ue88e";
        public const string Warning          = "\ue002";
        public const string Error            = "\ue000";
        public const string HelpOutline      = "\ue8fd";
        public const string Visibility       = "\ue8f4";
        public const string VisibilityOff    = "\ue8f5";
        public const string Lock             = "\ue897";
        public const string LockOpen         = "\ue898";

        // ── FAB default icons ─────────────────────────────────────────────────
        public const string AddDefault       = Add;
        public const string EditDefault      = Edit;
    }
}
