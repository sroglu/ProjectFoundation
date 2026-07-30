using System;

namespace PFound.GameDataStore.Entries
{
    public readonly struct EntityLevel : IEquatable<EntityLevel>, IComparable<EntityLevel>
    {
        public readonly byte Value;

        public EntityLevel(byte value)
        {
            Value = value;
        }
        
        public static EntityLevel Invalid { get; set; }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(EntityLevel other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object other)
        {
            return other is EntityLevel otherEntity && Equals(otherEntity);
        }

        public int CompareTo(EntityLevel other)
        {
            return Value.CompareTo(other.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool TryParse(string text, out EntityLevel entityLevel)
        {
            if (byte.TryParse(text, out var value))
            {
                entityLevel = new EntityLevel(value);
                return true;
            }

            entityLevel = Invalid;
            return false;
        }

        #region Predefined Types

        public static EntityLevel Zero => new EntityLevel(0);
        public static EntityLevel One => new EntityLevel(1);

        #endregion

    }
}