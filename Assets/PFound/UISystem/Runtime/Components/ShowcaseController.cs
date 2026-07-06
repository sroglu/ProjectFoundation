using PFound.UISystem.Core;
using PFound.UISystem.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShowcaseController : MonoBehaviour
    {
        public enum ShowcaseTab { Overview = 0, Specs = 1, Guidelines = 2, Accessibility = 3 }

        private struct SectionData
        {
            public string Name;
            public string NavElementName;
            public string M3SpecUrl;
        }

        private static readonly SectionData[] Sections =
        {
            new SectionData { Name = "Design Principles",    NavElementName = "nav-principles",         M3SpecUrl = "https://m3.material.io/foundations" },
            new SectionData { Name = "Button",               NavElementName = "nav-button",             M3SpecUrl = "https://m3.material.io/components/buttons" },
            new SectionData { Name = "FAB",                  NavElementName = "nav-fab",                M3SpecUrl = "https://m3.material.io/components/floating-action-button" },
            new SectionData { Name = "Segmented Button",     NavElementName = "nav-segmented-button",   M3SpecUrl = "https://m3.material.io/components/segmented-buttons" },
            new SectionData { Name = "Checkbox",             NavElementName = "nav-checkbox",           M3SpecUrl = "https://m3.material.io/components/checkbox" },
            new SectionData { Name = "Chip",                 NavElementName = "nav-chip",               M3SpecUrl = "https://m3.material.io/components/chips" },
            new SectionData { Name = "Radio Button",         NavElementName = "nav-radio",              M3SpecUrl = "https://m3.material.io/components/radio-button" },
            new SectionData { Name = "Slider",               NavElementName = "nav-slider",             M3SpecUrl = "https://m3.material.io/components/sliders" },
            new SectionData { Name = "Toggle",               NavElementName = "nav-toggle",             M3SpecUrl = "https://m3.material.io/components/switch" },
            new SectionData { Name = "Text Field",           NavElementName = "nav-textfield",          M3SpecUrl = "https://m3.material.io/components/text-fields" },
            new SectionData { Name = "Search Bar",           NavElementName = "nav-search-bar",         M3SpecUrl = "https://m3.material.io/components/search" },
            new SectionData { Name = "Date Picker",          NavElementName = "nav-date-picker",        M3SpecUrl = "https://m3.material.io/components/date-pickers" },
            new SectionData { Name = "Time Picker",          NavElementName = "nav-time-picker",        M3SpecUrl = "https://m3.material.io/components/time-pickers" },
            new SectionData { Name = "Card",                 NavElementName = "nav-card",               M3SpecUrl = "https://m3.material.io/components/cards" },
            new SectionData { Name = "Dialog",               NavElementName = "nav-dialog",             M3SpecUrl = "https://m3.material.io/components/dialogs" },
            new SectionData { Name = "Bottom Sheet",         NavElementName = "nav-bottom-sheet",       M3SpecUrl = "https://m3.material.io/components/bottom-sheets" },
            new SectionData { Name = "List",                 NavElementName = "nav-list",               M3SpecUrl = "https://m3.material.io/components/lists" },
            new SectionData { Name = "Menu",                 NavElementName = "nav-menu",               M3SpecUrl = "https://m3.material.io/components/menus" },
            new SectionData { Name = "Snackbar",             NavElementName = "nav-snackbar",           M3SpecUrl = "https://m3.material.io/components/snackbar" },
            new SectionData { Name = "Tooltip",              NavElementName = "nav-tooltip",            M3SpecUrl = "https://m3.material.io/components/tooltips" },
            new SectionData { Name = "Progress Indicator",   NavElementName = "nav-progress-indicator", M3SpecUrl = "https://m3.material.io/components/progress-indicators" },
            new SectionData { Name = "Navigation Bar",       NavElementName = "nav-navigation-bar",     M3SpecUrl = "https://m3.material.io/components/navigation-bar" },
            new SectionData { Name = "Navigation Drawer",    NavElementName = "nav-navigation-drawer",  M3SpecUrl = "https://m3.material.io/components/navigation-drawer" },
            new SectionData { Name = "Navigation Rail",      NavElementName = "nav-navigation-rail",    M3SpecUrl = "https://m3.material.io/components/navigation-rail" },
            new SectionData { Name = "Top App Bar",          NavElementName = "nav-top-app-bar",        M3SpecUrl = "https://m3.material.io/components/top-app-bar" },
            new SectionData { Name = "Bottom App Bar",       NavElementName = "nav-bottom-app-bar",     M3SpecUrl = "https://m3.material.io/components/bottom-app-bar" },
            new SectionData { Name = "Tabs",                 NavElementName = "nav-tabs",               M3SpecUrl = "https://m3.material.io/components/tabs" },
            new SectionData { Name = "Typography",           NavElementName = "nav-typography",          M3SpecUrl = "https://m3.material.io/styles/typography" },
            new SectionData { Name = "Color",                NavElementName = "nav-color",               M3SpecUrl = "https://m3.material.io/styles/color" },
        };

        private UIDocument     _document;
        private VisualElement  _root;
        private VisualElement  _tabContent;
        private Label          _specLink;
        private int            _activeSectionIndex = 1;
        private ShowcaseTab    _activeTab          = ShowcaseTab.Overview;

        private M3Tabs _m3TabBar;
        private readonly VisualElement[] _tabs     = new VisualElement[4];
        private readonly VisualElement[] _navItems = new VisualElement[Sections.Length];

        private const string ActiveTabClass     = "showcase-tab--active";
        private const string ActiveNavItemClass = "showcase-nav__item--active";

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            ThemeManager.RegisterPanel(_document);
            _root = _document.rootVisualElement;
            BindThemeToggle();
            BindTabBar();
            BindNavPanel();
            NavigateTo(_activeSectionIndex);
        }

        private void OnDestroy()
        {
            ThemeManager.UnregisterPanel(_document);
        }

        public void NavigateTo(int sectionIndex)
        {
            _activeSectionIndex = Mathf.Clamp(sectionIndex, 0, Sections.Length - 1);
            UpdateNavHighlight();
            UpdateSpecLink();
            RefreshContent();
        }

        public void SwitchTab(ShowcaseTab tab)
        {
            _activeTab = tab;
            UpdateTabHighlight();
            RefreshContent();
        }

        // ------------------------------------------------------------------ //
        //  Theme toggle                                                       //
        // ------------------------------------------------------------------ //

        private void BindThemeToggle()
        {
            var actions = _root.Q("top-bar-actions");
            if (actions == null) return;

            var toggleBtn = new M3Button { Text = "Toggle Theme", Variant = ButtonVariant.Tonal };
            toggleBtn.OnClick += () => ThemeManager.ToggleLightDark();
            actions.Add(toggleBtn);
        }

        // ------------------------------------------------------------------ //
        //  Binding                                                             //
        // ------------------------------------------------------------------ //

        private void BindTabBar()
        {
            _tabContent = _root.Q("tab-content");

            var tabBar = _root.Q("tab-bar");
            if (tabBar != null)
            {
                tabBar.Clear();

                // Replace UXML tabs with M3Tabs component
                _m3TabBar = new M3Tabs { Variant = M3Tabs.TabsVariant.Primary };
                _m3TabBar.AddTab("Overview");
                _m3TabBar.AddTab("Specs");
                _m3TabBar.AddTab("Guidelines");
                _m3TabBar.AddTab("Accessibility");
                _m3TabBar.style.flexGrow = 1;
                _m3TabBar.OnTabChanged += idx => SwitchTab((ShowcaseTab)idx);
                tabBar.Add(_m3TabBar);

                // M3 Spec link
                _specLink = new Label("M3 Spec \u2197");
                _specLink.AddToClassList("showcase-spec-link");
                _specLink.AddToClassList("m3-label-large");
                _specLink.RegisterCallback<ClickEvent>(_ =>
                    Application.OpenURL(Sections[_activeSectionIndex].M3SpecUrl));
                tabBar.Add(_specLink);
            }
        }

        private void BindNavPanel()
        {
            for (int i = 0; i < Sections.Length; i++)
            {
                int c = i;
                _navItems[i] = _root.Q(Sections[i].NavElementName);
                _navItems[i]?.RegisterCallback<ClickEvent>(_ => NavigateTo(c));
            }
            UpdateNavHighlight();
        }

        private void UpdateTabHighlight()
        {
            _m3TabBar?.SelectTab((int)_activeTab);
        }

        private void UpdateNavHighlight()
        {
            for (int i = 0; i < _navItems.Length && i < Sections.Length; i++)
                _navItems[i]?.EnableInClassList(ActiveNavItemClass, i == _activeSectionIndex);
        }

        private void UpdateSpecLink()
        {
            if (_specLink != null)
                _specLink.text = "M3 Spec \u2197";
        }

        private void RefreshContent()
        {
            if (_tabContent == null) return;
            _tabContent.Clear();
            var section = Sections[_activeSectionIndex];

            switch (_activeTab)
            {
                case ShowcaseTab.Overview:      BuildOverviewTab(section); break;
                case ShowcaseTab.Specs:         BuildSpecsTab(section); break;
                case ShowcaseTab.Guidelines:    BuildGuidelinesTab(section); break;
                case ShowcaseTab.Accessibility: BuildAccessibilityTab(section); break;
            }
        }

        // ------------------------------------------------------------------ //
        //  Overview — live component demos                                    //
        // ------------------------------------------------------------------ //

        private void BuildOverviewTab(SectionData section)
        {
            AddSectionHeader(section.Name);

            var demoArea = new VisualElement();
            demoArea.AddToClassList("showcase-demo-area");
            demoArea.style.flexDirection = FlexDirection.Column;

            switch (section.NavElementName)
            {
                case "nav-button":           DemoButton(demoArea); break;
                case "nav-fab":              DemoFAB(demoArea); break;
                case "nav-segmented-button": DemoSegmentedButton(demoArea); break;
                case "nav-checkbox":         DemoCheckbox(demoArea); break;
                case "nav-chip":             DemoChip(demoArea); break;
                case "nav-radio":            DemoRadio(demoArea); break;
                case "nav-slider":           DemoSlider(demoArea); break;
                case "nav-toggle":           DemoToggle(demoArea); break;
                case "nav-textfield":        DemoTextField(demoArea); break;
                case "nav-search-bar":       DemoSearchBar(demoArea); break;
                case "nav-card":             DemoCard(demoArea); break;
                case "nav-dialog":           DemoDialog(demoArea); break;
                case "nav-tooltip":          DemoTooltip(demoArea); break;
                case "nav-list":             DemoList(demoArea); break;
                case "nav-menu":             DemoMenu(demoArea); break;
                case "nav-date-picker":      DemoDatePicker(demoArea); break;
                case "nav-time-picker":      DemoTimePicker(demoArea); break;
                case "nav-progress-indicator": DemoProgress(demoArea); break;
                case "nav-tabs":             DemoTabs(demoArea); break;
                case "nav-navigation-bar":   DemoNavBar(demoArea); break;
                case "nav-top-app-bar":      DemoTopAppBar(demoArea); break;
                case "nav-bottom-app-bar":   DemoBottomAppBar(demoArea); break;
                case "nav-typography":       DemoTypography(demoArea); break;
                case "nav-color":            DemoColor(demoArea); break;
                default:
                    var placeholder = new Label($"{section.Name} — demo coming soon.");
                    placeholder.AddToClassList("m3-body-medium");
                    placeholder.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                    placeholder.style.marginTop = 16f;
                    demoArea.Add(placeholder);
                    break;
            }

            _tabContent.Add(demoArea);
        }

        // ---- Demo builders ---- //

        private void DemoButton(VisualElement area)
        {
            AddDemoLabel(area, "Variants");
            var row = Row();
            AddSpacedButton(row, new M3Button { Text = "Filled",   Variant = ButtonVariant.Filled });
            AddSpacedButton(row, new M3Button { Text = "Outlined", Variant = ButtonVariant.Outlined });
            AddSpacedButton(row, new M3Button { Text = "Text",     Variant = ButtonVariant.Text });
            AddSpacedButton(row, new M3Button { Text = "Tonal",    Variant = ButtonVariant.Tonal });
            area.Add(row);

            AddDemoLabel(area, "Disabled");
            var row2 = Row();
            AddSpacedButton(row2, new M3Button { Text = "Filled",   Variant = ButtonVariant.Filled,   Disabled = true });
            AddSpacedButton(row2, new M3Button { Text = "Outlined", Variant = ButtonVariant.Outlined, Disabled = true });
            area.Add(row2);
        }

        private static void AddSpacedButton(VisualElement row, M3Button btn)
        {
            btn.style.marginRight  = 12f;
            btn.style.marginBottom = 12f;
            btn.style.minWidth     = 120f;  // give labels room — m3 medium button width per spec
            row.Add(btn);
        }

        private void DemoFAB(VisualElement area)
        {
            AddDemoLabel(area, "Sizes");
            var row = Row();
            row.Add(new M3FAB { Size = FABSize.Small,   Icon = FABIcon.Add });
            row.Add(new M3FAB { Size = FABSize.Regular,  Icon = FABIcon.Edit });
            row.Add(new M3FAB { Size = FABSize.Large,   Icon = FABIcon.Star });
            area.Add(row);

            AddDemoLabel(area, "Extended");
            var row2 = Row();
            row2.Add(new M3FAB { Extended = true, Text = "New item", Icon = FABIcon.Add });
            area.Add(row2);
        }

        private void DemoSegmentedButton(VisualElement area)
        {
            AddDemoLabel(area, "Single Select");
            var seg = new M3SegmentedButton { Mode = M3SegmentedButton.SelectionMode.SingleSelect };
            seg.AddSegment("Day");
            seg.AddSegment("Week");
            seg.AddSegment("Month");
            seg.style.width = 360f;
            area.Add(seg);

            AddDemoLabel(area, "Multi Select");
            var seg2 = new M3SegmentedButton { Mode = M3SegmentedButton.SelectionMode.MultiSelect };
            seg2.AddSegment("Bold");
            seg2.AddSegment("Italic");
            seg2.AddSegment("Underline");
            seg2.style.width = 360f;
            area.Add(seg2);
        }

        private void DemoCheckbox(VisualElement area)
        {
            AddDemoLabel(area, "States");
            var row = Row();
            AddLabeledControl(row, "Unchecked", new M3Checkbox { State = CheckboxState.Unchecked });
            AddLabeledControl(row, "Checked",   new M3Checkbox { State = CheckboxState.Checked });
            AddLabeledControl(row, "Indeterminate", new M3Checkbox { State = CheckboxState.Indeterminate });
            AddLabeledControl(row, "Disabled",  new M3Checkbox { State = CheckboxState.Checked, Disabled = true });
            area.Add(row);
        }

        private void DemoChip(VisualElement area)
        {
            AddDemoLabel(area, "Variants");
            var row = Row();
            row.Add(SpacedChip(new M3Chip { Text = "Assist", Variant = ChipVariant.Assist, Icon = ChipIcon.Star }));
            row.Add(SpacedChip(new M3Chip { Text = "Filter", Variant = ChipVariant.Filter }));
            row.Add(SpacedChip(new M3Chip { Text = "Input",  Variant = ChipVariant.Input }));
            row.Add(SpacedChip(new M3Chip { Text = "Suggestion", Variant = ChipVariant.Suggestion }));
            area.Add(row);

            AddDemoLabel(area, "Selected");
            var row2 = Row();
            row2.Add(SpacedChip(new M3Chip { Text = "Active Filter", Variant = ChipVariant.Filter, Selected = true }));
            row2.Add(SpacedChip(new M3Chip { Text = "Disabled", Variant = ChipVariant.Assist, Disabled = true }));
            area.Add(row2);
        }

        // M3 chip-row spec: 8dp horizontal gap + 8dp vertical gap when wrapping.
        private static M3Chip SpacedChip(M3Chip chip)
        {
            chip.style.marginRight = 8f;
            chip.style.marginBottom = 8f;
            return chip;
        }

        private void DemoRadio(VisualElement area)
        {
            AddDemoLabel(area, "Radio Group");
            var row = Row();
            var rbA = new M3RadioButton { Text = "Option A" };
            var rbB = new M3RadioButton { Text = "Option B" };
            var rbC = new M3RadioButton { Text = "Option C" };
            var group = new M3RadioGroup(rbA, rbB, rbC);
            row.Add(rbA);
            row.Add(rbB);
            row.Add(rbC);
            row.Add(new M3RadioButton { Text = "Disabled", Disabled = true });
            area.Add(row);
        }

        private void DemoSlider(VisualElement area)
        {
            AddDemoLabel(area, "Continuous");
            var slider1 = new M3Slider { Min = 0f, Max = 100f, Value = 40f, ShowValueLabel = true };
            slider1.style.width = 300f;
            area.Add(slider1);

            AddDemoLabel(area, "Discrete (step = 10)");
            var slider2 = new M3Slider { Min = 0f, Max = 100f, Value = 50f, Step = 10f, ShowValueLabel = true };
            slider2.style.width = 300f;
            area.Add(slider2);

            AddDemoLabel(area, "Disabled");
            var slider3 = new M3Slider { Min = 0f, Max = 100f, Value = 30f, Disabled = true };
            slider3.style.width = 300f;
            area.Add(slider3);
        }

        private void DemoToggle(VisualElement area)
        {
            AddDemoLabel(area, "States");
            var row = Row();
            AddLabeledControl(row, "Off", new M3Toggle { Value = false });
            AddLabeledControl(row, "On",  new M3Toggle { Value = true });
            AddLabeledControl(row, "Disabled", new M3Toggle { Value = true, Disabled = true });
            area.Add(row);
        }

        private void DemoTextField(VisualElement area)
        {
            AddDemoLabel(area, "Variants");
            var tf1 = new M3TextField { Label = "Filled", Variant = TextFieldVariant.Filled, HelperText = "Helper text" };
            tf1.style.width = 300f;
            tf1.style.marginBottom = 16f;
            area.Add(tf1);

            var tf2 = new M3TextField { Label = "Outlined", Variant = TextFieldVariant.Outlined };
            tf2.style.width = 300f;
            tf2.style.marginBottom = 16f;
            area.Add(tf2);

            AddDemoLabel(area, "Error State");
            var tf3 = new M3TextField { Label = "Email", Variant = TextFieldVariant.Outlined, HasError = true, ErrorText = "Invalid email address" };
            tf3.style.width = 300f;
            tf3.style.marginBottom = 16f;
            area.Add(tf3);

            AddDemoLabel(area, "Pre-filled");
            var tf4 = new M3TextField { Label = "Username", Variant = TextFieldVariant.Filled, Value = "john.doe" };
            tf4.style.width = 300f;
            area.Add(tf4);
        }

        private void DemoSearchBar(VisualElement area)
        {
            AddDemoLabel(area, "Search Bar");
            var sb = new M3SearchBar { Placeholder = "Search components..." };
            sb.style.width = 400f;
            area.Add(sb);
        }

        private void DemoCard(VisualElement area)
        {
            AddDemoLabel(area, "Variants");
            var row = Row();
            foreach (var variant in new[] { CardVariant.Elevated, CardVariant.Filled, CardVariant.Outlined })
            {
                var card = new M3Card { Variant = variant, Clickable = true };
                card.style.width = 200f;
                card.style.height = 120f;
                card.style.marginRight  = 16f;
                card.style.marginBottom = 16f;
                var title = new Label(variant.ToString());
                title.AddToClassList("m3-title-medium");
                title.style.marginBottom = 8f;
                card.Add(title);
                var body = new Label("Card content goes here with some description text.");
                body.AddToClassList("m3-body-small");
                body.style.whiteSpace = WhiteSpace.Normal;
                card.Add(body);
                row.Add(card);
            }
            area.Add(row);
        }

        private void DemoDialog(VisualElement area)
        {
            AddDemoLabel(area, "Open Dialog");
            var btn = new M3Button { Text = "Show Dialog", Variant = ButtonVariant.Filled };
            btn.OnClick += () =>
            {
                var dialog = new M3Dialog
                {
                    Headline = "Discard changes?",
                    Body = "All unsaved changes will be lost.",
                    ConfirmText = "Discard",
                    DismissText = "Cancel"
                };
                dialog.Show(_root);
            };
            area.Add(btn);
        }

        private void DemoTooltip(VisualElement area)
        {
            AddDemoLabel(area, "Plain");
            var anchor1 = new M3Button { Text = "Hover me", Variant = ButtonVariant.Outlined };
            anchor1.style.alignSelf = Align.FlexStart;
            anchor1.style.marginBottom = 16f;
            var plain = new M3Tooltip { Variant = M3Tooltip.TooltipVariant.Plain, Body = "Save your changes" };
            plain.Attach(anchor1);
            area.Add(anchor1);

            AddDemoLabel(area, "Rich (title + body)");
            var anchor2 = new M3Button { Text = "Long-press for rich", Variant = ButtonVariant.Filled };
            anchor2.style.alignSelf = Align.FlexStart;
            var rich = new M3Tooltip
            {
                Variant = M3Tooltip.TooltipVariant.Rich,
                Title = "Cloud sync",
                Body = "Keeps your work in sync across devices. Press and hold to learn more."
            };
            rich.Attach(anchor2);
            area.Add(anchor2);
        }

        private void DemoMenu(VisualElement area)
        {
            AddDemoLabel(area, "Open Menu");
            var btn = new M3Button { Text = "Show Menu", Variant = ButtonVariant.Outlined };
            btn.style.alignSelf = Align.FlexStart;
            btn.OnClick += () =>
            {
                var menu = new M3Menu();
                menu.AddItem("New",       Utils.MaterialSymbols.Add);
                menu.AddItem("Open",      Utils.MaterialSymbols.Search);
                menu.AddItem("Save",      Utils.MaterialSymbols.Star);
                menu.AddDivider();
                menu.AddItem("Settings",  Utils.MaterialSymbols.Person);
                menu.AddItem("Quit",      Utils.MaterialSymbols.Close);
                menu.Show(btn);
            };
            area.Add(btn);
        }

        private void DemoDatePicker(VisualElement area)
        {
            AddDemoLabel(area, "Modal Date Picker");
            var btn = new M3Button { Text = "Pick a Date", Variant = ButtonVariant.Outlined };
            btn.style.alignSelf = Align.FlexStart;

            var resultLabel = new Label("Selected: (none)");
            resultLabel.AddToClassList("m3-body-medium");
            resultLabel.style.marginTop = 12f;

            btn.OnClick += () =>
            {
                var picker = new M3DatePicker();
                picker.OnDateSelected += d =>
                    resultLabel.text = $"Selected: {d:yyyy-MM-dd}";
                picker.Show(_root);
            };

            area.Add(btn);
            area.Add(resultLabel);
        }

        private void DemoTimePicker(VisualElement area)
        {
            AddDemoLabel(area, "Modal Time Picker");
            var btn = new M3Button { Text = "Pick a Time", Variant = ButtonVariant.Outlined };
            btn.style.alignSelf = Align.FlexStart;

            var resultLabel = new Label("Selected: (none)");
            resultLabel.AddToClassList("m3-body-medium");
            resultLabel.style.marginTop = 12f;

            btn.OnClick += () =>
            {
                var picker = new M3TimePicker();
                picker.OnTimeSelected += t =>
                    resultLabel.text = $"Selected: {t:hh\\:mm}";
                picker.Show(_root);
            };

            area.Add(btn);
            area.Add(resultLabel);
        }

        private void DemoList(VisualElement area)
        {
            // --- Standard: two-line with leading icon + trailing text ---
            AddDemoLabel(area, "Standard");
            var list1 = new M3List();
            list1.style.width = 360f;

            var a1 = new M3ListItem { Headline = "Label text", Supporting = "Supporting text", Variant = M3ListItem.ListItemVariant.TwoLine };
            a1.SetLeadingIcon(Utils.MaterialSymbols.Person);
            AddTrailingText(a1, "100+");
            list1.Add(a1);

            var a2 = new M3ListItem { Headline = "Label text 2", Supporting = "Supporting text", Variant = M3ListItem.ListItemVariant.TwoLine };
            a2.SetLeadingIcon(Utils.MaterialSymbols.Person);
            AddTrailingText(a2, "78");
            list1.Add(a2);

            var a3 = new M3ListItem { Headline = "Label text 3", Supporting = "Supporting text", Variant = M3ListItem.ListItemVariant.TwoLine };
            a3.SetLeadingIcon(Utils.MaterialSymbols.Person);
            AddTrailingText(a3, "47");
            list1.Add(a3);

            area.Add(list1);

            // --- Segmented: two-line with leading icon ---
            AddDemoLabel(area, "Segmented");
            var list2 = new M3List { Style = M3List.ListStyle.Segmented };
            list2.style.width = 360f;

            var b1 = new M3ListItem { Headline = "Inbox", Supporting = "5 new messages", Variant = M3ListItem.ListItemVariant.TwoLine };
            b1.SetLeadingIcon(Utils.MaterialSymbols.Email);
            list2.Add(b1);

            var b2 = new M3ListItem { Headline = "Drafts", Supporting = "2 unsent", Variant = M3ListItem.ListItemVariant.TwoLine };
            b2.SetLeadingIcon(Utils.MaterialSymbols.Edit);
            list2.Add(b2);

            var b3 = new M3ListItem { Headline = "Notifications", Supporting = "3 alerts", Variant = M3ListItem.ListItemVariant.TwoLine };
            b3.SetLeadingIcon(Utils.MaterialSymbols.Notifications);
            list2.Add(b3);

            var b4 = new M3ListItem { Headline = "Settings", Variant = M3ListItem.ListItemVariant.OneLine };
            b4.SetLeadingIcon(Utils.MaterialSymbols.Settings);
            list2.Add(b4);

            area.Add(list2);

            // --- Three-line with trailing icon ---
            AddDemoLabel(area, "Three-line");
            var list3 = new M3List();
            list3.style.width = 360f;

            var c1 = new M3ListItem { Headline = "21st Century Strangers", Supporting = "For Now\nNBR", Variant = M3ListItem.ListItemVariant.ThreeLine };
            c1.SetLeadingIcon(Utils.MaterialSymbols.PlayArrow);
            AddTrailingIcon(c1, Utils.MaterialSymbols.MoreVert);
            list3.Add(c1);

            var c2 = new M3ListItem { Headline = "Strangers", Supporting = "Trapezoid\nWarped Speed", Variant = M3ListItem.ListItemVariant.ThreeLine };
            c2.SetLeadingIcon(Utils.MaterialSymbols.PlayArrow);
            AddTrailingIcon(c2, Utils.MaterialSymbols.MoreVert);
            list3.Add(c2);

            area.Add(list3);
        }

        private void AddTrailingText(M3ListItem item, string text)
        {
            var label = new Label(text);
            label.AddToClassList("m3-label-small");
            label.style.color = new StyleColor(new Color(0.29f, 0.27f, 0.31f));
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            item.TrailingSlot.Add(label);
            item.TrailingSlot.style.display = DisplayStyle.Flex;
        }

        private void AddTrailingIcon(M3ListItem item, string codepoint)
        {
            var icon = new Label(codepoint);
            icon.AddToClassList("m3-icon");
            icon.style.fontSize = 24;
            icon.style.color = new StyleColor(new Color(0.29f, 0.27f, 0.31f));
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            item.TrailingSlot.Add(icon);
            item.TrailingSlot.style.display = DisplayStyle.Flex;
        }

        private void DemoProgress(VisualElement area)
        {
            AddDemoLabel(area, "Linear (60%)");
            var linear = new M3ProgressIndicator { Variant = M3ProgressIndicator.ProgressVariant.Linear, Progress = 0.6f };
            linear.style.width = 300f;
            area.Add(linear);

            AddDemoLabel(area, "Linear (30%)");
            var linear30 = new M3ProgressIndicator { Variant = M3ProgressIndicator.ProgressVariant.Linear, Progress = 0.3f };
            linear30.style.width = 300f;
            area.Add(linear30);

            AddDemoLabel(area, "Circular");
            var row = Row();
            row.Add(new M3ProgressIndicator { Variant = M3ProgressIndicator.ProgressVariant.Circular, Progress = 0.75f });
            row.Add(new M3ProgressIndicator { Variant = M3ProgressIndicator.ProgressVariant.Circular, Progress = 0.4f });
            area.Add(row);
        }

        private void DemoTabs(VisualElement area)
        {
            AddDemoLabel(area, "Primary Tabs (icon + label)");
            var tabs0 = new M3Tabs { Variant = M3Tabs.TabsVariant.Primary };
            tabs0.AddTab("Flights", "\ue539");
            tabs0.AddTab("Trips",   "\ue53e");
            tabs0.AddTab("Explore", "\ue87a");
            tabs0.style.width = 400f;
            area.Add(tabs0);

            AddDemoLabel(area, "Primary Tabs");
            var tabs = new M3Tabs { Variant = M3Tabs.TabsVariant.Primary };
            tabs.AddTab("Overview");
            tabs.AddTab("Details");
            tabs.AddTab("Reviews");
            tabs.style.width = 400f;
            area.Add(tabs);

            AddDemoLabel(area, "Secondary Tabs");
            var tabs2 = new M3Tabs { Variant = M3Tabs.TabsVariant.Secondary };
            tabs2.AddTab("All");
            tabs2.AddTab("Unread");
            tabs2.AddTab("Starred");
            tabs2.style.width = 400f;
            area.Add(tabs2);
        }

        private void DemoNavBar(VisualElement area)
        {
            AddDemoLabel(area, "Navigation Bar");
            var nav = new M3NavigationBar();
            nav.AddItem(new M3NavigationItem { Icon = "\ue88a", Label = "Home", Active = true });
            nav.AddItem(new M3NavigationItem { Icon = "\ue8b6", Label = "Search" });
            nav.AddItem(new M3NavigationItem { Icon = "\ue7fb", Label = "Profile" });
            nav.style.width = 400f;
            area.Add(nav);
        }

        private void DemoTopAppBar(VisualElement area)
        {
            AddDemoLabel(area, "Small");
            var small = new M3TopAppBar
            {
                Variant = M3TopAppBar.TopAppBarVariant.Small,
                Headline = "Page title",
                ShowNavIcon = true
            };
            small.AddActionIcon(Utils.MaterialSymbols.Search);
            small.AddActionIcon(Utils.MaterialSymbols.MoreVert);
            small.style.width = 400f;
            small.style.marginBottom = 16f;
            area.Add(small);

            AddDemoLabel(area, "Center-aligned");
            var center = new M3TopAppBar
            {
                Variant = M3TopAppBar.TopAppBarVariant.CenterAligned,
                Headline = "Page title",
                ShowNavIcon = true
            };
            center.AddActionIcon(Utils.MaterialSymbols.MoreVert);
            center.style.width = 400f;
            area.Add(center);
        }

        private void DemoBottomAppBar(VisualElement area)
        {
            AddDemoLabel(area, "Bottom App Bar");
            var bar = new M3BottomAppBar();
            bar.AddIconButton(Utils.MaterialSymbols.Search);
            bar.AddIconButton(Utils.MaterialSymbols.Person);
            bar.AddIconButton(Utils.MaterialSymbols.Star);
            bar.FabSlot.Add(new M3FAB { Size = FABSize.Regular, Icon = FABIcon.Add });
            bar.style.width = 400f;
            area.Add(bar);
        }

        private void DemoTypography(VisualElement area)
        {
            string[] roles = {
                "m3-display-large", "m3-display-medium", "m3-display-small",
                "m3-headline-large", "m3-headline-medium", "m3-headline-small",
                "m3-title-large", "m3-title-medium", "m3-title-small",
                "m3-body-large", "m3-body-medium", "m3-body-small",
                "m3-label-large", "m3-label-medium", "m3-label-small"
            };
            foreach (var role in roles)
            {
                var label = new Label(role.Replace("m3-", "").Replace("-", " "));
                label.AddToClassList(role);
                label.style.marginBottom = 4f;
                area.Add(label);
            }
        }

        private void DemoColor(VisualElement area)
        {
            AddDemoLabel(area, "Theme Color Roles");
            var theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;

            foreach (ColorRole role in System.Enum.GetValues(typeof(ColorRole)))
            {
                var color = theme.GetColor(role);
                if (color.a < 0.01f) continue;

                var swatch = new VisualElement();
                swatch.style.width = 80f;
                swatch.style.height = 60f;
                swatch.style.backgroundColor = color;
                swatch.style.borderTopLeftRadius = 8f;
                swatch.style.borderTopRightRadius = 8f;
                swatch.style.borderBottomLeftRadius = 8f;
                swatch.style.borderBottomRightRadius = 8f;
                swatch.style.marginRight = 8f;
                swatch.style.marginBottom = 8f;
                swatch.style.alignItems = Align.Center;
                swatch.style.justifyContent = Justify.Center;

                var lbl = new Label(role.ToString());
                lbl.style.fontSize = 9f;
                float lum = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                lbl.style.color = lum < 0.5f ? Color.white : Color.black;
                swatch.Add(lbl);
                grid.Add(swatch);
            }
            area.Add(grid);
        }

        // ------------------------------------------------------------------ //
        //  Specs tab — component API & dimensions                            //
        // ------------------------------------------------------------------ //

        private void BuildSpecsTab(SectionData section)
        {
            AddSectionHeader($"{section.Name} — Specifications");

            switch (section.NavElementName)
            {
                case "nav-button":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Text",     "string",        "\"Button\"" },
                        new[] { "Variant",  "ButtonVariant", "Filled" },
                        new[] { "Size",     "ButtonSize",    "Medium" },
                        new[] { "Shape",    "ButtonShape",   "Round" },
                        new[] { "Compact",  "bool",          "false" },
                        new[] { "Disabled", "bool",          "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecTable(new[] { "Size", "Height", "Padding", "Radius" },
                        new[] { "ExtraSmall", "24dp", "12dp", "12dp" },
                        new[] { "Small",      "32dp", "16dp", "16dp" },
                        new[] { "Medium",     "40dp", "24dp", "20dp (pill)" },
                        new[] { "Large",      "56dp", "32dp", "28dp" },
                        new[] { "ExtraLarge", "96dp", "48dp", "48dp" });
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Filled bg", "--m3-primary");
                    AddSpecRow("Filled label", "--m3-on-primary");
                    AddSpecRow("Outlined border", "--m3-outline");
                    AddSpecRow("Tonal bg", "--m3-secondary-container");
                    AddSpecRow("Text label", "--m3-primary");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-button");
                    AddSpecRow("Variants", ".m3-button--filled, --outlined, --text, --tonal");
                    AddSpecRow("Sizes", ".m3-button--xs, --sm, --lg, --xl");
                    break;

                case "nav-fab":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Size",     "FABSize",  "Regular" },
                        new[] { "Extended", "bool",     "false" },
                        new[] { "Text",     "string",   "\"\"" },
                        new[] { "Icon",     "FABIcon",  "Add" },
                        new[] { "Disabled", "bool",     "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecTable(new[] { "Size", "Width", "Height", "Radius", "Icon" },
                        new[] { "Small",   "40dp", "40dp", "12dp", "24dp" },
                        new[] { "Regular", "56dp", "56dp", "16dp", "24dp" },
                        new[] { "Large",   "96dp", "96dp", "28dp", "36dp" });
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Container", "--m3-primary-container");
                    AddSpecRow("Icon/Label", "--m3-on-primary-container");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-fab");
                    AddSpecRow("Sizes", ".m3-fab--small, --regular, --large");
                    AddSpecRow("Extended", ".m3-fab--extended");
                    break;

                case "nav-card":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Variant",   "CardVariant", "Elevated" },
                        new[] { "Clickable", "bool",        "false" },
                        new[] { "Disabled",  "bool",        "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Corner radius", "12dp (shape-medium)");
                    AddSpecRow("Min size", "Content-sized (flexible)");
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Elevated bg", "--m3-surface + tonal overlay");
                    AddSpecRow("Filled bg", "--m3-surface-container-highest");
                    AddSpecRow("Outlined border", "--m3-outline-variant");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-card");
                    AddSpecRow("Variants", ".m3-card--elevated, --filled, --outlined");
                    break;

                case "nav-checkbox":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "State",    "CheckboxState", "Unchecked" },
                        new[] { "Disabled", "bool",          "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Box size", "18x18dp");
                    AddSpecRow("Corner radius", "2dp");
                    AddSpecRow("Outline", "2dp (unchecked)");
                    AddSpecRow("Touch target", "48x48dp");
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Unchecked outline", "--m3-outline");
                    AddSpecRow("Checked fill", "--m3-primary");
                    AddSpecRow("Icon", "white (on-primary)");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-checkbox");
                    AddSpecRow("States", ".m3-checkbox--unchecked, --checked, --indeterminate");
                    break;

                case "nav-chip":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Text",     "string",      "\"Chip\"" },
                        new[] { "Variant",  "ChipVariant", "Assist" },
                        new[] { "Icon",     "ChipIcon",    "None" },
                        new[] { "Selected", "bool",        "false" },
                        new[] { "Disabled", "bool",        "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Height", "32dp");
                    AddSpecRow("Corner radius", "8dp");
                    AddSpecRow("Padding", "16dp horizontal");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-chip");
                    AddSpecRow("Variants", ".m3-chip--assist, --filter, --input, --suggestion");
                    break;

                case "nav-toggle":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Value",    "bool", "false" },
                        new[] { "Disabled", "bool", "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Track", "52x32dp");
                    AddSpecRow("Thumb (off)", "16dp diameter");
                    AddSpecRow("Thumb (on)", "24dp diameter");
                    AddSpecRow("Corner radius", "16dp (pill)");
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Track (off)", "--m3-surface-container-highest");
                    AddSpecRow("Track (on)", "--m3-primary");
                    AddSpecRow("Thumb (off)", "--m3-outline");
                    AddSpecRow("Thumb (on)", "--m3-on-primary");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-toggle");
                    AddSpecRow("States", ".m3-toggle--checked, --unchecked");
                    break;

                case "nav-textfield":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Label",      "string",           "\"\"" },
                        new[] { "Variant",    "TextFieldVariant", "Filled" },
                        new[] { "HelperText", "string",           "\"\"" },
                        new[] { "HasError",   "bool",             "false" },
                        new[] { "ErrorText",  "string",           "\"\"" },
                        new[] { "Disabled",   "bool",             "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Height", "56dp");
                    AddSpecRow("Corner radius (Filled)", "4dp top");
                    AddSpecRow("Corner radius (Outlined)", "4dp all");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-textfield");
                    AddSpecRow("Variants", ".m3-textfield--filled, --outlined");
                    break;

                case "nav-slider":
                    AddSpecSubheader("Properties");
                    AddSpecTable(new[] { "Property", "Type", "Default" },
                        new[] { "Min",            "float", "0" },
                        new[] { "Max",            "float", "100" },
                        new[] { "Value",          "float", "0" },
                        new[] { "Step",           "float", "0 (continuous)" },
                        new[] { "ShowValueLabel", "bool",  "false" },
                        new[] { "Disabled",       "bool",  "false" });
                    AddSpecSubheader("Dimensions");
                    AddSpecRow("Track height", "4dp");
                    AddSpecRow("Thumb", "20dp diameter");
                    AddSpecRow("Active indicator", "4dp height");
                    AddSpecSubheader("Color Tokens");
                    AddSpecRow("Active track", "--m3-primary");
                    AddSpecRow("Inactive track", "--m3-surface-container-highest");
                    AddSpecRow("Thumb", "--m3-primary");
                    AddSpecSubheader("USS Classes");
                    AddSpecRow("Base", ".m3-slider");
                    break;

                default:
                    AddSpecRow("Namespace", "PFound.UISystem.Components");
                    AddSpecRow("USS File", $"{section.Name.Replace(" ", "").ToLower()}.uss");
                    var note = new Label("Detailed specs coming soon.");
                    note.AddToClassList("m3-body-medium");
                    note.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                    note.style.marginTop = 16f;
                    _tabContent.Add(note);
                    break;
            }
        }

        // ------------------------------------------------------------------ //
        //  Guidelines tab — Do / Don't / Accessibility                       //
        // ------------------------------------------------------------------ //

        private void BuildGuidelinesTab(SectionData section)
        {
            AddSectionHeader($"{section.Name} — Usage Guidelines");

            switch (section.NavElementName)
            {
                case "nav-button":
                    AddGuidelineSub("When to use");
                    AddDo("Use Filled for the single most important action on a screen.");
                    AddDo("Use Tonal for secondary but still prominent actions.");
                    AddDo("Use Outlined for actions that need emphasis but not primary focus.");
                    AddDo("Use Text for low-emphasis actions like Cancel or Skip.");
                    AddDont("Don't place multiple Filled buttons side by side \u2014 they compete for attention.");
                    AddDont("Don't use Elevated buttons inside cards or dialogs.");
                    AddGuidelineSub("Sizing");
                    AddDo("Use Medium (default) for most actions.");
                    AddDo("Use Large or ExtraLarge for hero-level primary actions.");
                    AddDo("Use Small for dense UIs or inline actions.");
                    AddDont("Don't mix different sizes of the same variant in one row.");
                    AddGuidelineSub("Accessibility");
                    AddDo("Use descriptive, action-oriented labels (\"Save\" not \"OK\").");
                    AddDo("Disabled state reduces opacity to 38% \u2014 ensure contrast is still readable.");
                    AddDo("Minimum touch target: 48x48dp (all size variants meet this).");
                    break;

                case "nav-fab":
                    AddGuidelineSub("When to use");
                    AddDo("Use a single FAB per screen for the primary, most important action.");
                    AddDo("Use Extended mode when the action label adds clarity (e.g., \"Add Note\").");
                    AddDo("Use Large size when the FAB is the dominant action on a complex screen.");
                    AddDont("Don't use multiple FABs on the same screen.");
                    AddDont("Don't use FAB for secondary or optional actions \u2014 use regular buttons.");
                    AddGuidelineSub("Placement");
                    AddDo("Anchor to a consistent position (typically bottom-right for LTR).");
                    AddDont("Don't hide or auto-dismiss FAB without user action.");
                    AddGuidelineSub("Accessibility");
                    AddDo("Always provide a descriptive Icon + optional Text in extended mode.");
                    AddDo("Minimum touch target: 56x56dp (met by Regular/Large sizes).");
                    break;

                case "nav-card":
                    AddGuidelineSub("When to use");
                    AddDo("Use Elevated cards for content that should appear prominent or distinct.");
                    AddDo("Use Filled cards for flat, consistent layouts.");
                    AddDo("Use Outlined cards for secondary information hierarchy.");
                    AddDo("Use Clickable mode only when the entire card is a single interaction target.");
                    AddDont("Don't mix Clickable cards in dense lists \u2014 use List items instead.");
                    AddDont("Don't use Elevated cards for every item in a scrollable list.");
                    AddGuidelineSub("Content");
                    AddDo("Follow M3 spacing (16dp padding recommended).");
                    AddDo("Use Title Medium for card headers, Body Medium for body text.");
                    AddDont("Don't exceed 2-3 lines of body text without scrolling.");
                    break;

                case "nav-checkbox":
                    AddGuidelineSub("When to use");
                    AddDo("Use for binary (yes/no) selections in forms.");
                    AddDo("Use Indeterminate state for parent items with partially checked children.");
                    AddDo("Group related checkboxes (2-5 options) with a visible label.");
                    AddDont("Don't use as a toggle \u2014 use Switch/Toggle instead.");
                    AddDont("Don't use Indeterminate for uncertain selections \u2014 only for hierarchical scenarios.");
                    AddGuidelineSub("Accessibility");
                    AddDo("Each checkbox should have an associated label.");
                    AddDo("Minimum touch target: 48x48dp (met by wrapper).");
                    break;

                case "nav-chip":
                    AddGuidelineSub("When to use");
                    AddDo("Use Assist chips for smart suggestions or quick actions.");
                    AddDo("Use Filter chips for filtering content (multi-select allowed).");
                    AddDo("Use Input chips for user-entered data (tags, recipients).");
                    AddDo("Use Suggestion chips for dynamically generated recommendations.");
                    AddDont("Don't use chips as buttons for primary actions.");
                    AddDont("Don't use too many chips in a row \u2014 wrap or scroll if needed.");
                    break;

                case "nav-toggle":
                    AddGuidelineSub("When to use");
                    AddDo("Use for binary on/off settings that take effect immediately.");
                    AddDo("Place label to the left of the toggle.");
                    AddDont("Don't use for form submissions that require a Save action \u2014 use Checkbox.");
                    AddDont("Don't use toggle for multi-option selections.");
                    AddGuidelineSub("Accessibility");
                    AddDo("Visual state change (thumb size + color) provides clear feedback.");
                    AddDo("Minimum touch target: 48dp (met by track width 52dp).");
                    break;

                case "nav-textfield":
                    AddGuidelineSub("When to use");
                    AddDo("Use Filled variant in most forms \u2014 it has stronger visual presence.");
                    AddDo("Use Outlined variant in dense forms or when visual weight should be minimal.");
                    AddDo("Provide helper text for format guidance (e.g., \"MM/DD/YYYY\").");
                    AddDo("Use error state with descriptive ErrorText for validation.");
                    AddDont("Don't leave Label empty \u2014 always provide a visible label.");
                    AddDont("Don't use placeholder text as the only label.");
                    break;

                case "nav-slider":
                    AddGuidelineSub("When to use");
                    AddDo("Use continuous mode for values that don't need precision (e.g., volume).");
                    AddDo("Use discrete mode (Step > 0) when specific values matter.");
                    AddDo("Show value label when exact values help the user.");
                    AddDont("Don't use slider for text input or date selection.");
                    AddDont("Don't set a range too large for discrete steps.");
                    break;

                default:
                    AddDo("Follow M3 spec sizing and spacing.");
                    AddDo("Use USS tokens for all colors and shapes.");
                    AddDo("Extend M3ComponentBase for new components.");
                    var note = new Label("Component-specific guidelines coming soon.");
                    note.AddToClassList("m3-body-medium");
                    note.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                    note.style.marginTop = 16f;
                    _tabContent.Add(note);
                    break;
            }
        }

        // ------------------------------------------------------------------ //
        //  Accessibility tab                                                  //
        // ------------------------------------------------------------------ //

        private void BuildAccessibilityTab(SectionData section)
        {
            AddSectionHeader($"{section.Name} — Accessibility");
            var body = new Label("Accessibility documentation is coming in a future update. Follow Unity UI Toolkit's built-in focusable API and ARIA-equivalent patterns from the M3 spec.");
            body.AddToClassList("m3-body-medium");
            body.style.whiteSpace = WhiteSpace.Normal;
            _tabContent.Add(body);
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private void AddSectionHeader(string text)
        {
            var h = new Label(text);
            h.AddToClassList("m3-headline-small");
            h.style.marginBottom = 16f;
            _tabContent.Add(h);
        }

        private void AddSpecSubheader(string text)
        {
            var h = new Label(text);
            h.AddToClassList("m3-title-small");
            h.style.marginTop = 20f;
            h.style.marginBottom = 8f;
            _tabContent.Add(h);
        }

        private void AddGuidelineSub(string text)
        {
            var h = new Label(text);
            h.AddToClassList("m3-title-small");
            h.style.marginTop = 16f;
            h.style.marginBottom = 8f;
            _tabContent.Add(h);
        }

        private void AddDemoLabel(VisualElement area, string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("m3-label-large");
            lbl.style.marginTop = 16f;
            lbl.style.marginBottom = 8f;
            lbl.style.color = new StyleColor(new Color(0.45f, 0.43f, 0.49f));
            area.Add(lbl);
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static void AddLabeledControl(VisualElement parent, string label, VisualElement control)
        {
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Row;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.marginRight = 24f;
            wrapper.style.marginBottom = 8f;
            wrapper.Add(control);
            var lbl = new Label(label);
            lbl.AddToClassList("m3-body-medium");
            lbl.style.marginLeft = 8f;
            wrapper.Add(lbl);
            parent.Add(wrapper);
        }

        private void AddSpecRow(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("showcase-spec-row");
            var keyLabel = new Label(key);
            keyLabel.AddToClassList("showcase-spec-row__key");
            keyLabel.AddToClassList("m3-body-small");
            row.Add(keyLabel);
            var valueLabel = new Label(value);
            valueLabel.AddToClassList("showcase-spec-row__value");
            valueLabel.AddToClassList("m3-body-medium");
            row.Add(valueLabel);
            _tabContent.Add(row);
        }

        private void AddSpecTable(string[] headers, params string[][] rows)
        {
            var table = new VisualElement();
            table.AddToClassList("showcase-spec-table");

            // Header row
            var headerRow = new VisualElement();
            headerRow.AddToClassList("showcase-spec-table-header");
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = new Label(headers[i]);
                cell.AddToClassList("showcase-spec-cell");
                cell.AddToClassList("m3-label-medium");
                if (i == 0) cell.AddToClassList("showcase-spec-cell--name");
                else if (i == 1) cell.AddToClassList("showcase-spec-cell--type");
                else cell.AddToClassList("showcase-spec-cell--default");
                headerRow.Add(cell);
            }
            table.Add(headerRow);

            // Data rows
            foreach (var rowData in rows)
            {
                var dataRow = new VisualElement();
                dataRow.AddToClassList("showcase-spec-table-row");
                for (int i = 0; i < rowData.Length; i++)
                {
                    var cell = new Label(rowData[i]);
                    cell.AddToClassList("showcase-spec-cell");
                    cell.AddToClassList("m3-body-small");
                    if (i == 0) cell.AddToClassList("showcase-spec-cell--name");
                    else if (i == 1) cell.AddToClassList("showcase-spec-cell--type");
                    else cell.AddToClassList("showcase-spec-cell--default");
                    dataRow.Add(cell);
                }
                table.Add(dataRow);
            }

            _tabContent.Add(table);
        }

        private void AddDo(string text)
        {
            var item = new VisualElement();
            item.AddToClassList("showcase-do-item");
            var icon = new Label("\ue876");
            icon.AddToClassList("m3-icon");
            icon.AddToClassList("showcase-do-icon");
            icon.AddToClassList("showcase-do-icon--do");
            item.Add(icon);
            var lbl = new Label(text);
            lbl.AddToClassList("m3-body-medium");
            lbl.style.whiteSpace = WhiteSpace.Normal;
            lbl.style.flexGrow = 1f;
            item.Add(lbl);
            _tabContent.Add(item);
        }

        private void AddDont(string text)
        {
            var item = new VisualElement();
            item.AddToClassList("showcase-do-item");
            var icon = new Label("\ue5cd");
            icon.AddToClassList("m3-icon");
            icon.AddToClassList("showcase-do-icon");
            icon.AddToClassList("showcase-do-icon--dont");
            item.Add(icon);
            var lbl = new Label(text);
            lbl.AddToClassList("m3-body-medium");
            lbl.style.whiteSpace = WhiteSpace.Normal;
            lbl.style.flexGrow = 1f;
            item.Add(lbl);
            _tabContent.Add(item);
        }
    }
}
