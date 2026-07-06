using UnityEngine;
using UnityEngine.UI;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// A minimal dialog: a text label plus a continue button. Presses latch a one-shot flag the driving
    /// step consumes, so the panel needn't know anything about the runner.
    /// </summary>
    public sealed class DialogPanel : MonoBehaviour, IDialogPanel
    {
        [SerializeField] private Text _label;
        [SerializeField] private Button _continueButton;

        private bool _continued;

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(() => _continued = true);
            Hide();
        }

        public void Show(string message)
        {
            _continued = false;
            if (_label != null)
                _label.text = message;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public bool ConsumeContinue()
        {
            if (!_continued)
                return false;
            _continued = false;
            return true;
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
