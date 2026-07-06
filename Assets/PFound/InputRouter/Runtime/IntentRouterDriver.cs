using UnityEngine;

namespace PFound.InputRouter
{
    /// <summary>
    /// Optional MonoBehaviour host that owns an <see cref="IntentRouter"/> and pumps it once per
    /// frame. Purely convenience glue: the router is engine-free and can be ticked from anywhere,
    /// but wiring it to Unity's update loop and the EventSystem UI gate is common enough to ship.
    /// Register adapters and subscribe against <see cref="Router"/> after this component exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntentRouterDriver : MonoBehaviour
    {
        [Tooltip("When true, suppress gameplay intents while the pointer is over EventSystem UI.")]
        [SerializeField] private bool _respectUiGate = true;

        /// <summary>The router driven by this component. Available from Awake onward.</summary>
        public IntentRouter Router { get; private set; }

        private void Awake()
        {
            Router = _respectUiGate
                ? new IntentRouter(PointerOverUiGate.FromEventSystem())
                : new IntentRouter();
        }

        private void Update()
        {
            Router.Tick();
        }
    }
}
