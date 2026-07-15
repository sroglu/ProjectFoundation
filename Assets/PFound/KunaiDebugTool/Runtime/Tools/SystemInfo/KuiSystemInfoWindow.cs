using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Read-only system data dump grouped into 5 collapsible sections
    /// (Hardware, OS, Graphics, App, Runtime). Each row is read inside a
    /// per-field try/catch so a single broken accessor renders <c>n/a</c>
    /// without breaking siblings. Refresh button re-snapshots everything
    /// (e.g., after a game-view resize). The same data is also exposed as
    /// a multi-line <c>key: value</c> string via <see cref="DumpAsText"/>
    /// for D8's bug-report payload.
    /// </summary>
    public class KuiSystemInfoWindow : KuWindow
    {
        public override string Title => KuiIcons.Info + " System Info";

        const int CollapsibleIdBase = 9401;

        readonly Dictionary<string, bool> _expanded = new(StringComparer.Ordinal);
        readonly List<Section> _sections = new();
        Vector2 _scroll;
        bool    _initialised;

        struct Section
        {
            public string Title;
            public List<(string key, string value)> Rows;
        }

        public override void Initialize()
        {
            WindowRect = new Rect(420, 760, 380, 320);
            Snapshot();
            _initialised = true;
        }

        public override void OnRenderUI()
        {
            if (!_initialised) return;

            if (KUI.Button(KuiIcons.Refresh + " Refresh"))
                Snapshot();

            KUI.Separator();

            var sh = KUI.BeginScroll(ref _scroll);
            for (int i = 0; i < _sections.Count; i++)
            {
                var sec = _sections[i];
                if (!_expanded.TryGetValue(sec.Title, out bool open)) open = true;
                bool prev = open;
                if (KUI.BeginCollapsible(CollapsibleIdBase + i, sec.Title, ref open))
                {
                    for (int r = 0; r < sec.Rows.Count; r++)
                        KUI.Label("  " + sec.Rows[r].key + ": " + sec.Rows[r].value);
                }
                KUI.EndCollapsible();
                if (open != prev) _expanded[sec.Title] = open;
            }
            KUI.EndScroll(ref _scroll, sh);
        }

        // ---- snapshot ----------------------------------------------------------

        void Snapshot()
        {
            _sections.Clear();
            _sections.Add(BuildHardware());
            _sections.Add(BuildOS());
            _sections.Add(BuildGraphics());
            _sections.Add(BuildApp());
            _sections.Add(BuildRuntime());
        }

        static Section BuildHardware()
        {
            var s = new Section { Title = "Hardware", Rows = new List<(string, string)>() };
            s.Rows.Add(("CPU",         Try(() => SystemInfo.processorType)));
            s.Rows.Add(("Cores",       Try(() => SystemInfo.processorCount.ToString())));
            s.Rows.Add(("CPU MHz",     Try(() => SystemInfo.processorFrequency.ToString())));
            s.Rows.Add(("RAM (MB)",    Try(() => SystemInfo.systemMemorySize.ToString())));
            s.Rows.Add(("GPU",         Try(() => SystemInfo.graphicsDeviceName)));
            s.Rows.Add(("GPU vendor",  Try(() => SystemInfo.graphicsDeviceVendor)));
            s.Rows.Add(("VRAM (MB)",   Try(() => SystemInfo.graphicsMemorySize.ToString())));
            return s;
        }

        static Section BuildOS()
        {
            var s = new Section { Title = "OS", Rows = new List<(string, string)>() };
            s.Rows.Add(("OS",          Try(() => SystemInfo.operatingSystem)));
            s.Rows.Add(("OS family",   Try(() => SystemInfo.operatingSystemFamily.ToString())));
            s.Rows.Add(("Locale",      Try(() => System.Globalization.CultureInfo.CurrentCulture.Name)));
            s.Rows.Add(("Device model", Try(() => SystemInfo.deviceModel)));
            s.Rows.Add(("Device type",  Try(() => SystemInfo.deviceType.ToString())));
            return s;
        }

        static Section BuildGraphics()
        {
            var s = new Section { Title = "Graphics", Rows = new List<(string, string)>() };
            s.Rows.Add(("API",          Try(() => SystemInfo.graphicsDeviceType.ToString())));
            s.Rows.Add(("Shader model", Try(() => SystemInfo.graphicsShaderLevel.ToString())));
            s.Rows.Add(("Screen res",   Try(() => Screen.width + " x " + Screen.height)));
            s.Rows.Add(("DPI",          Try(() => Screen.dpi.ToString("F1"))));
            s.Rows.Add(("Refresh Hz",   Try(() => Screen.currentResolution.refreshRateRatio.value.ToString("F1"))));
            s.Rows.Add(("Fullscreen",   Try(() => Screen.fullScreen.ToString())));
            return s;
        }

        static Section BuildApp()
        {
            var s = new Section { Title = "App", Rows = new List<(string, string)>() };
            s.Rows.Add(("Unity",       Try(() => Application.unityVersion)));
            s.Rows.Add(("Version",     Try(() => Application.version)));
            s.Rows.Add(("Identifier",  Try(() => Application.identifier)));
            s.Rows.Add(("Platform",    Try(() => Application.platform.ToString())));
            s.Rows.Add(("Editor",      Try(() => Application.isEditor.ToString())));
            s.Rows.Add(("Genuine",     Try(() => Application.genuineCheckAvailable ? Application.genuine.ToString() : "n/a")));
            return s;
        }

        static Section BuildRuntime()
        {
            var s = new Section { Title = "Runtime", Rows = new List<(string, string)>() };
            s.Rows.Add(("Uptime",         Try(() => Time.realtimeSinceStartup.ToString("F1") + " s")));
            s.Rows.Add(("Frame",          Try(() => Time.frameCount.ToString())));
            s.Rows.Add(("Active scene",   Try(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)));
            s.Rows.Add(("Loaded scenes",  Try(() => UnityEngine.SceneManagement.SceneManager.sceneCount.ToString())));
            s.Rows.Add(("System lang",    Try(() => Application.systemLanguage.ToString())));
            return s;
        }

        // Converts any thrown exception into "n/a" so a broken accessor on one
        // platform doesn't blank out the whole row.
        static string Try(Func<string> f)
        {
            try { var v = f(); return string.IsNullOrEmpty(v) ? "n/a" : v; }
            catch { return "n/a"; }
        }

        /// <summary>
        /// Multi-line <c>key: value</c> dump, one section per heading. Used
        /// by D8's bug reporter to ship a snapshot alongside the screenshot.
        /// Forces a fresh snapshot before formatting so the dump is current.
        /// </summary>
        public static string DumpAsText()
        {
            var w = new KuiSystemInfoWindow();
            w.Snapshot();
            var sb = new StringBuilder(2048);
            for (int i = 0; i < w._sections.Count; i++)
            {
                var sec = w._sections[i];
                sb.Append("[").Append(sec.Title).Append("]\n");
                for (int r = 0; r < sec.Rows.Count; r++)
                    sb.Append(sec.Rows[r].key).Append(": ").Append(sec.Rows[r].value).Append('\n');
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
