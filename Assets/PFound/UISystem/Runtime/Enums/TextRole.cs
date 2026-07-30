namespace PFound.UISystem.Enums
{
    public enum TextRole
    {
        // ── Full 15-role M3 scale ────────────────────────────────────────────
        DisplayLarge   = 0,
        DisplayMedium  = 1,
        DisplaySmall   = 2,

        HeadlineLarge  = 3,
        HeadlineMedium = 4,
        HeadlineSmall  = 5,

        TitleLarge     = 6,
        TitleMedium    = 7,
        TitleSmall     = 8,

        BodyLarge      = 9,
        BodyMedium     = 10,
        BodySmall      = 11,

        LabelLarge     = 12,
        LabelMedium    = 13,
        LabelSmall     = 14,

        // ── Backward-compatible aliases ─────────────────────────────────────
        Display  = DisplayLarge,
        Headline = HeadlineLarge,
        Title    = TitleLarge,
        Body     = BodyLarge,
        Label    = LabelLarge,
        Caption  = BodySmall,
    }
}
