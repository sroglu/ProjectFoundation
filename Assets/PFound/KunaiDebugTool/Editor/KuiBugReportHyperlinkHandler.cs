using UnityEditor;
using UnityEngine;

namespace Kunai.Editor
{
    /// <summary>
    /// Intercepts the <c>kunaiBugFolder="..."</c> custom attribute on
    /// <c>&lt;a&gt;</c> tags emitted by <see cref="KuiBugReporterWindow"/> log
    /// lines and routes the click to <see cref="EditorUtility.RevealInFinder"/>.
    /// Unity's default <c>href</c> handler tries <c>Application.OpenURL</c>,
    /// which silently no-ops on plain directory paths — so the bug-report
    /// folder link previously did nothing when clicked. A custom attribute
    /// avoids that fallback path entirely.
    /// </summary>
    [InitializeOnLoad]
    static class KuiBugReportHyperlinkHandler
    {
        const string AttrName = "kunaiBugFolder";

        static KuiBugReportHyperlinkHandler()
        {
            // Subtract-then-add idempotently re-binds across domain reloads
            // so two reloads in a row don't leak a duplicate handler.
            EditorGUI.hyperLinkClicked -= OnHyperlink;
            EditorGUI.hyperLinkClicked += OnHyperlink;
        }

        static void OnHyperlink(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if (args.hyperLinkData == null) return;
            if (!args.hyperLinkData.TryGetValue(AttrName, out string path)) return;
            if (string.IsNullOrEmpty(path)) return;
            EditorUtility.RevealInFinder(path);
        }
    }
}
