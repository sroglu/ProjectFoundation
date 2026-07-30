using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// A dialog line with an optional typewriter reveal and configurable dismissal. A tap (the continue
    /// button, or anywhere on the panel) first fast-forwards the typewriter to the full line, then — if the
    /// dismiss mode allows tapping — advances it. In an auto-dismiss mode the fully-revealed line advances
    /// itself after a delay. Whichever path advances it latches a one-shot flag the driving step consumes,
    /// so the panel needn't know anything about the runner.
    /// </summary>
    public sealed class DialogPanel : MonoBehaviour, IDialogPanel, IPointerClickHandler
    {
        [SerializeField] private Text _label;
        [SerializeField] private Button _continueButton;

        private string _fullText = string.Empty;
        private float _typingSpeedCps;
        private DialogDismissMode _dismissMode;
        private float? _autoDismissSeconds;

        private bool _showing;
        private bool _continued;
        private float _revealed;         // characters revealed so far (fractional)
        private float _sinceFullyShown;  // seconds since the full line finished revealing

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnTap);
            Hide();
        }

        public void Show(string message) => Show(message, 0f, DialogDismissMode.Tap, null);

        public void Show(string message, float typingSpeedCps, DialogDismissMode dismissMode, float? autoDismissSeconds)
        {
            _fullText = message ?? string.Empty;
            _typingSpeedCps = typingSpeedCps;
            _dismissMode = dismissMode;
            _autoDismissSeconds = autoDismissSeconds;

            _continued = false;
            _showing = true;
            _sinceFullyShown = 0f;
            _revealed = typingSpeedCps > 0f ? 0f : _fullText.Length;

            gameObject.SetActive(true);
            Render();
        }

        public void Hide()
        {
            _showing = false;
            _continued = false;
            gameObject.SetActive(false);
        }

        public bool ConsumeContinue()
        {
            if (!_continued)
                return false;
            _continued = false;
            return true;
        }

        public void OnPointerClick(PointerEventData eventData) => OnTap();

        private void Update()
        {
            if (!_showing || _continued)
                return;

            float dt = Time.unscaledDeltaTime;

            if (IsRevealing)
            {
                _revealed += _typingSpeedCps * dt;
                if (_revealed >= _fullText.Length)
                    _revealed = _fullText.Length;
                Render();
                return;
            }

            // Fully revealed: auto-dismiss modes advance themselves after the configured dwell.
            if (_dismissMode != DialogDismissMode.Tap && _autoDismissSeconds.HasValue)
            {
                _sinceFullyShown += dt;
                if (_sinceFullyShown >= _autoDismissSeconds.Value)
                    _continued = true;
            }
        }

        private bool IsRevealing => _typingSpeedCps > 0f && _revealed < _fullText.Length;

        private void OnTap()
        {
            if (!_showing || _continued)
                return;

            // First tap fast-forwards a running typewriter; only a tap on the fully-shown line dismisses,
            // and only when the mode accepts taps.
            if (IsRevealing)
            {
                _revealed = _fullText.Length;
                Render();
                return;
            }

            if (_dismissMode == DialogDismissMode.Tap || _dismissMode == DialogDismissMode.Both)
                _continued = true;
        }

        private void Render()
        {
            if (_label == null)
                return;
            int count = Mathf.Clamp(Mathf.FloorToInt(_revealed), 0, _fullText.Length);
            _label.text = count >= _fullText.Length ? _fullText : _fullText.Substring(0, count);
        }
    }

    /// <summary>Owns the overlay canvas that hosts every tutorial affordance.</summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class TutorialCanvas : MonoBehaviour, ITutorialCanvas
    {
        private Canvas _canvas;

        public Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponent<Canvas>());

        public void SetVisible(bool visible)
        {
            Canvas.enabled = visible;
        }
    }
}
