namespace PFound.ECS
{
    /// <summary>
    /// Assigns a stable, dense type id (0..255) to each component type on first use.
    /// Main-thread only (see project conventions); no locking by design.
    /// </summary>
    internal static class ComponentTypeRegistry
    {
        private static int _next;

        internal static int Next()
        {
            int id = _next++;
            if (id >= ComponentMask.MaxBits)
                throw new System.InvalidOperationException(
                    "Exceeded " + ComponentMask.MaxBits + " component types.");
            return id;
        }

        internal static int Count => _next;
    }

    /// <summary>Holds the assigned id for component type <typeparamref name="T"/>.</summary>
    public static class ComponentType<T> where T : struct
    {
        public static readonly int Id = ComponentTypeRegistry.Next();
    }
}
