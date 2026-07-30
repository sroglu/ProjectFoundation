using System;

namespace PFound.MVC.core
{
    /// <summary>
    /// Controllers can be a page (singleton/screen) or an instance (per-object).
    /// </summary>
    public enum ControllerType
    {
        Page,
        Instance
    }

    /// <summary>
    /// Interface for controllers.
    /// A controller should has a model and a view.
    /// </summary>
    public interface IController : IDisposable
    {
        IModel GetModel();
        ViewBase GetView();
    }

    /// <summary>
    /// Base controller class with some common implementations
    /// It also describes functionalities of a controller
    /// </summary>
    public abstract class ControllerBase : IController
    {
        /// <summary>
        /// The scoped context this controller belongs to.
        /// </summary>
        protected MvcContext Context { get; }

        #region Properties
        protected ControllerType ControllerType = ControllerType.Instance;
        #endregion

        protected ControllerBase(MvcContext context, ControllerType controllerType)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ControllerType = controllerType;
            Context.Register(this);
        }

        /// <summary>
        /// Backward-compatible constructor — creates/uses a default shared context.
        /// </summary>
        [Obsolete("Use the constructor with MvcContext parameter for scoped routing")]
        protected ControllerBase(ControllerType controllerType)
            : this(DefaultContext, controllerType)
        {
        }

        #region Default Context (backward compat)
        private static MvcContext _defaultContext;
        private static MvcContext DefaultContext => _defaultContext ??= new MvcContext();

        /// <summary>
        /// Resets the default shared context. Used for test cleanup when using legacy constructors.
        /// </summary>
        internal static void ResetDefaultContext()
        {
            _defaultContext?.Dispose();
            _defaultContext = null;
        }
        #endregion

        #region UtilityFunctions
        public abstract IModel GetModel();
        public abstract ViewBase GetView();

        /// <summary>
        /// Broadcast to all controllers in this context (legacy string-based).
        /// </summary>
        [Obsolete("Use Context.Redirect<T> or Context.Broadcast<T> for type-safe routing")]
        protected void Redirect(string actionName, EventArgs data = null)
        {
#pragma warning disable CS0618
            Context.LegacyRedirect(this, actionName, null, data);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Targeted redirect to a specific controller type by name (legacy string-based).
        /// </summary>
        [Obsolete("Use Context.Redirect<T> for type-safe routing")]
        protected void Redirect(string actionName, string controllerName, EventArgs data = null)
        {
#pragma warning disable CS0618
            Context.LegacyRedirect(this, actionName, controllerName, data);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called by MvcContext for legacy string-based routing dispatch.
        /// </summary>
        internal void HandleLegacyRedirect(IController source, string actionName, string controllerName, EventArgs data)
        {
            if (controllerName == null || controllerName == GetType().ToString())
            {
                OnActionRedirected(source, actionName, data);
            }
        }
        #endregion

        #region Overridables
        /// <summary>
        /// Handle function for redirected events
        /// All controllers implement the events they responsible.
        /// </summary>
        protected virtual void OnActionRedirected(IController source, string actionName, EventArgs data) { }

        public virtual void Dispose()
        {
            Context.Unregister(this);
        }
        #endregion
    }

    /// <summary>
    /// Generic controller class
    /// It implements the relation with view and model
    /// </summary>
    /// <typeparam name="V"> View </typeparam>
    /// <typeparam name="M"> Model </typeparam>
    public class Controller<V, M> : ControllerBase where V : ViewBase where M : IModel
    {
        #region Accesors
        V pageView { get { return ViewManager.GetPageView<V>(); } }
        V _instanceView;
        V instanceView
        {
            get
            {
                if (_instanceView == null)
                    _instanceView = ViewManager.Instance.CreateInstanceView<V>();
                return _instanceView;
            }
        }
        V view;
        public V View
        {
            get
            {
                if (view == null)
                {
                    switch (ControllerType)
                    {
                        case ControllerType.Page:
                            view = pageView;
                            break;
                        case ControllerType.Instance:
                            view = instanceView;
                            break;
                        default:
                            view = pageView;
                            break;
                    }
                }
                return view;
            }
            private set
            {
                view = value;
            }
        }
        protected M Model { get; private set; }
        #endregion

        public Controller(MvcContext context, ControllerType controllerType, M model, V view = null) : base(context, controllerType)
        {
            Model = model;
            View = view;

            View.Init(this);
            OnCreate();

            if (ControllerType == ControllerType.Page)
                View.Hide();
        }

        /// <summary>
        /// Backward-compatible constructor — uses default shared context.
        /// </summary>
        [Obsolete("Use the constructor with MvcContext parameter for scoped routing")]
        public Controller(ControllerType controllerType, M model, V view = null) : base(controllerType)
        {
            Model = model;
            View = view;

            View.Init(this);
            OnCreate();

            if (ControllerType == ControllerType.Page)
                View.Hide();
        }

        public override sealed void Dispose()
        {
            OnDestroy();
            Model.Dispose();
            View.Dispose();
            base.Dispose();
        }
        public override sealed IModel GetModel()
        {
            return Model;
        }
        public override sealed ViewBase GetView()
        {
            return View;
        }

        #region Overridables
        protected virtual void OnCreate() { }
        protected virtual void OnDestroy() { }
        #endregion

    }
}
