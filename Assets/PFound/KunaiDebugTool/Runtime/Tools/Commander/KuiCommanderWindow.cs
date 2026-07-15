using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Reflection-driven REPL window. <c>[KuiCommand("name")]</c> static
    /// methods are auto-discovered at first render via
    /// <see cref="KuiCommandRegistry.BuildFromScan"/>. Type a command, optionally
    /// press Tab to complete to the longest-common prefix among matches, and
    /// Enter to execute. Up/Down arrows walk recently-entered commands.
    ///
    /// Output pane is isolated from the main Console — exceptions thrown by
    /// commands render here in red without polluting Console with stack noise.
    /// </summary>
    public class KuiCommanderWindow : KuWindow
    {
        public override string Title => KuiIcons.Cog + " Commander";

        const int InputFieldId = 9201;

        const int    MaxOutputLines  = 200;
        const float  RowHeight       = 18f;
        const float  PromptHeight    = 24f;
        const float  SuggestionHeight = 18f;
        const int    MaxSuggestions  = 6;

        readonly KuiCommandRegistry _registry = new();
        readonly KuiCommandHistory  _history  = new();
        readonly List<OutputLine>   _output   = new();
        readonly List<string>       _tokens   = new();
        readonly List<string>       _suggestions = new(MaxSuggestions);

        KuiTextFieldState _input;
        Vector2           _scroll;
        bool              _scanned;

        struct OutputLine
        {
            public string Text;
            public Color32 Color;
        }

        public override void Initialize()
        {
            WindowRect = new Rect(800, 80, 520, 360);
        }

        public override void OnRenderUI()
        {
            // Lazy first-time scan. Done in OnRenderUI rather than Initialize
            // so user assemblies that load late (addressables, dynamic dlls)
            // are still picked up before the user opens the window.
            if (!_scanned)
            {
                _scanned = true;
                var r = _registry.BuildFromScan();
                AppendOutput($"Commander ready — {_registry.Count} command(s) registered.", KuiStyles.TextDim);
                if (r.Errors != null && r.Errors.Count > 0)
                    AppendOutput($"({r.Errors.Count} reflection error(s) — see Console)", KuiStyles.TextDim);
            }

            // Input row.
            bool entered = KUI.TextField(InputFieldId, ref _input);

            // History navigation only fires while the input field has focus.
            // Reading raw GetKeyDown is fine because the focus model already
            // arbitrated the keystroke for us.
            if (KuiInputFocus.IsFocused(InputFieldId))
            {
                if (KuiInput.GetKeyDown(KeyCode.UpArrow))   ApplyHistory(_history.Previous());
                if (KuiInput.GetKeyDown(KeyCode.DownArrow)) ApplyHistory(_history.Next());
                if (KuiInput.GetKeyDown(KeyCode.Tab))       TryComplete();
            }

            if (entered) ExecuteCurrentLine();

            // Suggestion strip — only when there's a non-empty prompt and at
            // least one match. Renders BEFORE the output pane so it doesn't
            // shift output rows when it appears/disappears.
            BuildSuggestions();
            if (_suggestions.Count > 0) DrawSuggestions();

            KUI.Separator();

            // Output pane — newest at the bottom (REPL convention).
            var sh = KUI.BeginScroll(ref _scroll);
            for (int i = 0; i < _output.Count; i++)
                DrawOutputRow(_output[i]);
            KUI.EndScroll(ref _scroll, sh);
        }

        // ---- input plumbing ----------------------------------------------------

        void ApplyHistory(string line)
        {
            if (line == null) return;
            _input.SetText(line);
        }

        void TryComplete()
        {
            string current = _input.GetText();
            // Only complete the head token (no per-arg completion in v1).
            int firstSpace = current.IndexOf(' ');
            string head = firstSpace < 0 ? current : current.Substring(0, firstSpace);
            if (string.IsNullOrEmpty(head)) return;

            string longest = LongestCommonPrefix(head);
            if (string.IsNullOrEmpty(longest)) return;
            if (longest.Length <= head.Length) return;   // no progress

            string tail = firstSpace < 0 ? string.Empty : current.Substring(firstSpace);
            _input.SetText(longest + tail);
        }

        string LongestCommonPrefix(string head)
        {
            string lcp = null;
            foreach (var name in _registry.Match(head))
            {
                if (lcp == null) { lcp = name; continue; }
                int n = 0;
                int max = math.min(lcp.Length, name.Length);
                while (n < max && char.ToLowerInvariant(lcp[n]) == char.ToLowerInvariant(name[n])) n++;
                if (n == 0) return head;
                lcp = lcp.Substring(0, n);
            }
            return lcp ?? head;
        }

        void BuildSuggestions()
        {
            _suggestions.Clear();
            string current = _input.GetText();
            int firstSpace = current.IndexOf(' ');
            string head = firstSpace < 0 ? current : current.Substring(0, firstSpace);
            if (string.IsNullOrEmpty(head)) return;

            int n = 0;
            foreach (var match in _registry.Match(head))
            {
                _suggestions.Add(match);
                if (++n >= MaxSuggestions) break;
            }
        }

        void DrawSuggestions()
        {
            for (int i = 0; i < _suggestions.Count; i++)
            {
                var ctx = KuiContext.Instance;
                if (ctx == null) return;

                float h = KuiDPI.Px(SuggestionHeight);
                float4 rect = ctx.Layout.NextRect(h);
                float4 clip = ctx.Layout.CurrentClip;
                ctx.CommandBuffer.PushRect(rect, KuiStyles.SliderTrack, clip);

                float pad = KuiDPI.Px(KuiStyles.Padding);
                ctx.CommandBuffer.PushLabel(_suggestions[i],
                    new float4(rect.x + pad, rect.y + KuiDPI.Px(2f), rect.z - pad * 2, h),
                    KuiStyles.TextDim, clip);
            }
        }

        // ---- execution ---------------------------------------------------------

        void ExecuteCurrentLine()
        {
            string line = _input.GetText();
            _input.Clear();

            if (string.IsNullOrWhiteSpace(line)) return;
            _history.Push(line);
            AppendOutput("> " + line, KuiStyles.Text);

            if (!KuiCommandParser.TryTokenize(line, _tokens, out string tokErr))
            {
                AppendOutput("error: " + tokErr, ErrorColor);
                return;
            }
            if (_tokens.Count == 0) return;

            string name = _tokens[0];
            if (!_registry.TryGet(name, out var entry))
            {
                AppendOutput("unknown command: " + name, ErrorColor);
                return;
            }

            if (!KuiCommandParser.TryBind(entry, _tokens, firstArg: 1, out var boxed, out string bindErr))
            {
                AppendOutput("error: " + bindErr, ErrorColor);
                if (!string.IsNullOrEmpty(entry.Help))
                    AppendOutput("usage: " + entry.Help, KuiStyles.TextDim);
                return;
            }

            object ret;
            try
            {
                ret = entry.Method.Invoke(null, boxed);
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                AppendOutput($"exception: {inner.GetType().Name}: {inner.Message}", ErrorColor);
                return;
            }
            catch (Exception ex)
            {
                AppendOutput($"exception: {ex.GetType().Name}: {ex.Message}", ErrorColor);
                return;
            }

            if (entry.Method.ReturnType == typeof(void))
                AppendOutput("OK", KuiStyles.Text);
            else
                AppendOutput(ret == null ? "null" : ret.ToString(), KuiStyles.Text);
        }

        void AppendOutput(string text, Color32 color)
        {
            if (_output.Count >= MaxOutputLines) _output.RemoveAt(0);
            _output.Add(new OutputLine { Text = text, Color = color });
        }

        void DrawOutputRow(OutputLine row)
        {
            var ctx = KuiContext.Instance;
            if (ctx == null) return;
            float h = KuiDPI.Px(RowHeight);
            float4 rect = ctx.Layout.NextRect(h);
            float4 clip = ctx.Layout.CurrentClip;
            float pad = KuiDPI.Px(KuiStyles.Padding);
            ctx.CommandBuffer.PushLabel(row.Text ?? string.Empty,
                new float4(rect.x + pad, rect.y + KuiDPI.Px(2f), rect.z - pad * 2, h),
                row.Color, clip);
        }

        static readonly Color32 ErrorColor = new(230, 80, 80, 255);
    }
}
