using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    public class KuiConsoleWindow : KuWindow
    {
        public override string Title => KuiIcons.FileCode + " Console";
        // Console is pinned — the master toolbox does not toggle it off.
        public override bool ShowInMasterToggle => false;
        // NOTE: deliberately NOT StretchHorizontal. That per-frame full-width override fought the
        // window manager — it overwrote x/width every frame AFTER resize/move interaction, so the
        // corner resize handles only changed height (horizontal resize was silently discarded) and the
        // console behaved unlike every other window. We start wide once in Initialize instead, then let
        // the user move/resize it freely like any other window.

        bool _showInfo = true;
        bool _showWarn = true;
        bool _showError = true;
        bool _showVerbose = true;
        bool _autoScroll = true;
        bool _collapseDuplicates;
        int _selectedIndex = -1;
        int _lastBufferCount;       // for pin / new-entry detection
        Vector2 _scroll;
        Vector2 _chipScroll;
        KuiTextFieldState _searchField;

        // States array kept in lockstep with KuiConsole.Categories. Resized
        // lazily in EnsureChipStates so a new category appearing mid-session
        // gets a chip without losing prior toggle state.
        readonly List<bool> _chipStates = new();

        // Reused index list for the visible-row pass — avoids per-frame List
        // allocation. Cleared and refilled each render.
        readonly List<int> _visible = new();

        // Cached static labels — avoids per-frame string allocations.
        static readonly string s_clearLabel    = KuiIcons.Trash + " Clear";
        static readonly string s_pinLabel      = KuiIcons.Pin + " Pin";
        static readonly string s_infoLabel     = KuiIcons.Info + " Info";
        static readonly string s_warnLabel     = KuiIcons.Warning + " Warn";
        static readonly string s_errorLabel    = KuiIcons.Cross + " Error";
        static readonly string s_verboseLabel  = KuiIcons.Eye + " Verbose";
        static readonly string s_collapseLabel = KuiIcons.CircleSolid + " Collapse";

        // Row visual constants
        const float RowHeight = 18f;
        const float StackPaneHeight = 120f;

        // TextField widget id for KuiInputFocus arbitration. Globally unique
        // across all Kunai windows is overkill — within a frame is enough.
        const int SearchFieldId = 9101;
        const int CategoryChipsId = 9102;

        public override void Initialize()
        {
            // Start wide (logs are wide) but as a one-time size — not a per-frame lock. The user can
            // then move/resize it like any other window.
            const float inset = 20f;
            float w = Mathf.Max(360f, Screen.width - inset * 2f);
            WindowRect = new Rect(inset, 520, w, 480);
        }

        public override void OnRenderUI()
        {
            var buffer = KuiConsole.Buffer;
            if (buffer == null)
            {
                KUI.Label("KuiConsole.Initialize() not called.");
                return;
            }
            var categories = KuiConsole.Categories;
            buffer.Drain(categories);

            // Toolbar row: [Clear] [Info] [Warn] [Error] [Pin] [Collapse]
            using (KUI.BeginGroup())
            {
                if (KUI.Button(s_clearLabel, 90f))
                {
                    KuiConsole.Clear();
                    _selectedIndex = -1;
                    _chipStates.Clear();   // reset filter chips with the buffer
                }
                _showInfo           = KUI.Toggle(_showInfo,           s_infoLabel,     80f);
                _showWarn           = KUI.Toggle(_showWarn,           s_warnLabel,     80f);
                _showError          = KUI.Toggle(_showError,          s_errorLabel,    80f);
                _showVerbose        = KUI.Toggle(_showVerbose,        s_verboseLabel,  90f);
                _autoScroll         = KUI.Toggle(_autoScroll,         s_pinLabel,      80f);
                _collapseDuplicates = KUI.Toggle(_collapseDuplicates, s_collapseLabel, 110f);
            }

            // Search box on its own row (full width).
            if (KUI.TextField(SearchFieldId, ref _searchField))
                _selectedIndex = -1;   // committing search resets selection

            // Category chips — only render when at least one was observed.
            // All / None bulk-action buttons live on their own row above the
            // chip strip so the user can flip every chip in one click without
            // scrolling through a long category list.
            if (categories != null && categories.Count > 0)
            {
                EnsureChipStates(categories.Count);
                using (KUI.BeginGroup())
                {
                    if (KUI.Button("All",  60f)) SetAllChips(true);
                    if (KUI.Button("None", 70f)) SetAllChips(false);
                }
                KUI.ChipStrip(CategoryChipsId, categories.Categories, _chipStates, ref _chipScroll);
            }

            KUI.Separator();

            // Build the visible-index list once per frame so we can both
            // collapse it and render it. NEW behaviour: we walk
            // newest-to-oldest because that's how rows render.
            _visible.Clear();
            int total = buffer.Count;
            string searchText = _searchField.GetText();
            for (int i = total - 1; i >= 0; i--)
            {
                ref var entry = ref buffer.GetAt(i);
                if (!IsRowVisible(in entry, categories, searchText)) continue;
                _visible.Add(i);
            }

            // Pin: when ON and the user is currently at the top of the scroll,
            // re-snap to the top exactly when new entries arrive. We don't snap
            // every frame — that would fight the user trying to scroll DOWN to
            // read older entries. New entries arrive at the buffer tail but
            // render at the top of the list (newest-first), so staying at
            // scroll.y=0 keeps the user looking at the latest.
            bool newEntriesArrived = total > _lastBufferCount;
            if (_autoScroll && newEntriesArrived && _scroll.y < KuiDPI.Px(2f))
                _scroll.y = 0f;
            _lastBufferCount = total;

            var sh = KUI.BeginScroll(ref _scroll);
            if (_collapseDuplicates)
            {
                var runs = buffer.CollapseConsecutive(_visible);
                for (int r = 0; r < runs.Count; r++)
                {
                    var span = runs[r];
                    ref var head = ref buffer.GetAt(span.FirstIndex);
                    DrawEntryRow(span.FirstIndex, ref head, span.RunCount);
                }
            }
            else
            {
                for (int v = 0; v < _visible.Count; v++)
                {
                    int idx = _visible[v];
                    ref var entry = ref buffer.GetAt(idx);
                    DrawEntryRow(idx, ref entry, 1);
                }
            }
            KUI.EndScroll(ref _scroll, sh);

            HandleCopyShortcut(buffer);
        }

        // Cmd+C (Mac) / Ctrl+C (Win/Linux) copies the selected entry's
        // message + stack to the system clipboard. Works whether the stack
        // pane is expanded or not.
        void HandleCopyShortcut(KuiLogBuffer buffer)
        {
            if (_selectedIndex < 0 || _selectedIndex >= buffer.Count) return;
            // Don't steal the keypress while the user is typing in search.
            if (KuiInputFocus.IsFocused(SearchFieldId)) return;
            if (!KuiInput.GetKeyDown(KeyCode.C)) return;

            bool mod = KuiInput.GetKey(KeyCode.LeftControl)  || KuiInput.GetKey(KeyCode.RightControl)
                    || KuiInput.GetKey(KeyCode.LeftCommand)  || KuiInput.GetKey(KeyCode.RightCommand);
            if (!mod) return;

            ref var sel = ref buffer.GetAt(_selectedIndex);
            string msg = sel.Message ?? string.Empty;
            string stk = sel.StackTrace ?? string.Empty;
            GUIUtility.systemCopyBuffer = stk.Length > 0 ? msg + "\n" + stk : msg;
        }

        // Called once per render after the chip-list state is known. Grows
        // _chipStates to match the registry's current Count, defaulting new
        // chips to "on" so a freshly observed category isn't immediately
        // hidden.
        void EnsureChipStates(int needed)
        {
            while (_chipStates.Count < needed) _chipStates.Add(true);
            // Shrinking is intentionally not supported — Clear handles reset.
        }

        void SetAllChips(bool value)
        {
            for (int i = 0; i < _chipStates.Count; i++) _chipStates[i] = value;
        }

        bool IsLevelVisible(KuiLogLevel level)
        {
            return level switch
            {
                KuiLogLevel.Info      => _showInfo,
                KuiLogLevel.Warning   => _showWarn,
                KuiLogLevel.Error     => _showError,
                KuiLogLevel.Exception => _showError,
                KuiLogLevel.Verbose   => _showVerbose,
                _                     => true,
            };
        }

        bool IsRowVisible(in KuiLogEntry entry, KuiCategoryRegistry categories, string searchText)
        {
            if (!IsLevelVisible(entry.Level)) return false;

            // Category chip filter — applies only to categorised entries.
            // Uncategorised rows are orthogonal to the chip strip (the strip
            // can't represent "no category") and always pass; use the
            // level-toggle row to hide them by severity.
            //
            // Chip semantics are now authoritative: chip OFF == hide that
            // category. This means clicking "None" actually hides every
            // categorised entry (only uncategorised survive). Previously a
            // "no chip on" shortcut fell through to show-everything, which
            // made the None button a no-op.
            if (categories != null && !string.IsNullOrEmpty(entry.Category))
            {
                int slot = IndexOfCategory(categories, entry.Category);
                if (slot < 0) return false;                  // not in cap → overflowed; hide for v1
                if (slot >= _chipStates.Count) return true;  // chip not yet rendered → show
                if (!_chipStates[slot]) return false;
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                // Case-insensitive substring on Message + Category.
                if (entry.Message != null && entry.Message.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (entry.Category != null && entry.Category.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                return false;
            }

            return true;
        }

        static int IndexOfCategory(KuiCategoryRegistry reg, string cat)
        {
            var list = reg.Categories;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], cat, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        void DrawEntryRow(int index, ref KuiLogEntry entry, int runCount)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float h = KuiDPI.Px(RowHeight);
            float4 rect = ctx.Layout.NextRect(h);
            float4 clip = ctx.Layout.CurrentClip;
            var mouse = ctx.InputHandler.State.MousePosition;

            bool hovered = mouse.x >= rect.x && mouse.x <= rect.x + rect.z
                        && mouse.y >= rect.y && mouse.y <= rect.y + rect.w;
            bool clicked = hovered && ctx.InputHandler.State.IsTap;
            if (clicked) _selectedIndex = (_selectedIndex == index) ? -1 : index;

            // Row background — selection > hover > level-tint > none
            Color32 bg;
            if (_selectedIndex == index)        bg = new Color32(60, 80, 120, 255);
            else if (hovered)                   bg = new Color32(55, 55, 70, 255);
            else                                bg = LevelTint(entry.Level);
            if (bg.a > 0) ctx.CommandBuffer.PushRect(rect, bg, clip);

            // Glyph + level color
            string icon = entry.Level switch
            {
                KuiLogLevel.Warning   => KuiIcons.Warning,
                KuiLogLevel.Error     => KuiIcons.Cross,
                KuiLogLevel.Exception => KuiIcons.Bug,
                KuiLogLevel.Verbose   => KuiIcons.Eye,
                _                     => KuiIcons.Info,
            };
            Color32 fg = entry.Level switch
            {
                KuiLogLevel.Warning   => new Color32(230, 200, 80, 255),
                KuiLogLevel.Error     => new Color32(230, 80, 80, 255),
                KuiLogLevel.Exception => new Color32(230, 80, 80, 255),
                KuiLogLevel.Verbose   => new Color32(140, 140, 150, 255),
                _                     => KuiStyles.Text,
            };

            float pad = KuiDPI.Px(KuiStyles.Padding);
            ctx.CommandBuffer.PushLabel(icon, new float4(rect.x + pad, rect.y + KuiDPI.Px(2f), KuiDPI.Px(20f), h), fg, clip);

            // Optional category badge between icon and message — dim text so it
            // reads like metadata, not the message itself. Tight clip prevents
            // long names (e.g. "[McpToolRegistry]") from spilling into the
            // message column.
            float msgX = rect.x + pad + KuiDPI.Px(22f);
            if (!string.IsNullOrEmpty(entry.Category))
            {
                float catW = KuiDPI.Px(80f);
                float4 badgeRect = new float4(msgX, rect.y + KuiDPI.Px(2f), catW, h);
                float4 badgeClip = IntersectClip(badgeRect, clip);
                ctx.CommandBuffer.PushLabel("[" + entry.Category + "]",
                    badgeRect, KuiStyles.TextDim, badgeClip);
                msgX += catW + KuiDPI.Px(4f);
            }

            // Run-count badge on the right side when collapsed.
            float msgRightTrim = pad;
            if (runCount > 1)
            {
                string badge = "(" + runCount + "×)";
                float badgeW = KuiDPI.Px(50f);
                ctx.CommandBuffer.PushLabel(badge,
                    new float4(rect.x + rect.z - badgeW - pad, rect.y + KuiDPI.Px(2f), badgeW, h),
                    KuiStyles.TextDim, clip);
                msgRightTrim += badgeW + KuiDPI.Px(4f);
            }

            // Row shows only the first line of Message. Multi-line content
            // (e.g. logs that bake stack info into the condition) goes to the
            // stack pane in full. No allocation: just cap the char count.
            string msg = entry.Message;
            int msgLen = msg?.Length ?? 0;
            if (msgLen > 0)
            {
                int nl = msg.IndexOf('\n');
                if (nl >= 0) msgLen = nl;
                ctx.CommandBuffer.PushLabel(msg, 0, msgLen,
                    new float4(msgX, rect.y + KuiDPI.Px(2f), rect.x + rect.z - msgX - msgRightTrim, h),
                    fg, clip);
            }

            // If selected and this is the LAST visible entry, render the stack pane below.
            if (_selectedIndex == index)
                DrawStackPane(ref entry);
        }

        void DrawStackPane(ref KuiLogEntry entry)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float h = KuiDPI.Px(StackPaneHeight);
            float4 rect = ctx.Layout.NextRect(h);
            float4 outerClip = ctx.Layout.CurrentClip;

            // Tight clip = pane rect ∩ outer (scroll) clip. Without this, a
            // multi-line stack trace overflows the pane and bleeds into the
            // next row's hover area.
            float cx0 = math.max(rect.x, outerClip.x);
            float cy0 = math.max(rect.y, outerClip.y);
            float cx1 = math.min(rect.x + rect.z, outerClip.x + outerClip.z);
            float cy1 = math.min(rect.y + rect.w, outerClip.y + outerClip.w);
            float4 tight = new float4(cx0, cy0,
                math.max(0f, cx1 - cx0), math.max(0f, cy1 - cy0));

            ctx.CommandBuffer.PushRect(rect, new Color32(20, 20, 28, 255), outerClip);

            float pad = KuiDPI.Px(KuiStyles.Padding);
            ctx.CommandBuffer.PushLabel(entry.StackTrace ?? "<no stack trace>",
                new float4(rect.x + pad, rect.y + KuiDPI.Px(2f), rect.z - pad * 2, h - pad * 2),
                KuiStyles.TextDim, tight);
        }

        static Color32 LevelTint(KuiLogLevel level)
        {
            switch (level)
            {
                case KuiLogLevel.Warning:   return new Color32(60, 50, 20, 90);
                case KuiLogLevel.Error:
                case KuiLogLevel.Exception: return new Color32(70, 25, 25, 110);
                default:                    return new Color32(0, 0, 0, 0);
            }
        }

        static float4 IntersectClip(float4 a, float4 b)
        {
            float x0 = math.max(a.x, b.x);
            float y0 = math.max(a.y, b.y);
            float x1 = math.min(a.x + a.z, b.x + b.z);
            float y1 = math.min(a.y + a.w, b.y + b.w);
            return new float4(x0, y0, math.max(0f, x1 - x0), math.max(0f, y1 - y0));
        }
    }
}
