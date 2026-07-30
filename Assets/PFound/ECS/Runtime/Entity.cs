using System;

namespace PFound.ECS
{
    /// <summary>
    /// Lightweight value handle for an entity: a reusable <see cref="Id"/> plus a
    /// <see cref="Version"/> that invalidates stale handles after the id is recycled.
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly int Id;
        public readonly int Version;

        public Entity(int id, int version)
        {
            Id = id;
            Version = version;
        }

        public static readonly Entity Invalid = new Entity(-1, 0);

        public bool IsValid => Id >= 0;

        public bool Equals(Entity other) => Id == other.Id && Version == other.Version;
        public override bool Equals(object obj) => obj is Entity e && Equals(e);
        public override int GetHashCode() => (Id * 397) ^ Version;
        public override string ToString() => "Entity(" + Id + "v" + Version + ")";

        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
    }
}
