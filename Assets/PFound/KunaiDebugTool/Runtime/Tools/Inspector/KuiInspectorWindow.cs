using System.Collections.Generic;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// SROptions-analog: walks every <c>[KuiOption]</c> static field/property
    /// in the AppDomain (via <see cref="KuiReflectionScanner"/>) and renders
    /// them grouped by <c>[KuiCategory]</c>. Each row picks a renderer based
    /// on the member's type (Toggle / Slider / stepper / cycle / Label).
    /// </summary>
    public class KuiInspectorWindow : KuWindow
    {
        public override string Title => KuiIcons.Cog + " Inspector";

        readonly KuiOptionRegistry _registry = new();
        readonly Dictionary<string, bool> _expanded = new(System.StringComparer.Ordinal);
        bool   _scanned;
        Vector2 _scroll;

        // Caller-supplied widget id base for collapsible headers. Must be
        // unique-per-window; categories get id = base + index.
        const int CollapsibleIdBase = 9301;

        public override void Initialize()
        {
            WindowRect = new Rect(20, 760, 380, 400);
        }

        public override void OnRenderUI()
        {
            if (!_scanned)
            {
                _scanned = true;
                _registry.BuildFromScan();
                if (_registry.Count == 0)
                    KUI.Label("No [KuiOption]-tagged static members found.");
            }

            var sh = KUI.BeginScroll(ref _scroll);
            for (int c = 0; c < _registry.Categories.Count; c++)
            {
                var cat = _registry.Categories[c];
                if (!_expanded.TryGetValue(cat, out bool open)) open = true;
                bool prevOpen = open;
                if (KUI.BeginCollapsible(CollapsibleIdBase + c, cat, ref open))
                {
                    var rows = _registry.InCategory(cat);
                    for (int i = 0; i < rows.Count; i++)
                        KuiOptionRow.Draw(rows[i]);
                }
                KUI.EndCollapsible();
                if (open != prevOpen) _expanded[cat] = open;
            }
            KUI.EndScroll(ref _scroll, sh);
        }
    }
}
