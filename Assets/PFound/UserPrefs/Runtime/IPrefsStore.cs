using System;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.UserPrefs
{
    public interface IPrefsStore : IDisposable
    {
        T Get<T>(PrefKey<T> key);
        void Set<T>(PrefKey<T> key, T value);
        void Clear<T>(PrefKey<T> key);

        void SetDeferred<T>(PrefKey<T> key, T value);
        void Flush();
        Task FlushAsync(CancellationToken ct = default);

        IDisposable Subscribe<T>(PrefKey<T> key, Action<PrefChange<T>> handler);

        int SchemaVersion { get; }
        bool IsLoaded { get; }
    }
}
