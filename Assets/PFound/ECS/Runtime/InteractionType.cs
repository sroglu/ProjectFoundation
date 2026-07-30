using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    /// <summary>Which side of a directed interaction an entity plays.</summary>
    public enum InteractionRole
    {
        Interactor = 0,
        Interactee = 1,
    }

    /// <summary>
    /// A named, strongly-typed interaction key. Wraps the raw <see cref="int"/> id the
    /// <see cref="InteractionManager"/> stores against, and carries a human-readable name via
    /// <see cref="InteractionTypeRegistry"/>. Implicitly converts to <see cref="int"/>, so every
    /// <c>int</c>-keyed API (Start/Get/…) accepts a typed value transparently. <see cref="Invalid"/>
    /// (value 0) is the reserved "no type".
    /// </summary>
    public readonly struct InteractionType : IEquatable<InteractionType>
    {
        /// <summary>The raw id used as the interaction key.</summary>
        public readonly int Value;

        public InteractionType(int value) { Value = value; }

        /// <summary>True unless this is the reserved <see cref="Invalid"/> type.</summary>
        public bool IsValid => Value != Invalid.Value;

        /// <summary>Registered name, or "Invalid" if none was registered.</summary>
        public string Name => InteractionTypeRegistry.GetName(Value);

        /// <summary>The paired reverse type (interactee-side mirror), value <c>int.MaxValue - Value</c>.</summary>
        public InteractionType Reverse => new InteractionType(int.MaxValue - Value);

        public static readonly InteractionType Invalid = new InteractionType(0);

        private static int _nextId = 1;

        /// <summary>Allocates the next free id and registers <paramref name="name"/> (and its reverse).</summary>
        public static InteractionType Create(string name)
        {
            int value = _nextId++;
            InteractionTypeRegistry.Register(value, name);
            return new InteractionType(value);
        }

        /// <summary>Wraps an explicit id and registers <paramref name="name"/> (and its reverse).</summary>
        public static InteractionType Named(int value, string name)
        {
            InteractionTypeRegistry.Register(value, name);
            return new InteractionType(value);
        }

        public static implicit operator int(InteractionType type) => type.Value;

        public bool Equals(InteractionType other) => Value == other.Value;
        public override bool Equals(object obj) => obj is InteractionType other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Name + "(" + Value + ")";
    }

    /// <summary>
    /// Process-wide id↔name registry for interaction types. Registering a type also registers its
    /// reverse id (<c>int.MaxValue - value</c>) as <c>name + "_reverse"</c>, and a name→id reverse
    /// lookup. Names are diagnostic only — the manager keys on the raw int.
    /// </summary>
    public static class InteractionTypeRegistry
    {
        private static readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private static readonly Dictionary<string, int> _ids = new Dictionary<string, int>();

        public static void Register(int type, string name)
        {
            if (name == null) return;
            if (!_names.ContainsKey(type))
            {
                _names[type] = name;
                _ids[name] = type;

                int reverse = int.MaxValue - type;
                if (!_names.ContainsKey(reverse))
                {
                    string reverseName = name + "_reverse";
                    _names[reverse] = reverseName;
                    _ids[reverseName] = reverse;
                }
            }
        }

        public static string GetName(int type) => _names.TryGetValue(type, out var name) ? name : "Invalid";

        /// <summary>Reverse name→id lookup.</summary>
        public static bool TryGetType(string name, out int type) => _ids.TryGetValue(name, out type);

        /// <summary>Clears all registrations (test isolation).</summary>
        public static void Clear() { _names.Clear(); _ids.Clear(); }
    }

    /// <summary>
    /// Optional interaction predicate carried on a <see cref="QueryBuilder{T1}"/>: restricts matches
    /// to entities that play <see cref="Role"/> in an interaction of <see cref="Type"/>. Inactive
    /// (the default) when <see cref="Type"/> is 0 (<see cref="InteractionType.Invalid"/>).
    /// </summary>
    internal readonly struct InteractionFilter : IEquatable<InteractionFilter>
    {
        public readonly int Type;
        public readonly InteractionRole Role;

        public InteractionFilter(int type, InteractionRole role) { Type = type; Role = role; }

        public bool Active => Type != 0;

        public bool Equals(InteractionFilter other) => Type == other.Type && Role == other.Role;
        public override bool Equals(object obj) => obj is InteractionFilter f && Equals(f);
        public override int GetHashCode() => (Type * 397) ^ (int)Role;
    }
}
