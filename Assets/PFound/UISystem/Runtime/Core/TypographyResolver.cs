using PFound.UISystem.Enums;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Core
{
    /// <summary>
    /// Helper MonoBehaviour that maps a TextRole to the corresponding M3 typography
    /// USS class on a named VisualElement inside a UIDocument. For UI Toolkit layouts
    /// this is an optional convenience — you can also assign classes directly in UXML.
    ///
    /// Typical setup: Place on any GameObject in the scene. Assign the UIDocument,
    /// enter the element name (as defined in UXML), and choose the TextRole. The resolver
    /// applies the matching class (.m3-display through .m3-caption) in Awake.
    /// </summary>
    public class TypographyResolver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Serialized Fields                                                   //
        // ------------------------------------------------------------------ //
        [Tooltip("UIDocument that contains the target element.")]
        [Required]
        [SerializeField] private UIDocument _document;

        [Tooltip("The name attribute of the VisualElement to style (as set in UXML).")]
        [SerializeField] private string _elementName;

        [Tooltip("Semantic typography role to apply.")]
        [SerializeField] private TextRole _role = TextRole.Body;

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //
        /// <summary>Assigns a new role and immediately re-applies the USS class.</summary>
        public TextRole Role
        {
            get => _role;
            set { _role = value; ApplyRole(_role); }
        }

        /// <summary>
        /// Removes all .m3-* classes from the target element and adds the class
        /// corresponding to the given role. No-op if the element is not found.
        /// </summary>
        public void ApplyRole(TextRole role)
        {
            var element = FindElement();
            if (element == null)
            {
                Debug.LogWarning($"[UISystem] TypographyResolver: element '{_elementName}' not found in '{_document?.name}'.", this);
                return;
            }

            element.RemoveFromClassList("m3-display");
            element.RemoveFromClassList("m3-headline");
            element.RemoveFromClassList("m3-title");
            element.RemoveFromClassList("m3-body");
            element.RemoveFromClassList("m3-label");
            element.RemoveFromClassList("m3-caption");

            element.AddToClassList(RoleToClass(role));
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //
        private void Start()
        {
            ApplyRole(_role);
        }

        private void OnEnable()
        {
            ThemeManager.OnThemeChanged += OnThemeChanged;
        }

        private void OnDisable()
        {
            ThemeManager.OnThemeChanged -= OnThemeChanged;
        }

        // ------------------------------------------------------------------ //
        //  Private                                                             //
        // ------------------------------------------------------------------ //
        private void OnThemeChanged(ThemeData _) => ApplyRole(_role);

        private VisualElement FindElement()
        {
            if (_document == null || _document.rootVisualElement == null) return null;
            if (string.IsNullOrEmpty(_elementName)) return _document.rootVisualElement;
            return _document.rootVisualElement.Q(_elementName);
        }

        private static string RoleToClass(TextRole role) => role switch
        {
            TextRole.Display  => "m3-display",
            TextRole.Headline => "m3-headline",
            TextRole.Title    => "m3-title",
            TextRole.Body     => "m3-body",
            TextRole.Label    => "m3-label",
            TextRole.Caption  => "m3-caption",
            _                 => "m3-body"
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplyRole(_role);
        }
#endif
    }
}
