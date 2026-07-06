using PFound.MVC.core;
using UnityEngine;

namespace PFound.MVC.Samples
{
    /// <summary>
    /// Sample view rendering a CounterModel. Override <see cref="UpdateView"/>
    /// to push model data into your UI widgets (TMP_Text, Image, etc.).
    ///
    /// This file deliberately avoids referencing UGUI/TMP so the sample asmdef
    /// stays minimal — wire your own text component on the GameObject hosting
    /// this view and update it in <see cref="UpdateView"/>.
    /// </summary>
    public class CounterView : View<CounterModel>
    {
        [SerializeField] private string _renderedText;

        public string RenderedText => _renderedText;

        protected override void OnInit()
        {
            // Hook up controller callbacks here, e.g. button clicks routed to Controller.
        }

        public override void UpdateView()
        {
            if (Model == null) return;

            _renderedText = $"{Model.CurrentData.Label}: {Model.CurrentData.Value}";
            // In a real view: _label.text = _renderedText;
        }
    }
}
