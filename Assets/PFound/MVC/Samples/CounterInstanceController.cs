using PFound.MVC.core;

namespace PFound.MVC.Samples
{
    /// <summary>
    /// Sample <b>instance</b>-type controller. Instance controllers ask the
    /// <see cref="ViewManager"/> to instantiate a view prefab (picked from
    /// instancePrefabs[] by matching type). One controller ⇄ one view ⇄ one
    /// GameObject. Typical use: inventory slots, enemy units, list items.
    ///
    /// Usage:
    /// <code>
    /// var ctx = new MvcContext();
    /// foreach (var cfg in questConfigs)
    /// {
    ///     var data = new CounterData { Label = cfg.Name, Value = cfg.Starting };
    ///     var ctrl = new CounterInstanceController(ctx, new CounterModel(ctx, data));
    ///     // ctrl.View is a freshly instantiated GameObject; position it, parent it.
    /// }
    /// </code>
    /// </summary>
    public class CounterInstanceController : Controller<CounterView, CounterModel>, IEventHandler<CounterChangedEvent>
    {
        public CounterInstanceController(MvcContext context, CounterModel model)
            : base(context, ControllerType.Instance, model) { }

        public void Add(int amount)
        {
            for (int i = 0; i < amount; i++) Model.Increment();
            View.UpdateView();
        }

        public void Handle(CounterChangedEvent evt)
        {
            // Another counter changed — could refresh or react.
            View.UpdateView();
        }
    }
}
