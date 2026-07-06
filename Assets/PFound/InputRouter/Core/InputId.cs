using System;

namespace PFound.InputRouter
{
    /// <summary>
    /// Identifies one concurrent input within a multi-input intent — e.g. a touch finger id, a
    /// player/device slot, or a gamepad index. Lets a single intent type carry N simultaneous
    /// inputs, each with its own independent phase lifecycle.
    /// </summary>
    public readonly struct InputId : IEquatable<InputId>
    {
        public readonly int Value;

        public InputId(int value)
        {
            Value = value;
        }

        public bool Equals(InputId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is InputId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "InputId(" + Value + ")";
        }
    }
}
