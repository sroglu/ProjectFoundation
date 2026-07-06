# MVC

## Purpose
Lightweight Model-View-Controller framework for Unity. A `ControllerBase` holds a `Model` and a `ViewBase`, can be **page** type (one screen-level singleton) or **instance** type (per-object), and broadcasts named actions to peer controllers via `Redirect`. `ViewManager` tracks registered page views and instantiates instance views from prefabs.

Designed to be **input-source-agnostic**: the assembly does not reference `Unity.InputSystem` or any specific input package. Pointer enter/exit detection uses Unity's `EventSystem` (`IPointerEnterHandler`/`IPointerExitHandler`); games wire their own input layer to controllers (see *Game-specific input* below).

## Assembly

| Assembly | Path | Depends On |
|---|---|---|
| `PFound.MVC` | `PFound.MVC.asmdef` | (none) — Unity built-in only |

## Key Classes

- **`MvcContext`** — Scoped container for controller routing, model registry, and event dispatch. Replaces global static state. Each game section creates its own context; disposing cleans up all references.
- **`IEventHandler<TEvent>`** — Generic typed event handler interface for type-safe broadcasts.
- **`ControllerBase`** — Abstract base controller. Takes `MvcContext` at construction; registers/unregisters automatically. Legacy string-based `Redirect()` is marked `[Obsolete]` — use `Context.Redirect<T>` or `Context.Broadcast<T>`.
- **`Controller<V, M>`** — Typed controller; `View` (V) and `Model` (M) auto-resolved (page view from `ViewManager`'s pre-registered list, or new instance view from prefab list).
- **`ControllerType`** — `Page` (uses pre-designed page view, hidden by default) or `Instance` (instantiated at runtime).
- **`IController`** / **`IModel`** — Interfaces.
- **`ViewBase`** — Abstract `MonoBehaviour` view; exposes `Show`/`Hide`, `IsPointerOn`, `OnCreate`/`OnRemove`/`OnStateChanged` hooks.
- **`View<M>`** — Generic typed view bound to a model.
- **`ModelBase`** / **`Model<T>`** — Auto-registered models with cloneable description data; supports single + array models. Takes optional `MvcContext` for scoped registry.
- **`ViewManager`** — Singleton MonoBehaviour managing the page view roster and the instance prefab list. Tracks page-view history (`ShowPageView` / `ShowLastPageView`).
- **`EmptyModel`**, **`EmptyView`**, **`EmptyData`** — No-op implementations for controllers that don't need all three pieces.

## Quick Start

```csharp
using PFound.MVC;
using PFound.MVC.core;

public readonly struct ItemSelectedEvent { public readonly int ItemId; public ItemSelectedEvent(int id) => ItemId = id; }

public class InventoryModel : Model<InventoryData> { public InventoryModel(MvcContext ctx, InventoryData d) : base(ctx, d) { } }

public class InventoryView : View<InventoryModel>
{
    public override void UpdateView() { /* refresh UI */ }
}

public class InventoryController : Controller<InventoryView, InventoryModel>, IEventHandler<ItemSelectedEvent>
{
    public InventoryController(MvcContext context, InventoryModel model)
        : base(context, ControllerType.Page, model) { }

    public void OnItemClicked(int id) => Context.Broadcast(new ItemSelectedEvent(id));

    public void Handle(ItemSelectedEvent evt) { View.UpdateView(); }
}
```

In your scene, drop a `ViewManager` MonoBehaviour, populate its `pageViews[]` (pre-designed page roots) and `instancePrefabs[]` (instantiable view prefabs). Call `ViewManager.ShowPageView<InventoryView>()` to switch screens.

## Game-specific input

The MVC assembly intentionally has **no input dependency**. To wire input:

- **Pointer hover/enter/exit** — already handled by `EventSystem` (`IsPointerOn` is set by `IPointerEnterHandler`/`IPointerExitHandler` callbacks on `ViewBase`). No setup required beyond an `EventSystem` in the scene.
- **Custom actions** (click, hold, swipe, etc.) — add an input handler in your game project (e.g. `Assets/GameSpecific/Input/`) that:
  1. Owns the InputSystem `.inputactions` asset and generated `Inputs.cs`
  2. On callback, calls into your controller's public method (e.g. `inventoryController.OnHoldGesture()`)
- This separation lets you swap input backends (InputSystem, Touch, Gamepad-only, mock for tests) without touching MVC.

A previous version of this module embedded an `Inputs.inputactions` asset and a static `ViewBase.ActionPerformed` event coupled to `InputAction.CallbackContext` — those have been removed. Game projects that previously relied on them should keep their own `Inputs.inputactions` under `Assets/GameSpecific/Input/`.

## File Structure
```
(submodule root)
PFound.MVC.asmdef
MODULE.md
ViewManager.cs
Core/
  Controller.cs        (ControllerBase, Controller<V,M>, ControllerType, IController)
  Model.cs             (IModel, ModelBase, Model<T>)
  View.cs              (IView, ViewBase, View<M>)
EmptyComponent/
  EmptyData.cs
  EmptyModel.cs
  EmptyView.cs
Tests/                 (EditMode unit tests — PFound.MVC.Tests)
Samples/               (Counter sample, autoReferenced=false — see Samples/MODULE.md)
```

## Migration: Static → Scoped MvcContext

**Old pattern** (still compiles, marked `[Obsolete]`):
```csharp
var model = new MyModel(data);
var ctrl = new MyController(ControllerType.Page, model);
ctrl.Redirect("ActionName"); // global broadcast
```

**New pattern** (recommended):
```csharp
var context = new MvcContext();
var model = new MyModel(context, data);
var ctrl = new MyController(context, ControllerType.Page, model);

// Type-safe redirect to specific controller
context.Redirect<OtherController>(c => c.DoSomething());

// Type-safe broadcast to all IEventHandler<T> implementors
context.Broadcast(new ScoreChangedEvent { NewScore = 100 });

// Cleanup — disposes all references
context.Dispose();
```

Existing game projects (StrategyGame, TileMatch) using the legacy constructors continue to work without changes. The `[Obsolete]` warnings will guide migration.

## Known Limitations / Future Work

- Legacy `Redirect(string)` still works within scoped contexts for backward compatibility but is marked `[Obsolete]`. Use `Context.Redirect<T>()` or `Context.Broadcast<T>()` for type-safe routing. Samples demonstrate the type-safe pattern.
- `ViewManager` remains a global MonoBehaviour singleton — it is a view factory, not a routing component, so context-scoping is not needed.

## Downstream Dependents

No assemblies in this project depend on MVC — it is consumed by separate game projects (e.g. `StrategyGame`, `TileMatch`) that copy the source locally today; future work is to switch them to this submodule reference.
