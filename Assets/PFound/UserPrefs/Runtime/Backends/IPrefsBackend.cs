using System.Threading;
using System.Threading.Tasks;

namespace PFound.UserPrefs
{
    public interface IPrefsBackend
    {
        bool TryGet<T>(string key, out T value);
        void Set<T>(string key, T value);
        void Delete(string key);
        bool HasKey(string key);

        void Load();
        void Flush();
        Task FlushAsync(CancellationToken ct);
    }
}
