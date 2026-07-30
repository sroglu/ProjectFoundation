using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PFound.InputRouter
{
    /// <summary>
    /// Optional MonoBehaviour host that owns an <see cref="IntentRouter"/> and pumps it once per
    /// frame. Purely convenience glue: the router is engine-free and can be ticked from anywhere,
    /// but wiring it to Unity's update loop and the EventSystem UI gate is common enough to ship.
    /// Register adapters and subscribe against <see cref="Router"/> after this component exists.
    ///
    /// It also offers a combined enable/disable that toggles intent dispatch AND the active
    /// EventSystem's UI input module together, re-applying the choice whenever the active scene
    /// changes (a freshly loaded scene brings its own EventSystem whose module defaults to enabled).
    /// The pure <see cref="IntentRouter"/> core stays engine-free; only this host touches the
    /// EventSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntentRouterDriver : MonoBehaviour
    {
        [Tooltip("When true, suppress gameplay intents while the pointer is over EventSystem UI.")]
        [SerializeField] private bool _respectUiGate = true;

        /// <summary>The router driven by this component. Available from Awake onward.</summary>
        public IntentRouter Router { get; private set; }

        // Desired UI-input state, remembered so it survives scene changes (each scene's EventSystem
        // starts with its module enabled). This is a flag, not a null-state check.
        private bool _uiInputEnabled = true;

        private void Awake()
        {
            Router = _respectUiGate
                ? new IntentRouter(PointerOverUiGate.FromEventSystem())
                : new IntentRouter();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Update()
        {
            Router.Tick();
        }

        /// <summary>
        /// Enable intent dispatch AND the active EventSystem's UI input module. The choice is
        /// remembered and re-applied on the next active-scene change.
        /// </summary>
        public void Enable()
        {
            SetInputActive(true);
        }

        /// <summary>
        /// Disable intent dispatch AND the active EventSystem's UI input module. The choice is
        /// remembered and re-applied on the next active-scene change.
        /// </summary>
        public void Disable()
        {
            SetInputActive(false);
        }

        /// <summary>
        /// Toggle intent dispatch (<see cref="IntentRouter.Enabled"/>) together with the active
        /// EventSystem's UI input module. The state is stored and re-applied whenever the active
        /// scene changes so a newly loaded EventSystem inherits it. To gate only gameplay intents
        /// while leaving UI input untouched, set <see cref="IntentRouter.GameplayEnabled"/> instead.
        /// </summary>
        public void SetInputActive(bool active)
        {
            _uiInputEnabled = active;
            Router.Enabled = active;
            ApplyUiInputState();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ApplyUiInputState();
        }

        private void ApplyUiInputState()
        {
            // EventSystem.current is a legitimate boundary null (no EventSystem in the scene), as is
            // a scene with no BaseInputModule — branch on absence rather than treating it as an error.
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                return;
            }

            BaseInputModule module = current.GetComponent<BaseInputModule>();
            if (module != null)
            {
                module.enabled = _uiInputEnabled;
            }
        }
    }
}
