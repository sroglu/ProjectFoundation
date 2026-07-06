using PFound.MVC.core;

namespace PFound.MVC.Samples
{
    /// <summary>
    /// Sample <b>page</b>-type controller. Page controllers grab a pre-designed
    /// view from the <see cref="ViewManager"/>'s pageViews list. There is one
    /// instance per scene; the view is hidden by default and shown via
    /// <c>ViewManager.ShowPageView&lt;CounterView&gt;()</c>.
    ///
    /// Usage:
    /// <code>
    /// var ctx = new MvcContext();
    /// var data = new CounterData { Label = "Coins", Value = 0 };
    /// var ctrl = new CounterPageController(ctx, new CounterModel(ctx, data));
    /// ViewManager.ShowPageView&lt;CounterView&gt;();
    /// ctrl.Add();
    /// </code>
    /// </summary>
    public class CounterPageController : Controller<CounterView, CounterModel>, IEventHandler<CounterChangedEvent>
    {
        public CounterPageController(MvcContext context, CounterModel model)
            : base(context, ControllerType.Page, model) { }

        public void Add()
        {
            Model.Increment();
            View.UpdateView();
            Context.Broadcast(new CounterChangedEvent(Model.CurrentData.Value));
        }

        public void ResetCounter()
        {
            Model.Reset();
            View.UpdateView();
        }

        public void Handle(CounterChangedEvent evt)
        {
            // Listen to peer controllers if needed.
        }
    }
}
