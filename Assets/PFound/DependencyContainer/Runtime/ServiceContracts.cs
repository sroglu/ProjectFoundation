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
    }
}
