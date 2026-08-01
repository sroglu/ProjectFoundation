namespace GameSpecific.Backend.Unity
{
    using UnityEngine;
    using GameSpecific.Backend;

    /// <summary>
    /// Editor-only MonoBehaviour host for the backend server. Runs the same boot sequence
    /// as the console Program.cs, but driven by Unity's MonoBehaviour lifecycle instead of
    /// a manual game loop.
    /// </summary>
    public sealed class GameBackendEditorHost : MonoBehaviour
    {
        BackendHost _host;

        void Start()
        {
            _host = GameBackendSetup.CreateHost(7777);
            _host.Listen(7777);
            Debug.Log("Backend server started on port 7777");
        }

        void Update()
        {
            if (_host != null)
                _host.Tick();
        }

        void OnDestroy()
        {
            if (_host != null)
            {
                _host.Halt();
                Debug.Log("Backend server stopped");
            }
        }
    }
}
