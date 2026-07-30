using PFound.MVC.core;

namespace PFound.MVC.Samples
{
    /// <summary>
    /// Sample model wrapping <see cref="CounterData"/>. Demonstrates the
    /// Model&lt;T&gt; constructor pattern (auto-registers via RegisterNewModel).
    /// </summary>
    public class CounterModel : Model<CounterData>
    {
        public CounterModel(MvcContext context, CounterData data) : base(context, data) { }

        public void Increment()
        {
            CurrentData.Value += 1;
            UpdateDescriptionData();
        }

        public void Reset()
        {
            CurrentData.Value = 0;
            UpdateDescriptionData();
        }
    }
}
