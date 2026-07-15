# DependencyContainer

## Purpose
A lightweight constructor-injection DI container for Unity. Register services fluently, `Build()` to
wire the object graph, then resolve by lifetime. Pure C# — no engine reference, no `MonoBehaviour`,
no scene presence.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.DependencyContainer` | `Runtime/PFound.DependencyContainer.asmdef` | `noEngineReferences: true`, `autoReferenced: false` |
| `PFound.DependencyContainer.Tests` | `Tests/PFound.DependencyContainer.Tests.asmdef` | NUnit suite, `noEngineReferences: true` |

## Dependencies
None — no other PFound module, no third-party package, no scripting define. The runtime references
nothing but the BCL.

## Key Types

### `PFound.DependencyContainer`
- **`DependencyContainer`** — the container. `IServiceProvider` + `IDisposable`. Holds the
  registration graph, owns the singletons/scoped instances it creates.
- **`ServiceBuilder<TImpl>`** — fluent configuration returned by `Register<TImpl>()`
  (`As<TService>`, `WithLifetime`, `WithFactory`).
- **`ServiceLifetime`** (enum) — `Singleton`, `Scoped`, `Transient`.
- **`IServiceScope`** (`IDisposable`) — a resolution scope; generic `Get<T>` / `TryGet<T>` **and**
  runtime-`Type` `Get(Type)` / `TryGet(Type, out object)`, nested `CreateScope()`, and a `Provider`
  (`IServiceProvider`) view that resolves within the scope. Scoped services resolve to one instance
  per scope.
- **`IInitializableService`** — optional hook; `Initialize()` runs once, right after a service is
  constructed and injected.
- **`ServiceDescriptor`** (internal) — one registration record; aliases share a single descriptor.

## Public API

**Registration** (before `Build`, else `InvalidOperationException`; a second registration for the
same service type throws `InvalidOperationException` — duplicates fail fast):
```csharp
ServiceBuilder<TImpl> Register<TImpl>() where TImpl : class;   // Singleton by default
void RegisterInstance<TService>(TService instance) where TService : class;  // caller-owned
void RegisterInstance(Type serviceType, object instance);      // runtime-type; throws if instance not assignable to serviceType
```
`ServiceBuilder<TImpl>`:
```csharp
ServiceBuilder<TImpl> As<TService>() where TService : class;   // alias; shared instance for singletons — throws if TImpl not assignable to TService
ServiceBuilder<TImpl> WithLifetime(ServiceLifetime lifetime);
ServiceBuilder<TImpl> WithFactory(Func<DependencyContainer, TImpl> factory);
```

**Wiring**:
```csharp
DependencyContainer Build();   // locks registration, eagerly constructs singletons, returns this
```

**Resolution**:
```csharp
T Get<T>();                            // throws if unregistered
bool TryGet<T>(out T service);
bool TryGet(Type serviceType, out object service);   // runtime-type; false if unregistered
object GetService(Type serviceType);   // IServiceProvider; null if unregistered
IServiceScope CreateScope();           // throws before Build()
```

**`IServiceScope`** (each caches its own `Scoped` instances and tracks its own disposables):
```csharp
T Get<T>();                            // throws if unregistered
bool TryGet<T>(out T service);
object Get(Type serviceType);          // runtime-type; throws if unregistered
bool TryGet(Type serviceType, out object service);   // runtime-type; false if unregistered
IServiceScope CreateScope();           // nested scope over the same registrations/singletons
IServiceProvider Provider { get; }     // an IServiceProvider view that resolves within this scope
void Dispose();                        // disposes this scope's created instances (not singletons)
```

**Teardown**:
```csharp
void Dispose();   // disposes created IDisposables in reverse creation order
```

## Model

- **Lifetimes.** `Singleton` = one shared instance per container (always root-owned, regardless of
  the scope that resolves it). `Scoped` = one instance per `IServiceScope`. `Transient` = a fresh
  instance on every resolve.
- **Build phase.** `Register(...)` / `RegisterInstance(...)` mutate the graph; `Build()` locks it and
  eagerly constructs every singleton (running each `IInitializableService.Initialize()`). Registering
  after `Build()` throws.
- **Registration is fail-fast.** A second registration for the same service type throws
  (no silent last-wins overwrite). `RegisterInstance(Type, object)` throws if the instance is not
  assignable to the service type, and `As<TService>()` throws if the implementation is not assignable
  to the alias — assignability is validated at registration, not deferred to resolve.
- **Constructor selection.** The greediest public constructor whose parameters are ALL registered is
  chosen; if none qualifies, the parameterless constructor is used via `Activator.CreateInstance`.
- **Circular dependencies fail fast.** A cycle detected while constructing throws
  `InvalidOperationException` naming the type — no lazy proxying.
- **Ownership.** The container tracks every `IDisposable` it *creates* and disposes them in reverse
  creation order on `Dispose()`. A pre-built instance passed to `RegisterInstance` is owned by the
  caller and never disposed by the container.

## Setup / wiring

Pure library — `new DependencyContainer()`, no scene object or lifecycle host. The consumer owns the
instance: build it once at startup, keep the reference (in your bootstrap object, or hand it to other
systems), and `Dispose()` it on shutdown. There is no static/singleton accessor — if you want global
reach, register the container (or a service locator over it) into your own bootstrap seam.

```csharp
var container = new DependencyContainer();
container.Register<SaveSystem>().As<ISaveSystem>();
container.Register<AudioService>().WithLifetime(ServiceLifetime.Singleton);
container.RegisterInstance<IClock>(clock);          // caller-owned instance
container.Build();                                   // eager singletons + Initialize() hooks

var save = container.Get<ISaveSystem>();
using (var scope = container.CreateScope()) { var perScope = scope.Get<Foo>(); }
// on shutdown:
container.Dispose();
```

Main-thread only. `CreateScope()` throws before `Build()`.

## Extension points

The container is deliberately small; three seams cover the customization space without subclassing:

- **Custom lifetimes** are expressed by choosing a `ServiceLifetime` per registration
  (`.WithLifetime(...)`), not by adding new enum values. The three built-ins cover per-container,
  per-scope, and per-resolve; anything more exotic (e.g. per-request pooling) is modeled as a
  `Transient` whose factory pulls from a pool you own.
- **Custom factories** via `.WithFactory(c => ...)`. The delegate receives the container, so a
  factory can resolve collaborators, read config, or wrap a third-party builder. Use this for types
  that can't be constructor-injected (native handles, types with non-service constructor args, or
  decorators that wrap another registered service resolved from `c`).
- **Custom scopes** via `CreateScope()` → `IServiceScope`. Each scope caches its own `Scoped`
  instances and tracks its own disposables; disposing the scope disposes only what that scope
  created (singletons stay alive at the root). Nest scopes freely (each is independent); the scope
  does NOT re-parent singletons.
- **Post-construct hooks** via `IInitializableService.Initialize()` — runs once per created instance,
  after injection, for setup that must not live in the constructor.

## File Structure
```
DependencyContainer/
  README.md
  MODULE.md
  Runtime/
    PFound.DependencyContainer.asmdef
    DependencyContainer.cs     # container, ServiceBuilder<TImpl>, ServiceDescriptor, ServiceScope
    ServiceContracts.cs        # ServiceLifetime, IInitializableService, IServiceScope
  Tests/
    PFound.DependencyContainer.Tests.asmdef
    DependencyContainerTests.cs
```

## Downstream Dependents
- **`PFound.Render.RenderContext`** — resolves render services through the container
  (`RenderContextRegistration`, `RenderContextResolver`, `ContainerRenderContextServiceProvider`).
- **`PFound.GuidedOnboardingFlow`** — registers its tutorial runtime services into a container.

## Limitations / Known Gaps
- **Main-thread only.** No locking; concurrent resolve/build from other threads is unsupported.
- **No collection resolution.** Resolving `IEnumerable<T>` / all implementations of a service is not
  built in — one descriptor per service type (a duplicate registration throws, it does not overwrite);
  register a factory that returns the list if you need many.
- **No open-generic registration.** You register concrete/closed types; `Register<IRepo<>>` is not
  supported.
- **Constructor injection only.** No property or method injection; use `.WithFactory(...)` for types
  that can't be constructor-injected.
- **Eager singletons.** `Build()` constructs every singleton up front (to run `Initialize()` in a
  known order); there is no lazy-singleton option.
- **`GetService` returns null for unregistered types** (the `IServiceProvider` contract), whereas
  `Get<T>()` throws — pick the one that matches your failure expectation.
