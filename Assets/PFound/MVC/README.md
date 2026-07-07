# PFound.MVC

A lightweight Model-View-Controller framework for Unity. A `Controller<V,M>` owns a typed `Model`
and a `ViewBase`; it is either **page** type (one screen-level view drawn from a pre-registered
roster) or **instance** type (a view instantiated per object). Controllers route to each other
through a scoped `MvcContext` with type-safe `Redirect`/`Broadcast`. Input-source-agnostic — the
assembly references no input package (see *Game-specific input*).

## Model

- **`MvcContext`** — a plain, per-scope container (`IDisposable`) for controller routing, the
  model registry, and event dispatch. Each game section (scene, screen, panel) news up its own;
  disposing clears every controller and model it holds. Replaces global static state.
- **Controllers** register/unregister themselves with their `MvcContext` at construction/`Dispose`.
  `ControllerType.Page` resolves its view from `ViewManager`'s inspector roster and is hidden on
  create; `ControllerType.Instance` instantiates a view prefab on first access.
- **Models** (`Model<T>` where `T : ICloneable`) hold cloneable description data + live current
  data, auto-register into the context, and support single or array (`SubModels`) shapes.
- **Views** (`View<M>`) are `MonoBehaviour`s; `Init` binds the controller + model and calls
  `UpdateView`. `ViewBase` exposes `Show`/`Hide`/`State`/`IsPointerOn` and the `StateChanged` event.

Legacy string-based `Redirect(...)` and the static model dictionary are still present but
`[Obsolete]` — use `Context.Redirect<T>` / `Context.Broadcast<T>` and the scoped registry.

## Public API

**Routing (`PFound.MVC.core`):**
- `MvcContext.Redirect<TController>(Action<TController>)` — invoke on the first matching controller.
- `MvcContext.Broadcast<TEvent>(TEvent)` — deliver to every `IEventHandler<TEvent>` in the context.
- `MvcContext.GetModel<TModel>()`, `GetModelById(uint)`, `Dispose()`.
- `IEventHandler<in TEvent>.Handle(TEvent)` — implement on a controller to receive a typed event.

**Controllers:**
- `ControllerBase(MvcContext, ControllerType)` — base; auto-registers.
- `Controller<V,M>(MvcContext, ControllerType, M model, V view = null)` — typed; `View`/`Model`
  resolved automatically. Overridable `OnCreate()`/`OnDestroy()`; sealed `Dispose()` tears down
  model + view + registration. `IController.GetModel()`/`GetView()`.

**Models:** `Model<T>(MvcContext, T)` / `(MvcContext, T[])`; `Update(T)`, `UpdateCurrentData()`,
`UpdateDescriptionData()`, `CurrentData`, `SubModels`, `InstanceId(int)`.

**Views:** `View<M>` — `UpdateView()` (abstract), `OnInit()`; `ViewBase` — `Show()`, `Hide()`,
`State`, `IsOpen`, `IsPointerOn`, `rectTransform`, `DestroyInstance()`, `StateChanged`.

**`ViewManager`** (singleton `MonoBehaviour`): static `Instance`, static `GetPageView<T>()`,
`ShowPageView<T>(bool remember = true)`, `ShowPageView(ViewBase, bool)`, `ShowLastPageView()`
(back-stack pop), and instance `CreateInstanceView<T>()`.

`EmptyModel` / `EmptyView` / `EmptyData` are no-op pieces for controllers that don't need all three.

## Setup / wiring

Two moving parts: a **plain `MvcContext`** you own in code, and a **`ViewManager` MonoBehaviour**
you place in the scene.

1. **Place one `ViewManager` per scene.** It is a singleton — `Instance` is assigned in `Awake`
   and is **scene-scoped (no `DontDestroyOnLoad`)**; a second `ViewManager` destroys itself. Its
   static methods (`GetPageView`/`ShowPageView`) require `Instance` to be live, so a page-type
   `Controller<V,M>` **must be constructed after** the `ViewManager` exists (or you pass an explicit
   `view`). If any UI must survive scene loads, that is your decision — mark the `ViewManager`'s
   GameObject `DontDestroyOnLoad` yourself; nothing here does it for you.
2. **Populate its two serialized arrays in the inspector:** `pageViews[]` (the pre-designed,
   screen-level view roots — one per page type) and `instancePrefabs[]` (view prefabs instantiated
   for `Instance` controllers). Resolution is by type, so each entry's concrete `ViewBase` type
   must be unique in its list.
3. **Add an `EventSystem` to the scene** — `ViewBase` pointer enter/exit (`IsPointerOn`) rides
   Unity's `EventSystem` (`IPointerEnterHandler`/`IPointerExitHandler`). No `EventSystem`, no hover.
4. **Add a Layer named `View`.** `ViewBase.Awake` sets `gameObject.layer = LayerMask.NameToLayer("View")`;
   if that layer is not defined in *Tags and Layers*, the lookup returns `-1` and the assignment
   fails. Define it once per project.
5. **Own an `MvcContext` per scope.** `new MvcContext()`, construct your controllers/models against
   it, and `Dispose()` it when the scope ends to release every registered controller and model.

```csharp
var ctx  = new MvcContext();
var data = new CounterData { Label = "Coins", Value = 0 };
var ctrl = new CounterPageController(ctx, new CounterModel(ctx, data));

ViewManager.ShowPageView<CounterView>();   // ViewManager.Instance must already be in the scene
ctrl.Add();
// scope teardown:
ctx.Dispose();
```

There is **no DI registration and no auto-created host** — you `new` the context and controllers,
and you place the `ViewManager` by hand. The assembly has no package references (Unity built-in only).

## Game-specific input

MVC has no input dependency by design. Hover/enter/exit is handled for you via `EventSystem`.
For custom actions (click, hold, swipe), add an input layer in your game project (e.g.
`Assets/GameSpecific/Input/`) that owns the input asset and calls your controller's public methods
— so the backend (Input System, touch, gamepad, test mock) is swappable without touching MVC. See
`MODULE.md` for the full input rationale.

## Layout

- `ViewManager.cs` — page-view roster + instance-prefab instantiation + back-stack.
- `Core/` — `Controller.cs`, `Model.cs`, `View.cs`, `IEventHandler.cs`, `MvcContext.cs`.
- `EmptyComponent/` — no-op `EmptyModel` / `EmptyView` / `EmptyData`.
- `Samples/` — a Counter page/instance example. Assembly `PFound.MVC` (+ `.Tests`, `.Samples`).
