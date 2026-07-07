# MVC

## Purpose
A lightweight Model-View-Controller framework for Unity. A `Controller<V, M>` owns a typed `Model`
and a `ViewBase`; it is either **page** type (one screen-level view drawn from a pre-registered
roster) or **instance** type (a view instantiated per object). Controllers route to each other
through a scoped `MvcContext` with type-safe `Redirect`/`Broadcast`. The assembly is
**input-source-agnostic** — it references no input package; pointer enter/exit rides Unity's
`EventSystem`, and games wire their own input layer to controllers (see *Game-specific input*).

## Assemblies

| Assembly | Path | Platforms | `autoReferenced` | Depends on |
|---|---|---|---|---|
| `PFound.MVC` | `PFound.MVC.asmdef` | all | `true` | (none) — Unity built-in only |
| `PFound.MVC.Tests` | `Tests/PFound.MVC.Tests.asmdef` | Editor | `false` | `PFound.MVC`, TestRunner, `nunit` |
| `PFound.MVC.Samples` | `Samples/PFound.MVC.Samples.asmdef` | all | `false` | `PFound.MVC` |

## Dependencies
- **PFound modules:** none.
- **Third-party packages:** none. The runtime asmdef has an empty `references` list and pulls
  only Unity built-ins (`UnityEngine`, `UnityEngine.UI`/`EventSystems`, `UnityEngine.Events`).
- **Scripting defines:** none.

## Key Types

**Routing / core (`PFound.MVC.core`):**
- **`MvcContext`** — a plain, per-scope container (`IDisposable`) for controller routing, the model
  registry, and event dispatch. Each game section (scene, screen, panel) news up its own; disposing
  clears every controller and model it holds. Replaces global static state.
- **`IEventHandler<in TEvent>`** — implement on a controller to receive a strongly-typed event, no
  string routing or `EventArgs` casting.
- **`IController`** / **`IModel`** / **`IView`** — the base contracts (all `IDisposable` except `IView`).
- **`ControllerBase`** — abstract base; takes `MvcContext` + `ControllerType`, registers itself on
  construction and unregisters on `Dispose`. Carries the `[Obsolete]` string-based `Redirect(...)`.
- **`Controller<V, M>`** (`V : ViewBase`, `M : IModel`) — typed controller; `View` and `Model`
  resolve automatically (page view from `ViewManager`'s roster, or a new instance view from prefab).
- **`ControllerType`** — `Page` (screen-level view, hidden on create) or `Instance` (per-object).
- **`ModelBase`** / **`Model<T>`** (`T : ICloneable`) — auto-registered models holding cloneable
  description data + live current data; single or array (`SubModels`) shapes.
- **`ModelType`** — `Single` or `Array` (the model's internal shape).
- **`ViewBase`** — abstract `MonoBehaviour` view (`[RequireComponent(typeof(RectTransform))]`);
  exposes `Show`/`Hide`/`State`/`IsOpen`/`IsPointerOn` and the `StateChanged` event, with
  `OnCreate`/`OnRemove`/`OnStateChanged` template hooks.
- **`View<M>`** (`M : ModelBase`) — generic view bound to a typed model; `UpdateView()` abstract,
  `OnInit()` hook.

**View management (`PFound.MVC`):**
- **`ViewManager`** — scene-scoped singleton `MonoBehaviour`; owns the page-view roster + instance
  prefab list and a back-stack of previously shown page views.

**Empty pieces (global namespace):**
- **`EmptyModel`** / **`EmptyView`** / **`EmptyData`** — no-op implementations for controllers that
  do not need all three pieces.

## Public API

**Routing — `MvcContext` (`PFound.MVC.core`):**
- `void Redirect<TController>(Action<TController> action)` — invoke the action on the first matching
  controller in this context.
- `void Broadcast<TEvent>(TEvent evt)` — deliver to every `IEventHandler<TEvent>` in the context.
- `TModel GetModel<TModel>()` where `TModel : class, IModel` — first registered model of that type.
- `IModel GetModelById(uint instanceId)`.
- `void Dispose()` — clears all controllers + models; further calls throw `ObjectDisposedException`.
- `void IEventHandler<in TEvent>.Handle(TEvent evt)` — implemented by handler controllers.

**Controllers (`PFound.MVC.core`):**
- `ControllerBase(MvcContext context, ControllerType controllerType)` — base ctor; auto-registers.
- `Controller<V, M>(MvcContext context, ControllerType controllerType, M model, V view = null)` —
  typed; `View`/`Model` resolved automatically when `view` is omitted. Overridable
  `OnCreate()`/`OnDestroy()`; sealed `Dispose()` tears down model + view + registration; sealed
  `GetModel()` / `GetView()`.
- `[Obsolete]` legacy ctors `ControllerBase(ControllerType)` / `Controller<V,M>(ControllerType, M, V)`
  route through a default shared context (back-compat only).

**Models (`PFound.MVC.core`):**
- `Model<T>(MvcContext context, T data)` and `Model<T>(MvcContext context, T[] dataArr)`.
- `void Update(T data)` / `void Update(T[] data)`, `void UpdateCurrentData()`,
  `void UpdateDescriptionData()`.
- `T CurrentData`, `T[] CurrentDataArr`, `Model<T>[] SubModels`, `uint InstanceId(int subModelIndex = 0)`.

**Views (`PFound.MVC.core`):**
- `View<M>` — `abstract void UpdateView()`, `void OnInit()` (hook), `bool IsInitiated`.
- `ViewBase` — `void Show()`, `void Hide()`, `ViewState State`, `bool IsOpen`, `bool IsPointerOn`,
  `RectTransform rectTransform`, `void DestroyInstance()`, `UnityAction<ViewState> StateChanged`.

**`ViewManager` (`PFound.MVC`):**
- `static ViewManager Instance` (assigned in `Awake`).
- `static T GetPageView<T>()` where `T : ViewBase`.
- `static void ShowPageView<T>(bool remember = true)` where `T : ViewBase`.
- `static void ShowPageView(ViewBase view, bool remember = true)`.
- `static void ShowLastPageView()` — pops and shows the last remembered page view.
- `T CreateInstanceView<T>()` where `T : ViewBase` (instance method — instantiates from the prefab list).

## Setup / wiring

Two moving parts: a **plain `MvcContext`** you own in code, and a **`ViewManager` MonoBehaviour**
you place in the scene. There is **no DI registration and no auto-created host** — you `new` the
context and controllers, and you place the `ViewManager` by hand.

1. **Place one `ViewManager` per scene.** It is a singleton — `Instance` is assigned in `Awake` and
   is **scene-scoped (no `DontDestroyOnLoad`)**; a second `ViewManager` destroys itself. Its static
   methods (`GetPageView`/`ShowPageView`) require `Instance` to be live, so a page-type
   `Controller<V, M>` **must be constructed after** the `ViewManager` exists (or you pass an explicit
   `view`). If any UI must survive scene loads, that is your decision — mark the `ViewManager`'s
   GameObject `DontDestroyOnLoad` yourself; nothing here does it for you.
2. **Populate its two serialized arrays in the inspector:** `pageViews[]` (the pre-designed,
   screen-level view roots — one per page type) and `instancePrefabs[]` (view prefabs instantiated
   for `Instance` controllers). Resolution is by type, so each entry's concrete `ViewBase` type must
   be unique within its list.
3. **Add an `EventSystem` to the scene.** `ViewBase` pointer enter/exit (`IsPointerOn`) rides Unity's
   `EventSystem` (`IPointerEnterHandler`/`IPointerExitHandler`). No `EventSystem`, no hover.
4. **Add a Layer named `View`.** `ViewBase.Awake` sets
   `gameObject.layer = LayerMask.NameToLayer("View")`; if that layer is not defined in *Tags and
   Layers*, the lookup returns `-1` and the assignment fails. Define it once per project.
5. **Own an `MvcContext` per scope.** `new MvcContext()`, construct your controllers/models against
   it, and `Dispose()` it when the scope ends to release every registered controller and model.

```csharp
var ctx  = new MvcContext();
var data = new CounterData { Label = "Coins", Value = 0 };
var ctrl = new CounterPageController(ctx, new CounterModel(ctx, data));

ViewManager.ShowPageView<CounterView>();   // ViewManager.Instance must already be in the scene

// scope teardown:
ctx.Dispose();
```

## File Structure
```
(submodule root)
PFound.MVC.asmdef
README.md              (thin landing page)
MODULE.md              (this file — canonical reference)
ViewManager.cs         (page-view roster + instance-prefab instantiation + back-stack)
Core/
  Controller.cs        (ControllerBase, Controller<V,M>, ControllerType, IController)
  Model.cs             (IModel, ModelType, ModelBase, Model<T>)
  View.cs              (IView, ViewBase, View<M>)
  MvcContext.cs        (MvcContext scoped container)
  IEventHandler.cs     (IEventHandler<TEvent>)
EmptyComponent/
  EmptyData.cs         (EmptyData)
  EmptyModel.cs        (EmptyModel)
  EmptyView.cs         (EmptyView)
Tests/                 (EditMode unit tests — PFound.MVC.Tests, autoReferenced=false)
Samples/               (Counter page/instance example — PFound.MVC.Samples, autoReferenced=false;
                        see Samples/MODULE.md)
```

## Downstream Dependents
No assemblies in this project depend on MVC — it is consumed by separate game projects (e.g.
`StrategyGame`, `TileMatch`) that copy the source locally today; future work is to switch them to
this submodule reference.

## Limitations / Known Gaps
- Legacy `Redirect(string)` and the static model dictionary still work within scoped contexts for
  backward compatibility but are marked `[Obsolete]`. Use `Context.Redirect<T>()` /
  `Context.Broadcast<T>()` and the scoped registry for type-safe routing.
- `ViewManager` remains a global scene-scoped `MonoBehaviour` singleton — it is a view factory, not a
  routing component, so context-scoping is intentionally not applied to it.
- Page/instance-view resolution is **by type**, so a given concrete `ViewBase` type may appear only
  once in `pageViews[]` / `instancePrefabs[]`.

## Game-specific input

MVC has **no input dependency by design** — the assembly references no input package.

- **Pointer hover/enter/exit** is already handled for you: `IsPointerOn` is set from `ViewBase`'s
  `IPointerEnterHandler`/`IPointerExitHandler` callbacks, which ride Unity's `EventSystem`. No setup
  beyond an `EventSystem` in the scene.
- **Custom actions** (click, hold, swipe, etc.) — add an input layer in your game project (e.g.
  `Assets/GameSpecific/Input/`) that:
  1. owns the input asset (and any generated bindings), and
  2. on callback, calls into your controller's public method (e.g. `inventoryController.OnHoldGesture()`).

This separation lets you swap input backends (Input System, legacy Input Manager, touch, gamepad, or
a test mock) without touching MVC. An earlier revision embedded an input-actions asset and a static
`ViewBase` event coupled to an input-callback type; those were removed. Game projects that relied on
them should keep their own input asset under `Assets/GameSpecific/Input/`.

## Migration: Static → Scoped MvcContext

**Old pattern** (still compiles, marked `[Obsolete]`):
```csharp
var model = new MyModel(data);
var ctrl  = new MyController(ControllerType.Page, model);
ctrl.Redirect("ActionName"); // global, string-based broadcast
```

**New pattern** (recommended):
```csharp
var context = new MvcContext();
var model   = new MyModel(context, data);
var ctrl    = new MyController(context, ControllerType.Page, model);

// Type-safe redirect to a specific controller
context.Redirect<OtherController>(c => c.DoSomething());

// Type-safe broadcast to all IEventHandler<T> implementors
context.Broadcast(new ScoreChangedEvent { NewScore = 100 });

// Cleanup — disposes all controllers + models
context.Dispose();
```

Existing game projects (`StrategyGame`, `TileMatch`) using the legacy constructors continue to work
without changes; the `[Obsolete]` warnings guide migration.
