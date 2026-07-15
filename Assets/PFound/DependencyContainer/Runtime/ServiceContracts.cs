using System;

namespace PFound.DependencyContainer
{
    /// <summary>How long a resolved service instance lives.</summary>
    public enum ServiceLifetime
    {
        /// <summary>One shared instance for the whole container.</summary>
        Singleton,
        /// <summary>One instance per <see cref="IServiceScope"/>.</summary>
        Scoped,
        /// <summary>A fresh instance on every resolve.</summary>
        Transient
    }

    /// <summary>Optional hook called once, right after a service is constructed and injected.</summary>
    public interface IInitializableService
    {
        void Initialize();
    }

    /// <summary>
    /// A resolution scope. Scoped services resolve to one instance per scope; disposing the scope
    /// disposes the <see cref="IDisposable"/> instances it created.
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        T Get<T>();
        bool TryGet<T>(out T service);

        /// <summary>Resolves a service by runtime type within this scope. Throws if not registered.</summary>
        object Get(Type serviceType);

        /// <summary>Tries to resolve a service by runtime type within this scope. Returns false if not registered.</summary>
        bool TryGet(Type serviceType, out object service);

        /// <summary>Creates a nested scope sharing the same registrations and singletons.</summary>
        IServiceScope CreateScope();

        /// <summary>A <see cref="IServiceProvider"/> view that resolves within this scope.</summary>
        IServiceProvider Provider { get; }
    }
}
