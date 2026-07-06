using UnityEngine.UIElements;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// <see cref="SdfPanel"/> that resolves its <see cref="SdfPanel.Material"/> from a theme-key
    /// string via <see cref="ActiveMaterialTheme"/>. Designer fills the theme manifest once
    /// (a <see cref="ThemeMaterialRegistry"/> ScriptableObject), then every panel using a
    /// matching key re-binds automatically when the active theme is swapped.
    /// </summary>
    /// <remarks>
    /// <para><b>Resolution order:</b> if <see cref="SdfPanel.Material"/> is set explicitly
    /// (UxmlAttribute or C# setter), that wins. If unset OR the user clears it, the panel falls
    /// back to <c>ActiveMaterialTheme.Get(ThemeKey)</c>. This lets one-off panels override
    /// the theme without forking the registry.</para>
    /// <para><b>Lifecycle:</b> subscribes to <see cref="ActiveMaterialTheme.OnChanged"/> on
    /// <see cref="AttachToPanelEvent"/>, unsubscribes on <see cref="DetachFromPanelEvent"/>.
    /// No subscription before attach to avoid leaked references in pooled / disposed panels.</para>
    /// </remarks>
    [UxmlElement]
    public partial class ThemedSdfPanel : SdfPanel
    {
        /// <summary>USS class added to every ThemedSdfPanel.</summary>
        public new static readonly string ussClassName = "themed-sdf-panel";

        private string _themeKey;
        private bool _explicitMaterial;

        /// <summary>
        /// Theme-key string looked up through <see cref="ActiveMaterialTheme"/>.
        /// Changing this triggers an immediate material rebind via the active registry.
        /// </summary>
        [UxmlAttribute("theme-key")]
        public string ThemeKey
        {
            get => _themeKey;
            set
            {
                if (_themeKey == value) return;
                _themeKey = value;
                ResolveFromTheme();
            }
        }

        public ThemedSdfPanel()
        {
            AddToClassList(ussClassName);
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            ActiveMaterialTheme.OnChanged += OnActiveThemeChanged;
            ResolveFromTheme();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            ActiveMaterialTheme.OnChanged -= OnActiveThemeChanged;
        }

        private void OnActiveThemeChanged(ThemeMaterialRegistry _) => ResolveFromTheme();

        private void ResolveFromTheme()
        {
            // Explicit material wins; only override when no manual material is set.
            if (_explicitMaterial) return;
            if (string.IsNullOrEmpty(_themeKey))
            {
                base.Material = null;
                return;
            }
            base.Material = ActiveMaterialTheme.Get(_themeKey);
        }
    }
}
