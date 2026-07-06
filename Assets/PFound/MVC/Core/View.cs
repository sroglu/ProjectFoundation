using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace PFound.MVC.core
{
    /// <summary>
    /// Interface for views.
    /// A view is initialized with a controller and exposes update/show/hide.
    /// </summary>
    public interface IView
    {
        void Init(IController controller);
        void UpdateView();
        void Hide();
        void Show();
    }

    /// <summary>
    /// Base class for views with common implementations.
    /// Pointer enter/exit detection uses Unity's EventSystem (not the InputSystem
    /// package) so this assembly stays input-source-agnostic. Derived views that
    /// need raw pointer position or custom input actions should read from their
    /// own input source (InputSystem, legacy Input, etc.) — see MODULE.md.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class ViewBase : MonoBehaviour, IView, IDisposable, IPointerEnterHandler, IPointerExitHandler
    {
        #region Definitions
        public enum ViewState
        {
            visible,
            invisible
        }
        #endregion

        #region Properties
        RectTransform _rectTransform;
        #endregion

        #region Accesors
        public ViewState State { get { if (gameObject.activeInHierarchy) return ViewState.visible; return ViewState.invisible; } }
        public RectTransform rectTransform { get { if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>(); return _rectTransform; } }
        public bool IsPointerOn { get; private set; }
        public bool IsOpen { get { return gameObject.activeInHierarchy; } }
        #endregion

        #region UtilityFunctions
        /// <summary> Makes the view invisible. </summary>
        public void Hide() { gameObject.SetActive(false); OnStateChanged(State); StateChanged?.Invoke(State); }
        /// <summary> Makes the view visible. </summary>
        public void Show() { gameObject.SetActive(true); OnStateChanged(State); StateChanged?.Invoke(State); }
        /// <summary> EventSystem callback — sets <see cref="IsPointerOn"/> to true. </summary>
        public void OnPointerEnter(PointerEventData eventData) { IsPointerOn = true; }
        /// <summary> EventSystem callback — sets <see cref="IsPointerOn"/> to false. </summary>
        public void OnPointerExit(PointerEventData eventData) { IsPointerOn = false; }
        /// <summary> Destroys the GameObject. </summary>
        public void DestroyInstance() { Destroy(gameObject); }
        #endregion

        /// <summary> Event raised when the view's <see cref="ViewState"/> changes. </summary>
        public UnityAction<ViewState> StateChanged;

        protected virtual void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("View");
            OnCreate();
        }

        protected void OnDestroy()
        {
            OnRemove();
        }
        /// <summary> Disposes (destroys) the instance. </summary>
        public void Dispose() { DestroyInstance(); }

        /// <summary>
        /// Template hooks for derived views.
        /// </summary>
        #region Overridables
        protected virtual void OnCreate() { }
        protected virtual void OnRemove() { }
        protected virtual void OnDestroyInstance() { }
        protected virtual void OnStateChanged(ViewState state) { }

        // Public Methods
        public virtual void Init(IController controller) { }
        public abstract void UpdateView();
        #endregion

    }

    /// <summary>
    /// Generic view bound to a typed model.
    /// </summary>
    /// <typeparam name="M"> Model </typeparam>
    public abstract class View<M> : ViewBase where M : ModelBase
    {

        public IController Controller;
        public M Model;

        public bool IsInitiated { get { return Model != null; } }
        public override sealed void Init(IController controller)
        {
            Controller = controller;

            if (Model == null)
                Model = (M)Controller.GetModel();

            OnInit();
            UpdateView();
        }
        protected sealed override void OnDestroyInstance() { Controller.Dispose(); }

        #region Overridables
        protected virtual void OnInit() { }
        #endregion
    }
}
