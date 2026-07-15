using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Toolbox window — single on/off control surface for every other window.
    /// Lists each registered <see cref="KuWindow"/> whose
    /// <see cref="KuWindow.ShowInMasterToggle"/> returns true and renders a
    /// toggle that drives <see cref="KuiWindowState.IsVisible"/>.
    ///
    /// Persistence (PlayerPrefs, per-window keyed by sanitised title):
    /// <list type="bullet">
    ///   <item><c>Kunai.Master.Vis.&lt;title&gt;</c> — visibility bit.</item>
    ///   <item><c>Kunai.Master.Rect.&lt;title&gt;.{x,y,w,h}</c> — last
    ///         drag/resize position. Saved one frame after drag end so the
    ///         per-frame compare is just four float equality checks.</item>
    /// </list>
    ///
    /// On first frame both keys are loaded and applied; rects are clamped into
    /// the current screen so a saved-then-resolution-changed rect snaps back.
    /// Console is pinned by overriding <see cref="ShowInMasterToggle"/> to
    /// false on its window — it never appears in this list and stays visible.
    /// </summary>
    public class KuiMasterWindow : KuWindow
    {
        public override string Title => KuiIcons.Cog + " Toolbox";
        public override bool ShowInMasterToggle => false;

        const string PrefKeyPrefix    = "Kunai.Master.Vis.";
        const string RectKeyPrefix    = "Kunai.Master.Rect.";

        bool    _appliedFirstFrame;
        Vector2 _scroll;
        float   _pendingScale = -1f;   // live slider value; committed to KuiDPI only on knob-release

        // Cache of last-saved rect per window-id. Used to detect drag/resize
        // end without subscribing to the manager: when the rect differs from
        // the cache and no drag is currently in progress, the window settled
        // — save and refresh the cache.
        readonly Dictionary<int, Rect> _lastSavedRect = new();

        public override void Initialize()
        {
            // Top-left narrow column. Tall enough to fit ~8 toggles without
            // scrolling at typical desktop DPI; the inner scroll handles
            // overflow when more windows are registered or the user shrinks
            // the panel.
            WindowRect = new Rect(20, 20, 220, 540);
        }

        public override void OnRenderUI()
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            // First-frame: read every saved pref and apply to the matching
            // registered window. Run once — subsequent frames just render
            // toggles whose state already reflects user choice.
            if (!_appliedFirstFrame)
            {
                _appliedFirstFrame = true;
                ApplyPrefsToAll(ctx);
            }

            // Persist rect changes from prior-frame drag/resize. Done before
            // rendering so the toggle row reflects the freshly-saved layout.
            PersistRectChanges(ctx);

            // Overall overlay size. All scale math lives in KuiDPI — this only reads/writes the user
            // multiplier through it. Commit ONLY on knob-release: applying every drag frame rescales
            // the whole overlay (this slider included) live under the finger. Drag previews the value;
            // release commits the real rescale once.
            if (_pendingScale < 0f) _pendingScale = ctx.Settings.UserScale;
            KUI.Label($"UI Scale: {Mathf.RoundToInt(_pendingScale * 100f)}%");
            _pendingScale = KUI.Slider(_pendingScale, KuiDPI.MinUserScale, KuiDPI.MaxUserScale);
            if (ctx.InputHandler.State.MouseUp && !Mathf.Approximately(_pendingScale, ctx.Settings.UserScale))
                KuiDPI.SetUserScale(ctx.Settings, _pendingScale);
            KUI.Separator();

            KUI.Label("Windows");
            KUI.Separator();

            // Scroll wraps the toggle list so the master stays compact even
            // with many registered windows; the user can resize the window
            // and the chrome plus scroll handle whatever count is registered.
            var sh = KUI.BeginScroll(ref _scroll);
            for (int i = 0; i < ctx.Windows.Count; i++)
            {
                var w = ctx.Windows[i];
                if (!w.ShowInMasterToggle) continue;

                var state = ctx.WindowStates[i];
                bool prev = state.IsVisible;
                bool next = KUI.Toggle(prev, w.Title);
                if (next != prev)
                {
                    state.IsVisible = next;
                    if (next) state.Rect = ClampToScreen(state.Rect);
                    ctx.WindowStates[i] = state;
                    SaveVisibility(w.Title, next);
                }
            }
            KUI.Label(KuiIcons.Pin + " Console (pinned)", KuiStyles.TextDim);
            KUI.EndScroll(ref _scroll, sh);
        }

        // ---- pref load/save ---------------------------------------------------

        void ApplyPrefsToAll(KuiContext ctx)
        {
            for (int i = 0; i < ctx.Windows.Count; i++)
            {
                var w = ctx.Windows[i];
                var state = ctx.WindowStates[i];

                // Rect prefs apply to ALL master-tracked windows including the
                // pinned Console — the toggle is what we hide for pinned windows,
                // not the persistence. Keeps the user's layout coherent.
                if (TryLoadRect(w.Title, out var savedRect))
                {
                    state.Rect = ClampToScreen(savedRect);
                }

                if (w.ShowInMasterToggle)
                {
                    string visKey = VisKeyFor(w.Title);
                    if (PlayerPrefs.HasKey(visKey))
                    {
                        state.IsVisible = PlayerPrefs.GetInt(visKey) != 0;
                    }
                    // Always clamp visible windows on first apply so a window
                    // saved in a position that's now off-screen (e.g. resolution
                    // changed between sessions) snaps back into view.
                    if (state.IsVisible) state.Rect = ClampToScreen(state.Rect);
                }

                ctx.WindowStates[i] = state;
                _lastSavedRect[state.Id] = state.Rect;
            }
        }

        // Per-frame check: any window whose Rect drifted from the cached
        // last-saved snapshot AND that is not currently being dragged gets its
        // new rect committed to PlayerPrefs. The IsDragging gate batches all
        // intra-drag changes into a single save the frame the drag ends.
        void PersistRectChanges(KuiContext ctx)
        {
            if (KuiWindowManager.IsDragging) return;

            bool anyChange = false;
            for (int i = 0; i < ctx.Windows.Count; i++)
            {
                var w = ctx.Windows[i];
                var state = ctx.WindowStates[i];
                var cached = _lastSavedRect.TryGetValue(state.Id, out var c) ? c : default;
                if (cached == state.Rect) continue;

                _lastSavedRect[state.Id] = state.Rect;
                WriteRectKeys(w.Title, state.Rect);
                anyChange = true;
            }
            if (anyChange) PlayerPrefs.Save();
        }

        static void SaveVisibility(string title, bool value)
        {
            PlayerPrefs.SetInt(VisKeyFor(title), value ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void WriteRectKeys(string title, Rect r)
        {
            string baseKey = RectKeyFor(title);
            PlayerPrefs.SetFloat(baseKey + ".x", r.x);
            PlayerPrefs.SetFloat(baseKey + ".y", r.y);
            PlayerPrefs.SetFloat(baseKey + ".w", r.width);
            PlayerPrefs.SetFloat(baseKey + ".h", r.height);
        }

        static bool TryLoadRect(string title, out Rect r)
        {
            string baseKey = RectKeyFor(title);
            if (!PlayerPrefs.HasKey(baseKey + ".x"))
            {
                r = default;
                return false;
            }
            r = new Rect(
                PlayerPrefs.GetFloat(baseKey + ".x"),
                PlayerPrefs.GetFloat(baseKey + ".y"),
                PlayerPrefs.GetFloat(baseKey + ".w"),
                PlayerPrefs.GetFloat(baseKey + ".h"));
            return true;
        }

        // Title strings include Nerd Font glyphs (chars > 127). PlayerPrefs
        // tolerates UTF-8 keys but stripping to ASCII keeps prefs files readable.
        static string VisKeyFor(string title)  => BuildKey(PrefKeyPrefix, title);
        static string RectKeyFor(string title) => BuildKey(RectKeyPrefix, title);

        static string BuildKey(string prefix, string title)
        {
            var sb = new StringBuilder(prefix.Length + 32);
            sb.Append(prefix);
            for (int i = 0; i < title.Length; i++)
            {
                char c = title[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                 || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        // ---- ensure-in-view ---------------------------------------------------

        // Shrinks oversized windows and snaps the rect's origin so the entire
        // rect is inside (0,0,Screen.width,Screen.height). Cheap; runs only on
        // toggle-on transitions and during first-frame pref apply.
        static Rect ClampToScreen(Rect r)
        {
            float w = Screen.width;
            float h = Screen.height;
            if (r.width  > w) r.width  = w;
            if (r.height > h) r.height = h;
            r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, w - r.width));
            r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, h - r.height));
            return r;
        }
    }
}
