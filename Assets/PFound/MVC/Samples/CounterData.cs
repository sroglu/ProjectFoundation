using System;

namespace PFound.MVC.Samples
{
    /// <summary>
    /// Plain data carried by the counter sample model. Must be ICloneable so
    /// Model&lt;T&gt; can snapshot it independently of the inspector reference.
    /// </summary>
    [Serializable]
    public class CounterData : ICloneable
    {
        public int Value;
        public string Label;

        public object Clone() => new CounterData { Value = Value, Label = Label };
    }
}
