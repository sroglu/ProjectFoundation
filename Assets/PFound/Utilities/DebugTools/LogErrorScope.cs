using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.DebugTools
{
    /// <summary>
    /// A <see cref="IDisposable"/> capture scope that records every error-severity log message emitted
    /// while it is open. Intended for tests that need to assert an error was (or was not) logged without
    /// letting Unity's test runner fail the test on the expected error. Wrap the code under test in a
    /// <c>using</c> block and inspect <see cref="Messages"/> afterwards.
    /// </summary>
    public sealed class LogErrorScope : IDisposable
    {
        private readonly List<string> _messages = new List<string>();
        private readonly bool _includeAssert;
        private readonly bool _includeException;
        private bool _disposed;

        /// <summary>Opens a scope subscribed to Unity's log callback.</summary>
        /// <param name="includeExceptions">When true, uncaught exceptions are also captured.</param>
        /// <param name="includeAsserts">When true, failed asserts are also captured.</param>
        public LogErrorScope(bool includeExceptions = true, bool includeAsserts = true)
        {
            _includeException = includeExceptions;
            _includeAssert = includeAsserts;
            Application.logMessageReceived += OnLogMessage;
        }

        /// <summary>The captured messages, in the order they were logged.</summary>
        public IReadOnlyList<string> Messages => _messages;

        /// <summary>Number of error-severity messages captured so far.</summary>
        public int Count => _messages.Count;

        /// <summary>True when at least one error-severity message has been captured.</summary>
        public bool Any => _messages.Count > 0;

        /// <summary>True when any captured message contains <paramref name="fragment"/> (ordinal).</summary>
        public bool Contains(string fragment)
        {
            for (var i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (IsCaptured(type)) _messages.Add(condition);
        }

        private bool IsCaptured(LogType type)
        {
            switch (type)
            {
                case LogType.Error: return true;
                case LogType.Exception: return _includeException;
                case LogType.Assert: return _includeAssert;
                default: return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.logMessageReceived -= OnLogMessage;
        }
    }
}
