using System;

namespace PFound.GameDataStore.Entries
{
    public readonly struct EntityType : IDataIdentifier, IEquatable<EntityType>
    {
        public readonly ushort Value;
        public ushort NumericId => Value;
        public string TextId => EntryManager.DataIdentifierRegistry.GetTextId(this);

        public EntityType(ushort value)
        {
            Value = value;
        }

        public static EntityType Invalid => new EntityType(0);

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(EntityType other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object other)
        {
            return other is EntityType otherEntity && Equals(otherEntity);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool TryParse(ReadOnlySpan<char> text, out EntityType entityType)
        {
            if (ushort.TryParse(text, out var value))
            {
                entityType = new EntityType(value);
                return true;
            }

            entityType = Invalid;
            return false;
        }

        public static bool TryParse(string numericIdOrTextId, out EntityType entityType)
        {
            if (ushort.TryParse(numericIdOrTextId, out var value))
            {
                entityType = new EntityType(value);
                return true;
            }

            return EntryManager.DataIdentifierRegistry.TryGetEntityType(numericIdOrTextId, out entityType);
        }
        
        

        #region Predefined Types

        public static EntityType Book => new EntityType(555);

        #endregion
    }
}