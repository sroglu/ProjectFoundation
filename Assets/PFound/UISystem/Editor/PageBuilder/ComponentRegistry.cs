using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using PFound.UISystem.Components;
using PFound.UISystem.Enums;

namespace PFound.UISystem.Editor.PageBuilder
{
    /// <summary>
    /// Static registry of all M3 components available in the Page Builder palette.
    /// Each entry holds metadata and a factory delegate for instantiation with sensible defaults.
    /// </summary>
    internal static class ComponentRegistry
    {
        internal readonly struct ComponentInfo
        {
            public readonly string DisplayName;
            public readonly string Category;
            public readonly Type ComponentType;
            public readonly Func<VisualElement> Factory;

            public ComponentInfo(string displayName, string category, Type componentType, Func<VisualElement> factory)
            {
                DisplayName = displayName;
                Category = category;
                ComponentType = componentType;
                Factory = factory;
            }
        }

        internal static readonly string[] Categories =
        {
            "Actions",
            "Selection",
            "Input",
            "Containment",
            "Communication",
            "Navigation"
        };

        private static ComponentInfo[] s_Components;

        internal static IReadOnlyList<ComponentInfo> All
        {
            get
            {
                if (s_Components == null)
                    s_Components = BuildRegistry();
                return s_Components;
            }
        }

        internal static IEnumerable<ComponentInfo> GetByCategory(string category)
        {
            foreach (var info in All)
            {
                if (info.Category == category)
                    yield return info;
            }
        }

        private static ComponentInfo[] BuildRegistry()
        {
            return new[]
            {
                // ── Actions ─────────────────────────────────────────────────
                new ComponentInfo("M3Button (Filled)", "Actions", typeof(M3Button),
                    () => new M3Button { Text = "Button", Variant = ButtonVariant.Filled }),
                new ComponentInfo("M3Button (Outlined)", "Actions", typeof(M3Button),
                    () => new M3Button { Text = "Button", Variant = ButtonVariant.Outlined }),
                new ComponentInfo("M3Button (Text)", "Actions", typeof(M3Button),
                    () => new M3Button { Text = "Button", Variant = ButtonVariant.Text }),
                new ComponentInfo("M3Button (Tonal)", "Actions", typeof(M3Button),
                    () => new M3Button { Text = "Button", Variant = ButtonVariant.Tonal }),
                new ComponentInfo("M3FAB (Regular)", "Actions", typeof(M3FAB),
                    () => new M3FAB { Size = FABSize.Regular }),
                new ComponentInfo("M3FAB (Small)", "Actions", typeof(M3FAB),
                    () => new M3FAB { Size = FABSize.Small }),
                new ComponentInfo("M3FAB (Large)", "Actions", typeof(M3FAB),
                    () => new M3FAB { Size = FABSize.Large }),
                new ComponentInfo("M3SegmentedButton", "Actions", typeof(M3SegmentedButton),
                    () => new M3SegmentedButton()),

                // ── Selection ───────────────────────────────────────────────
                new ComponentInfo("M3Checkbox", "Selection", typeof(M3Checkbox),
                    () => new M3Checkbox()),
                new ComponentInfo("M3RadioButton", "Selection", typeof(M3RadioButton),
                    () => new M3RadioButton()),
                new ComponentInfo("M3Toggle", "Selection", typeof(M3Toggle),
                    () => new M3Toggle()),
                new ComponentInfo("M3Chip (Assist)", "Selection", typeof(M3Chip),
                    () => new M3Chip { Text = "Chip", Variant = ChipVariant.Assist }),
                new ComponentInfo("M3Chip (Filter)", "Selection", typeof(M3Chip),
                    () => new M3Chip { Text = "Chip", Variant = ChipVariant.Filter }),
                new ComponentInfo("M3Chip (Input)", "Selection", typeof(M3Chip),
                    () => new M3Chip { Text = "Chip", Variant = ChipVariant.Input }),
                new ComponentInfo("M3Chip (Suggestion)", "Selection", typeof(M3Chip),
                    () => new M3Chip { Text = "Chip", Variant = ChipVariant.Suggestion }),
                new ComponentInfo("M3Slider", "Selection", typeof(M3Slider),
                    () => new M3Slider()),

                // ── Input ───────────────────────────────────────────────────
                new ComponentInfo("M3TextField (Filled)", "Input", typeof(M3TextField),
                    () => new M3TextField { Label = "Label", Variant = TextFieldVariant.Filled }),
                new ComponentInfo("M3TextField (Outlined)", "Input", typeof(M3TextField),
                    () => new M3TextField { Label = "Label", Variant = TextFieldVariant.Outlined }),
                new ComponentInfo("M3SearchBar", "Input", typeof(M3SearchBar),
                    () => new M3SearchBar()),

                // ── Containment ─────────────────────────────────────────────
                new ComponentInfo("M3Card (Elevated)", "Containment", typeof(M3Card),
                    () => new M3Card { Variant = CardVariant.Elevated }),
                new ComponentInfo("M3Card (Filled)", "Containment", typeof(M3Card),
                    () => new M3Card { Variant = CardVariant.Filled }),
                new ComponentInfo("M3Card (Outlined)", "Containment", typeof(M3Card),
                    () => new M3Card { Variant = CardVariant.Outlined }),
                new ComponentInfo("M3Dialog", "Containment", typeof(M3Dialog),
                    () => new M3Dialog()),
                new ComponentInfo("M3Menu", "Containment", typeof(M3Menu),
                    () => new M3Menu()),
                new ComponentInfo("M3List", "Containment", typeof(M3List),
                    () => new M3List()),
                new ComponentInfo("M3ListItem", "Containment", typeof(M3ListItem),
                    () => new M3ListItem()),
                new ComponentInfo("M3Divider", "Containment", typeof(M3Divider),
                    () => new M3Divider()),

                // ── Communication ───────────────────────────────────────────
                new ComponentInfo("M3Snackbar", "Communication", typeof(M3Snackbar),
                    () => new M3Snackbar()),
                new ComponentInfo("M3Badge", "Communication", typeof(M3Badge),
                    () => new M3Badge()),
                new ComponentInfo("M3ProgressIndicator", "Communication", typeof(M3ProgressIndicator),
                    () => new M3ProgressIndicator()),

                // ── Navigation ──────────────────────────────────────────────
                new ComponentInfo("M3NavigationBar", "Navigation", typeof(M3NavigationBar),
                    () => new M3NavigationBar()),
                new ComponentInfo("M3NavigationRail", "Navigation", typeof(M3NavigationRail),
                    () => new M3NavigationRail()),
                new ComponentInfo("M3TopAppBar", "Navigation", typeof(M3TopAppBar),
                    () => new M3TopAppBar()),
                new ComponentInfo("M3BottomAppBar", "Navigation", typeof(M3BottomAppBar),
                    () => new M3BottomAppBar()),
                new ComponentInfo("M3Tabs", "Navigation", typeof(M3Tabs),
                    () => new M3Tabs()),
            };
        }
    }
}
