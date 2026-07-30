using System;
using System.Collections.Generic;

namespace PFound.UISystem.Components
{
    /// <summary>
    /// Plain C# class that manages mutual exclusion for a set of M3RadioButton instances.
    ///
    /// Usage:
    ///   var group = new M3RadioGroup();
    ///   group.Add(rb1);
    ///   group.Add(rb2);
    ///   group.Add(rb3);
    ///   group.SelectedIndex = 0;
    ///   group.OnSelectionChanged += index => Debug.Log("Selected: " + index);
    /// </summary>
    public class M3RadioGroup
    {
        private readonly List<M3RadioButton> _buttons = new();
        private int _selectedIndex = -1;

        /// <summary>Fired when selection changes. Passes the new selected index (-1 if none).</summary>
        public event Action<int> OnSelectionChanged;

        /// <summary>Creates an empty group. Use <see cref="Add"/> to populate.</summary>
        public M3RadioGroup() { }

        /// <summary>Creates a group with the given buttons. First button is selected by default.</summary>
        public M3RadioGroup(params M3RadioButton[] buttons)
        {
            foreach (var btn in buttons)
                Add(btn);
            if (_buttons.Count > 0)
                SelectAt(0);
        }

        /// <summary>Currently selected index (-1 if none selected).</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SelectAt(value);
        }

        /// <summary>Add a radio button to this group.</summary>
        public void Add(M3RadioButton button)
        {
            if (button == null)
                throw new ArgumentNullException(nameof(button));
            if (_buttons.Contains(button)) return;
            _buttons.Add(button);
            button.GroupSelectionRequested += OnButtonRequested;
        }

        /// <summary>Remove a radio button from this group.</summary>
        public void Remove(M3RadioButton button)
        {
            if (!_buttons.Contains(button)) return;
            _buttons.Remove(button);
            button.GroupSelectionRequested -= OnButtonRequested;
        }

        private void OnButtonRequested(M3RadioButton requestingButton)
        {
            int idx = _buttons.IndexOf(requestingButton);
            if (idx < 0) return;
            SelectAt(idx);
        }

        private void SelectAt(int index)
        {
            if (index < -1 || index >= _buttons.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} out of range. Group has {_buttons.Count} buttons.");
            if (index == _selectedIndex) return;

            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count)
                _buttons[_selectedIndex].DeselectSilently();

            _selectedIndex = index;

            // Select new
            if (_selectedIndex >= 0)
                _buttons[_selectedIndex].SelectSilently();

            OnSelectionChanged?.Invoke(_selectedIndex);
        }
    }
}
