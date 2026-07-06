#if UNITY_INCLUDE_TESTS
using PFound.UISystem.Shapes;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace PFound.UISystem.Tests.Shapes
{
    /// <summary>
    /// EditMode tests for <see cref="ThemedSdfPanel"/> + <see cref="ActiveMaterialTheme"/>.
    /// Direct API contract validation. Panel attach/detach lifecycle is exercised by the
    /// PlayMode batching test + manual showcase visual review — not duplicated here because
    /// AttachToPanelEvent doesn't fire without a UIDocument host (and this asmdef doesn't
    /// reference UnityEditor.EditorWindow).
    /// </summary>
    public class ThemedSdfPanelTests
    {
        private Shader _shader;
        private Material _m3Card;
        private Material _papercutCard;
        private ThemeMaterialRegistry _m3Registry;
        private ThemeMaterialRegistry _papercutRegistry;

        private static ThemeMaterialRegistry MakeRegistry(string name, params (string Key, Material Material)[] entries)
        {
            var reg = ScriptableObject.CreateInstance<ThemeMaterialRegistry>();
            reg.hideFlags = HideFlags.DontSave;
            reg.ThemeName = name;
            var field = typeof(ThemeMaterialRegistry).GetField("_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var list = new System.Collections.Generic.List<ThemeMaterialRegistry.Entry>();
            foreach (var (k, m) in entries)
                list.Add(new ThemeMaterialRegistry.Entry { Key = k, Material = m });
            field.SetValue(reg, list);
            return reg;
        }

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find("UISystem/Shape");
            _m3Card = new Material(_shader) { hideFlags = HideFlags.DontSave, name = "M3CardMat" };
            _papercutCard = new Material(_shader) { hideFlags = HideFlags.DontSave, name = "PapercutCardMat" };
            _m3Registry = MakeRegistry("M3", ("card", _m3Card));
            _papercutRegistry = MakeRegistry("Papercut", ("card", _papercutCard));
        }

        [TearDown]
        public void TearDown()
        {
            ActiveMaterialTheme.Active = null;
            if (_m3Registry != null) Object.DestroyImmediate(_m3Registry);
            if (_papercutRegistry != null) Object.DestroyImmediate(_papercutRegistry);
            if (_m3Card != null) Object.DestroyImmediate(_m3Card);
            if (_papercutCard != null) Object.DestroyImmediate(_papercutCard);
        }

        [Test]
        public void ActiveMaterialTheme_OnChanged_FiresOnceOnAssign()
        {
            int called = 0;
            System.Action<ThemeMaterialRegistry> handler = _ => called++;
            ActiveMaterialTheme.OnChanged += handler;
            try
            {
                ActiveMaterialTheme.Active = _m3Registry;
                Assert.AreEqual(1, called);

                // Same value re-assignment is a no-op.
                ActiveMaterialTheme.Active = _m3Registry;
                Assert.AreEqual(1, called);

                // Different value fires again.
                ActiveMaterialTheme.Active = _papercutRegistry;
                Assert.AreEqual(2, called);
            }
            finally
            {
                ActiveMaterialTheme.OnChanged -= handler;
            }
        }

        [Test]
        public void ActiveMaterialTheme_Get_ReturnsActiveRegistryMaterial()
        {
            ActiveMaterialTheme.Active = _m3Registry;
            Assert.AreSame(_m3Card, ActiveMaterialTheme.Get("card"));
            ActiveMaterialTheme.Active = _papercutRegistry;
            Assert.AreSame(_papercutCard, ActiveMaterialTheme.Get("card"));
        }

        [Test]
        public void ActiveMaterialTheme_NullActive_GetReturnsNull()
        {
            ActiveMaterialTheme.Active = null;
            Assert.IsNull(ActiveMaterialTheme.Get("card"));
            Assert.IsNull(ActiveMaterialTheme.Get(""));
            Assert.IsNull(ActiveMaterialTheme.Get(null));
        }

        [Test]
        public void ThemedSdfPanel_Default_HasUssClass()
        {
            var panel = new ThemedSdfPanel();
            CollectionAssert.Contains(panel.GetClasses(), ThemedSdfPanel.ussClassName);
            // SdfPanel's class is also still applied (inheritance preserves base class list).
            CollectionAssert.Contains(panel.GetClasses(), SdfPanel.ussClassName);
        }

        [Test]
        public void ThemedSdfPanel_ThemeKeySetter_StoresValue()
        {
            var panel = new ThemedSdfPanel();
            panel.ThemeKey = "card";
            Assert.AreEqual("card", panel.ThemeKey);
        }

        [Test]
        public void ThemedSdfPanel_ManualResolve_BindsActiveThemeMaterial()
        {
            // The panel resolves on attach normally; in EditMode we can drive the same code
            // path via reflection-invoke of the private ResolveFromTheme() helper, which is
            // also called from the AttachToPanelEvent + theme-change handlers.
            ActiveMaterialTheme.Active = _m3Registry;
            var panel = new ThemedSdfPanel { ThemeKey = "card" };
            var m = typeof(ThemedSdfPanel).GetMethod("ResolveFromTheme",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "ResolveFromTheme reflection lookup failed.");
            m.Invoke(panel, null);
            Assert.AreSame(_m3Card, panel.Material);

            // Swap theme + call again.
            ActiveMaterialTheme.Active = _papercutRegistry;
            m.Invoke(panel, null);
            Assert.AreSame(_papercutCard, panel.Material);
        }

        [Test]
        public void ThemedSdfPanel_ChangeThemeKey_TriggersResolve()
        {
            ActiveMaterialTheme.Active = _m3Registry;
            var panel = new ThemedSdfPanel();
            // Setter calls ResolveFromTheme even before the panel is attached. With an active
            // theme this should yield the mapped material immediately.
            panel.ThemeKey = "card";
            Assert.AreSame(_m3Card, panel.Material,
                "Setting ThemeKey should trigger immediate resolve through the active theme.");
        }

        [Test]
        public void ThemedSdfPanel_EmptyKey_NullsMaterial()
        {
            ActiveMaterialTheme.Active = _m3Registry;
            var panel = new ThemedSdfPanel { ThemeKey = "card" };
            Assert.AreSame(_m3Card, panel.Material);

            panel.ThemeKey = "";
            Assert.IsNull(panel.Material,
                "Clearing ThemeKey to empty string should null the material.");
        }
    }
}
#endif
