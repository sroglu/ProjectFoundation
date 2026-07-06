using System;

namespace PFound.ECS
{
    /// <summary>
    /// 256-bit set of component-type ids, used both for per-entity component presence
    /// and for query include/exclude signatures. Four 64-bit lanes.
    /// </summary>
    public struct ComponentMask : IEquatable<ComponentMask>
    {
        public const int MaxBits = 256;

        private ulong _b0, _b1, _b2, _b3;

        public void Set(int bit)
        {
            switch (bit >> 6)
            {
                case 0: _b0 |= 1UL << (bit & 63); break;
                case 1: _b1 |= 1UL << (bit & 63); break;
                case 2: _b2 |= 1UL << (bit & 63); break;
                default: _b3 |= 1UL << (bit & 63); break;
            }
        }

        public void Clear(int bit)
        {
            switch (bit >> 6)
            {
                case 0: _b0 &= ~(1UL << (bit & 63)); break;
                case 1: _b1 &= ~(1UL << (bit & 63)); break;
                case 2: _b2 &= ~(1UL << (bit & 63)); break;
                default: _b3 &= ~(1UL << (bit & 63)); break;
            }
        }

        public bool Get(int bit)
        {
            switch (bit >> 6)
            {
                case 0: return (_b0 & (1UL << (bit & 63))) != 0;
                case 1: return (_b1 & (1UL << (bit & 63))) != 0;
                case 2: return (_b2 & (1UL << (bit & 63))) != 0;
                default: return (_b3 & (1UL << (bit & 63))) != 0;
            }
        }

        /// <summary>True if every bit set in <paramref name="other"/> is set here.</summary>
        public bool ContainsAll(in ComponentMask other) =>
            (_b0 & other._b0) == other._b0 &&
            (_b1 & other._b1) == other._b1 &&
            (_b2 & other._b2) == other._b2 &&
            (_b3 & other._b3) == other._b3;

        /// <summary>True if any bit set in <paramref name="other"/> is set here.</summary>
        public bool ContainsAny(in ComponentMask other) =>
            (_b0 & other._b0) != 0 ||
            (_b1 & other._b1) != 0 ||
            (_b2 & other._b2) != 0 ||
            (_b3 & other._b3) != 0;

        public void Reset() { _b0 = _b1 = _b2 = _b3 = 0; }

        public bool Equals(ComponentMask o) => _b0 == o._b0 && _b1 == o._b1 && _b2 == o._b2 && _b3 == o._b3;
        public override bool Equals(object obj) => obj is ComponentMask m && Equals(m);
        public override int GetHashCode()
        {
            unchecked
            {
                ulong h = _b0 * 31 ^ _b1 * 131 ^ _b2 * 1313 ^ _b3 * 13131;
                return (int)(h ^ (h >> 32));
            }
        }
    }
}
