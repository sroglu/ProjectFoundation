using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.DependencyContainer
{
    /// <summary>Internal record of one registration; aliases (As&lt;T&gt;) share one descriptor.</summary>
    internal sealed class ServiceDescriptor
    {
        public Type ImplType;
        public ServiceLifetime Lifetime;
        public Func<DependencyContainer, object> Factory; // null => reflection constructor injection
        public object Instance;                            // pre-set (RegisterInstance) or cached singleton
    }

    /// <summary>Fluent configuration for a registration.</summary>
    public sealed class ServiceBuilder<TImpl> where TImpl : class
    {
        private readonly DependencyContainer _container;
        private readonly ServiceDescriptor _descriptor;

        internal ServiceBuilder(DependencyContainer container, ServiceDescriptor descriptor)
        {
            _container = container;
            _descriptor = descriptor;
        }

        /// <summary>Also resolve <typeparamref name="TService"/> to this implementation (shared instance for singletons).</summary>
        public ServiceBuilder<TImpl> As<TService>() where TService : class
        {
            _container.AddAlias(typeof(TService), _descriptor);
            return this;
        }

        public ServiceBuilder<TImpl> WithLifetime(ServiceLifetime lifetime)
        {
            _descriptor.Lifetime = lifetime;
            return this;
        }

        public ServiceBuilder<TImpl> WithFactory(Func<DependencyContainer, TImpl> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _descriptor.Factory = c => factory(c);
            return this;
        }
    }

    /// <summary>
    /// Constructor-injection DI container. Register services fluently, <see cref="Build"/> to wire
    /// the graph (eager singletons + <see cref="IInitializableService"/> hooks), then resolve by
    /// lifetime (<see cref="ServiceLifetime"/>). Constructors are chosen by the greediest set of
    /// resolvable parameters; circular dependencies fail fast. Disposing the container disposes the
    /// instances it created. Main-thread only.
    /// </summary>
    public sealed class DependencyContainer : IServiceProvider, IDisposable
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<ServiceDescriptor, object> _rootScoped = new Dictionary<ServiceDescriptor, object>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly HashSet<Type> _resolving = new HashSet<Type>();
        private bool _built;
        private bool _disposed;

        // ---- registration ----

        /// <summary>Registers <typeparamref name="TImpl"/> (Singleton by default). Configure via the returned builder.</summary>
        public ServiceBuilder<TImpl> Register<TImpl>() where TImpl : class
        {
            ThrowIfBuilt();
            var descriptor = new ServiceDescriptor { ImplType = typeof(TImpl), Lifetime = ServiceLifetime.Singleton };
            Add(typeof(TImpl), descriptor);
            return new ServiceBuilder<TImpl>(this, descriptor);
        }

        /// <summary>Registers an already-built instance as a singleton for <typeparamref name="TService"/> (the caller owns its lifetime).</summary>
        public void RegisterInstance<TService>(TService instance) where TService : class
        {
            ThrowIfBuilt();
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Add(typeof(TService), new ServiceDescriptor
            {
                ImplType = instance.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Instance = instance
            });
        }

        /// <summary>
        /// Registers an already-built instance as a singleton under the runtime <paramref name="serviceType"/>
        /// (the caller owns its lifetime). Throws if <paramref name="instance"/> is not assignable to it.
        /// </summary>
        public void RegisterInstance(Type serviceType, object instance)
        {
            ThrowIfBuilt();
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!serviceType.IsInstanceOfType(instance))
                throw new InvalidOperationException(
                    "Instance of type " + instance.GetType().FullName + " is not assignable to " + serviceType.FullName + ".");
            Add(serviceType, new ServiceDescriptor
            {
                ImplType = instance.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Instance = instance
            });
        }

        /// <summary>Maps an additional service type to an existing descriptor, validating assignability.</summary>
        internal void AddAlias(Type serviceType, ServiceDescriptor descriptor)
        {
            if (!serviceType.IsAssignableFrom(descriptor.ImplType))
                throw new InvalidOperationException(
                    descriptor.ImplType.FullName + " is not assignable to " + serviceType.FullName + ".");
            Add(serviceType, descriptor);
        }

        /// <summary>Adds a service-type→descriptor mapping, failing fast on a duplicate registration.</summary>
        private void Add(Type serviceType, ServiceDescriptor descriptor)
        {
            if (_services.ContainsKey(serviceType))
                throw new InvalidOperationException("Duplicate registration for service type " + serviceType.FullName + ".");
            _services[serviceType] = descriptor;
        }

        /// <summary>Locks registration and eagerly constructs singletons (running their Initialize hooks).</summary>
        public DependencyContainer Build()
        {
            ThrowIfBuilt();
            _built = true;
            var seen = new HashSet<ServiceDescriptor>();
            foreach (var entry in _services)
            {
                var descriptor = entry.Value;
                if (descriptor.Lifetime == ServiceLifetime.Singleton && seen.Add(descriptor))
                    GetOrCreate(descriptor, _rootScoped, _disposables);
            }
            return this;
        }

        // ---- resolution ----

        public T Get<T>() => (T)GetRequired(typeof(T), _rootScoped, _disposables);

        public bool TryGet<T>(out T service)
        {
            if (_services.ContainsKey(typeof(T))) { service = Get<T>(); return true; }
            service = default;
            return false;
        }

        /// <summary>Tries to resolve a service by runtime type. Returns false if not registered.</summary>
        public bool TryGet(Type serviceType, out object service)
        {
            if (_services.ContainsKey(serviceType)) { service = GetRequired(serviceType, _rootScoped, _disposables); return true; }
            service = null;
            return false;
        }

        public object GetService(Type serviceType) =>
            _services.ContainsKey(serviceType) ? GetRequired(serviceType, _rootScoped, _disposables) : null;

        public IServiceScope CreateScope()
        {
            ThrowIfNotBuilt();
            return new ServiceScope(this);
        }

        internal object GetRequired(Type serviceType, Dictionary<ServiceDescriptor, object> scoped, List<IDisposable> disposeOwner)
        {
            ThrowIfDisposed();
            if (!_services.TryGetValue(serviceType, out var descriptor))
                throw new InvalidOperationException("No service registered for " + serviceType.FullName + ".");
            return GetOrCreate(descriptor, scoped, disposeOwner);
        }

        private object GetOrCreate(ServiceDescriptor descriptor, Dictionary<ServiceDescriptor, object> scoped, List<IDisposable> disposeOwner)
        {
            switch (descriptor.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    // Singletons are always root-owned, regardless of the resolving scope.
                    if (descriptor.Instance == null) descriptor.Instance = Create(descriptor, _rootScoped, _disposables);
                    return descriptor.Instance;

                case ServiceLifetime.Scoped:
                    var cache = scoped ?? _rootScoped;
                    if (!cache.TryGetValue(descriptor, out var scopedInstance))
                    {
                        scopedInstance = Create(descriptor, cache, disposeOwner);
                        cache[descriptor] = scopedInstance;
                    }
                    return scopedInstance;

                default: // Transient
                    return Create(descriptor, scoped, disposeOwner);
            }
        }

        private object Create(ServiceDescriptor descriptor, Dictionary<ServiceDescriptor, object> scoped, List<IDisposable> disposeOwner)
        {
            // A pre-set instance (RegisterInstance) is returned as-is; the caller owns its disposal.
            if (descriptor.Instance != null) return descriptor.Instance;

            object instance = descriptor.Factory != null
                ? descriptor.Factory(this)
                : Construct(descriptor.ImplType, scoped, disposeOwner);

            if (instance is IDisposable disposable) disposeOwner.Add(disposable);
            if (instance is IInitializableService initializable) initializable.Initialize();
            return instance;
        }

        private object Construct(Type implType, Dictionary<ServiceDescriptor, object> scoped, List<IDisposable> disposeOwner)
        {
            if (!_resolving.Add(implType))
                throw new InvalidOperationException("Circular dependency detected while resolving " + implType.FullName + ".");
            try
            {
                var ctor = SelectConstructor(implType);
                if (ctor == null) return Activator.CreateInstance(implType);

                var parameters = ctor.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                    args[i] = GetRequired(parameters[i].ParameterType, scoped, disposeOwner);
                return ctor.Invoke(args);
            }
            finally
            {
                _resolving.Remove(implType);
            }
        }

        // Greediest public constructor whose parameters are all registered; null => use parameterless.
        private ConstructorInfo SelectConstructor(Type implType)
        {
            ConstructorInfo best = null;
            int bestCount = -1;
            foreach (var ctor in implType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameters = ctor.GetParameters();
                bool resolvable = true;
                foreach (var p in parameters)
                    if (!_services.ContainsKey(p.ParameterType)) { resolvable = false; break; }
                if (resolvable && parameters.Length > bestCount) { best = ctor; bestCount = parameters.Length; }
            }
            return best;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _disposables.Count - 1; i >= 0; i--) _disposables[i].Dispose();
            _disposables.Clear();
            _rootScoped.Clear();
        }

        private void ThrowIfBuilt() { if (_built) throw new InvalidOperationException("Container already built; cannot register more services."); }
        private void ThrowIfNotBuilt() { if (!_built) throw new InvalidOperationException("Call Build() before creating scopes."); }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(DependencyContainer)); }

        private sealed class ServiceScope : IServiceScope, IServiceProvider
        {
            private readonly DependencyContainer _root;
            private readonly Dictionary<ServiceDescriptor, object> _scoped = new Dictionary<ServiceDescriptor, object>();
            private readonly List<IDisposable> _disposables = new List<IDisposable>();
            private bool _disposed;

            public ServiceScope(DependencyContainer root) { _root = root; }

            public IServiceProvider Provider => this;

            public T Get<T>() { ThrowIfDisposed(); return (T)_root.GetRequired(typeof(T), _scoped, _disposables); }

            public bool TryGet<T>(out T service)
            {
                if (!_disposed && _root._services.ContainsKey(typeof(T))) { service = Get<T>(); return true; }
                service = default;
                return false;
            }

            public object Get(Type serviceType) { ThrowIfDisposed(); return _root.GetRequired(serviceType, _scoped, _disposables); }

            public bool TryGet(Type serviceType, out object service)
            {
                if (!_disposed && _root._services.ContainsKey(serviceType)) { service = Get(serviceType); return true; }
                service = null;
                return false;
            }

            /// <summary>Resolves within this scope; returns null for an unregistered service (<see cref="IServiceProvider"/> contract).</summary>
            public object GetService(Type serviceType)
            {
                ThrowIfDisposed();
                return _root._services.ContainsKey(serviceType) ? _root.GetRequired(serviceType, _scoped, _disposables) : null;
            }

            public IServiceScope CreateScope() { ThrowIfDisposed(); return new ServiceScope(_root); }

            private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ServiceScope)); }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                for (int i = _disposables.Count - 1; i >= 0; i--) _disposables[i].Dispose();
                _disposables.Clear();
                _scoped.Clear();
            }
        }
    }
}
