using System;

namespace PFound.UserPrefs
{
    public interface IPrefsLogger
    {
        void Info(string message);
        void Warn(string message, Exception ex = null);
        void Error(string message, Exception ex = null);
    }
}
