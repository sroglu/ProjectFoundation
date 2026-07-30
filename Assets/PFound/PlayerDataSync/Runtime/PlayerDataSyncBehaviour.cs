using PFound.PlayerDataSync.Core;
using UnityEngine;

namespace PFound.PlayerDataSync
{
    /// <summary>
    /// A thin lifecycle shell that turns Unity's app-level pause/quit events into explicit sync points: when
    /// the app is backgrounded or closing, it flushes any pending local write to the backend. It holds no
    /// logic of its own — it proxies to the engine — which keeps saves event-driven rather than per-frame
    /// (there is deliberately no Update here). The game binds the engine it built via the installer; until
    /// then the shell is inert.
    /// </summary>
    public sealed class PlayerDataSyncBehaviour : MonoBehaviour
    {
        PlayerSaveSyncEngine _engine;
        bool _bound;

        public void Bind(PlayerSaveSyncEngine engine)
        {
            _engine = engine;
            _bound = true;
        }

        void OnApplicationPause(bool paused)
        {
            if (_bound && paused) FlushInBackground();
        }

        void OnApplicationQuit()
        {
            if (_bound) FlushInBackground();
        }

        // Fire-and-forget at the app-lifecycle boundary: the flush persists locally regardless, and a failed
        // push simply stays queued for the next launch, so there is nothing to await here.
        async void FlushInBackground()
        {
            await _engine.FlushAsync();
        }
    }
}
