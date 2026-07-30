using System;
using System.Collections.Generic;

namespace PFound.MVC.core
{
    /// <summary>
    /// Scoped container for MVC controller routing, model registry, and event dispatch.
    /// Replaces global static state with per-scope instances.
    /// Each game section (scene, screen, panel) creates its own MvcContext.
    /// </summary>
    public class MvcContext : IDisposable
    {
        private readonly List<IController> _controllers = new();
        private readonly Dictionary<uint, IModel> _models = new();
        private uint _nextModelId = 1;
        private bool _disposed;

        /// <summary>
        /// Type-safe redirect — invoke an action on the first matching controller in this context.
        /// </summary>
        public void Redirect<TController>(Action<TController> action) where TController : class, IController
        {
            ThrowIfDisposed();
            foreach (var ctrl in _controllers)
            {
                if (ctrl is TController target)
                {
                    action(target);
                    return;
                }
            }
        }

        /// <summary>
        /// Type-safe broadcast — deliver a typed event to all handlers in this context.
        /// </summary>
        public void Broadcast<TEvent>(TEvent evt)
        {
            ThrowIfDisposed();
            for (int i = _controllers.Count - 1; i >= 0; i--)
            {
                if (_controllers[i] is IEventHandler<TEvent> handler)
                    handler.Handle(evt);
            }
        }

        /// <summary>
        /// Legacy string-based redirect within this context's controllers.
        /// </summary>
        [Obsolete("Use type-safe Redirect<T> or Broadcast<T> instead")]
        internal void LegacyRedirect(IController source, string actionName, string controllerName, EventArgs data)
        {
            ThrowIfDisposed();
            for (int i = _controllers.Count - 1; i >= 0; i--)
            {
                var ctrl = _controllers[i];
                if (ctrl is ControllerBase cb)
                    cb.HandleLegacyRedirect(source, actionName, controllerName, data);
            }
        }

        internal void Register(IController controller)
        {
            ThrowIfDisposed();
            _controllers.Add(controller);
        }

        internal void Unregister(IController controller)
        {
            _controllers.Remove(controller);
        }

        internal uint RegisterModel(IModel model)
        {
            ThrowIfDisposed();
            var id = _nextModelId++;
            _models[id] = model;
            return id;
        }

        internal void UnregisterModel(uint id)
        {
            _models.Remove(id);
        }

        public TModel GetModel<TModel>() where TModel : class, IModel
        {
            ThrowIfDisposed();
            foreach (var kvp in _models)
            {
                if (kvp.Value is TModel model)
                    return model;
            }
            return null;
        }

        public IModel GetModelById(uint instanceId)
        {
            ThrowIfDisposed();
            return _models.TryGetValue(instanceId, out var model) ? model : null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _controllers.Clear();
            _models.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MvcContext), "MvcContext has been disposed");
        }
    }
}
