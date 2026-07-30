using PFound.UISystem.Data;
using PFound.UISystem.Enums;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PFound.UISystem
{
    /// <summary>
    /// ScriptableObject that maps each TextRole to a concrete TextStyle (USS class name,
    /// size, spacing). Assign as the TypographyConfig on ThemeManager.
    /// TypographyResolver reads this to apply the matching USS class to VisualElements.
    /// </summary>
    [CreateAssetMenu(menuName = "UISystem/Typography Config", fileName = "TypographyConfig")]
    public class TypographyConfig : ScriptableObject
    {
        // ------------------------------------------------------------------ //
        //  Display                                                             //
        // ------------------------------------------------------------------ //
        [BoxGroup("Display")]
        [Tooltip("57sp — hero text, splash screens (Regular weight).")]
        [SerializeField] private TextStyle _displayLarge;

        [BoxGroup("Display")]
        [Tooltip("45sp — large promotional text (Regular weight).")]
        [SerializeField] private TextStyle _displayMedium;

        [BoxGroup("Display")]
        [Tooltip("36sp — prominent display text (Regular weight).")]
        [SerializeField] private TextStyle _displaySmall;

        // ------------------------------------------------------------------ //
        //  Headline                                                            //
        // ------------------------------------------------------------------ //
        [BoxGroup("Headline")]
        [Tooltip("32sp — page headings (Regular weight).")]
        [SerializeField] private TextStyle _headlineLarge;

        [BoxGroup("Headline")]
        [Tooltip("28sp — section headings (Regular weight).")]
        [SerializeField] private TextStyle _headlineMedium;

        [BoxGroup("Headline")]
        [Tooltip("24sp — sub-section headings (Regular weight).")]
        [SerializeField] private TextStyle _headlineSmall;

        // ------------------------------------------------------------------ //
        //  Title                                                               //
        // ------------------------------------------------------------------ //
        [BoxGroup("Title")]
        [Tooltip("22sp — card and dialog titles (Regular weight).")]
        [SerializeField] private TextStyle _titleLarge;

        [BoxGroup("Title")]
        [Tooltip("16sp — list titles (Medium weight).")]
        [SerializeField] private TextStyle _titleMedium;

        [BoxGroup("Title")]
        [Tooltip("14sp — component titles (Medium weight).")]
        [SerializeField] private TextStyle _titleSmall;

        // ------------------------------------------------------------------ //
        //  Body                                                                //
        // ------------------------------------------------------------------ //
        [BoxGroup("Body")]
        [Tooltip("16sp — primary body text (Regular weight).")]
        [SerializeField] private TextStyle _bodyLarge;

        [BoxGroup("Body")]
        [Tooltip("14sp — secondary body text (Regular weight).")]
        [SerializeField] private TextStyle _bodyMedium;

        [BoxGroup("Body")]
        [Tooltip("12sp — helper text, captions (Regular weight).")]
        [SerializeField] private TextStyle _bodySmall;

        // ------------------------------------------------------------------ //
        //  Label                                                               //
        // ------------------------------------------------------------------ //
        [BoxGroup("Label")]
        [Tooltip("14sp — button labels (Medium weight).")]
        [SerializeField] private TextStyle _labelLarge;

        [BoxGroup("Label")]
        [Tooltip("12sp — chip labels, tab labels (Medium weight).")]
        [SerializeField] private TextStyle _labelMedium;

        [BoxGroup("Label")]
        [Tooltip("11sp — overlines, timestamps (Medium weight).")]
        [SerializeField] private TextStyle _labelSmall;

        // ------------------------------------------------------------------ //
        //  API                                                                 //
        // ------------------------------------------------------------------ //
        /// <summary>Returns the TextStyle for the given role.</summary>
        public TextStyle GetStyle(TextRole role) => role switch
        {
            TextRole.DisplayLarge   => _displayLarge,
            TextRole.DisplayMedium  => _displayMedium,
            TextRole.DisplaySmall   => _displaySmall,
            TextRole.HeadlineLarge  => _headlineLarge,
            TextRole.HeadlineMedium => _headlineMedium,
            TextRole.HeadlineSmall  => _headlineSmall,
            TextRole.TitleLarge     => _titleLarge,
            TextRole.TitleMedium    => _titleMedium,
            TextRole.TitleSmall     => _titleSmall,
            TextRole.BodyLarge      => _bodyLarge,
            TextRole.BodyMedium     => _bodyMedium,
            TextRole.BodySmall      => _bodySmall,
            TextRole.LabelLarge     => _labelLarge,
            TextRole.LabelMedium    => _labelMedium,
            TextRole.LabelSmall     => _labelSmall,
            _                       => _bodyLarge
        };
    }
}
