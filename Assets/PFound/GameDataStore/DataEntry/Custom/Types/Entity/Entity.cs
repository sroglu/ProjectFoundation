using System;

namespace PFound.GameDataStore.Entries
{
    
    public interface IReadOnlyEntity
    {
        EntityType Type { get; }
        EntityLevel Level { get; }
    }
    public class Entity: IEquatable<Entity>, IReadOnlyEntity
    {
        public EntityType Type { get; }
        public EntityLevel Level { get; }
        
        public Entity(EntityType type, EntityLevel level)
        {
            Type = type;
            Level = level;
        }

        public override string ToString()
        {
            return $"{nameof(Type)}: {Type.TextId}, {nameof(Level)}: {Level}";
        }

        public bool Equals(Entity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type.Equals(other.Type) && Level.Equals(other.Level);
        }

        public override bool Equals(object other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (other.GetType() != this.GetType()) return false;
            return Equals((Entity) other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Level);
        }

    }
}