# MVC Samples

Minimal **Counter** sample demonstrating both controller types.

## Files

| File | Role |
|---|---|
| `CounterData.cs` | POCO; ICloneable for Model&lt;T&gt; snapshotting |
| `CounterModel.cs` | `Model<CounterData>` with `Increment` / `Reset` |
| `CounterView.cs` | `View<CounterModel>` — exposes `RenderedText` (wire to your TMP/UGUI) |
| `CounterPageController.cs` | **Page** controller — singleton screen; uses `ViewManager.ShowPageView<CounterView>()` |
| `CounterInstanceController.cs` | **Instance** controller — instantiates a view prefab per controller (e.g. list item) |
| `SampleActions.cs` | Centralised action-name constants used with `Redirect` |

## How to Run

1. Reference `PFound.MVC.Samples` in your scene asmdef (the samples asmdef is `autoReferenced: false`).
2. Create a `ViewManager` MonoBehaviour in your scene.
3. For the **page** sample:
   - Add a GameObject with `CounterView` as the page root (RectTransform required) and drag it into `ViewManager.pageViews[]`.
   - From your bootstrap code:
     ```csharp
     var data = new CounterData { Label = "Coins", Value = 0 };
     var ctrl = new CounterPageController(new CounterModel(data));
     ViewManager.ShowPageView<CounterView>();
     ctrl.Add();
     ```
4. For the **instance** sample:
   - Make a prefab with `CounterView` and drop it into `ViewManager.instancePrefabs[]`.
   - From your code:
     ```csharp
     var ctrl = new CounterInstanceController(new CounterModel(new CounterData { Label = "Quest 1" }));
     ctrl.Add(3);
     ```

## What this sample demonstrates

- Page vs Instance distinction in `Controller<V,M>` constructor
- `Model<T>` registration + cloneable description data
- `Redirect(actionName)` broadcast and `OnActionRedirected` reception
- Centralising action names with constants (workaround for the string-based API)
- View kept input-source-agnostic — derived games wire input themselves
