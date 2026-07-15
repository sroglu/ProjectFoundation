# PFound.DependencyContainer

A lightweight constructor-injection DI container for Unity — register services fluently, `Build()`
to wire the graph, resolve by lifetime. Pure C#, no engine reference, no scene presence.

## Quick reference

```csharp
var container = new DependencyContainer();
container.Register<SaveSystem>().As<ISaveSystem>();          // Singleton by default
container.Register<Foo>().WithLifetime(ServiceLifetime.Scoped);
container.RegisterInstance<IClock>(clock);                    // caller-owned
container.Build();                                            // eager singletons + Initialize() hooks

var save = container.Get<ISaveSystem>();
using (var scope = container.CreateScope()) { var foo = scope.Get<Foo>(); }
container.Dispose();                                          // reverse-order teardown
```

## Dependencies

None — BCL only (`noEngineReferences`, `autoReferenced:false`).

## Docs

Deep reference: [MODULE.md](MODULE.md) — full API, lifetime/ownership model, and `## Extension
points` (custom lifetimes / factories / scopes / post-construct hooks).
