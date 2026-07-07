# PFound.MVC

A lightweight Model-View-Controller framework for Unity. A `Controller<V, M>` owns a typed `Model`
and a `ViewBase` (page-type or per-object instance) and routes to peers through a scoped
`MvcContext` with type-safe `Redirect`/`Broadcast`. Input-source-agnostic.

## Quick reference

```csharp
var ctx  = new MvcContext();                             // one per scope (scene/screen/panel)
var ctrl = new CounterController(ctx, new CounterModel(ctx, data));

ViewManager.ShowPageView<CounterView>();                 // ViewManager must be in the scene
ctx.Broadcast(new ScoreChangedEvent { NewScore = 100 }); // typed, to every IEventHandler<T>
ctx.Redirect<OtherController>(c => c.DoSomething());     // typed, to the first match
ctx.Dispose();                                           // releases all controllers + models
```

## Dependencies

None — Unity built-in only (no PFound modules, no third-party packages, no scripting defines).

## Docs

- Deep reference: [MODULE.md](MODULE.md) — key types, full public API, setup/wiring, game-specific
  input, and the static→scoped migration.
- Sample walkthrough: [Samples/MODULE.md](Samples/MODULE.md).
