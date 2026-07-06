using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// Static manager that queues M3Snackbar instances and shows one at a time.
    ///
    /// Usage:
    ///   M3SnackbarManager.SetRoot(rootVisualElement);
    ///   M3SnackbarManager.Show("Message", "Undo", OnUndo);
    /// </summary>
    public static class M3SnackbarManager
    {
        private static readonly Queue<SnackbarRequest> _queue = new();
        private static M3Snackbar _current;
        private static VisualElement _root;

        /// <summary>Register the root VisualElement to show snackbars in.</summary>
        public static void SetRoot(VisualElement root) => _root = root;

        /// <summary>Enqueue a snackbar.</summary>
        public static void Show(string text, string actionText = null, Action onAction = null, int durationMs = 4000)
        {
            _queue.Enqueue(new SnackbarRequest(text, actionText, onAction, durationMs));
            TryShowNext();
        }

        private static void TryShowNext()
        {
            if (_current != null || _queue.Count == 0 || _root == null) return;

            var req = _queue.Dequeue();
            _current = new M3Snackbar
            {
                Text       = req.Text,
                ActionText = req.ActionText,
                DurationMs = req.DurationMs,
            };

            if (req.OnAction != null)
                _current.OnAction += req.OnAction;

            _current.OnDismissed += () =>
            {
                _current = null;
                TryShowNext();
            };

            _current.Show(_root);
        }

        private readonly struct SnackbarRequest
        {
            public readonly string Text;
            public readonly string ActionText;
            public readonly Action OnAction;
            public readonly int    DurationMs;

            public SnackbarRequest(string text, string actionText, Action onAction, int durationMs)
            {
                Text       = text;
                ActionText = actionText;
                OnAction   = onAction;
                DurationMs = durationMs;
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorDomainReload()
        {
            _current = null;
            _root = null;
            _queue.Clear();
        }
#endif
    }
}
