using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.UserPrefs
{
    internal sealed class MemoryBackend : IPrefsBackend
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value) => _data[key] = value;

        public void Delete(string key) => _data.Remove(key);

        public bool HasKey(string key) => _data.ContainsKey(key);

        public void Load() { }

        public void Flush() { }

        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
