using System;
using UnityEngine;

namespace PFound.UserPrefs
{
    internal sealed class UnityDebugLogger : IPrefsLogger
    {
        private const string Prefix = "[UserPrefs] ";
        private readonly bool _verbose;

        public UnityDebugLogger(bool verbose)
        {
            _verbose = verbose;
        }

        public void Info(string message)
        {
            if (!_verbose) return;
            Debug.Log(Prefix + message);
        }

        public void Warn(string message, Exception ex = null)
        {
            if (ex != null)
                Debug.LogWarning(Prefix + message + " | " + ex);
            else
                Debug.LogWarning(Prefix + message);
        }

        public void Error(string message, Exception ex = null)
        {
            if (ex != null)
                Debug.LogError(Prefix + message + " | " + ex);
            else
                Debug.LogError(Prefix + message);
        }
    }

    internal sealed class NullLogger : IPrefsLogger
    {
        public void Info(string message) { }
        public void Warn(string message, Exception ex = null) { }
        public void Error(string message, Exception ex = null) { }
    }
}
