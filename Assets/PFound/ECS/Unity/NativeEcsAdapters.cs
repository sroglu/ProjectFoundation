using Unity.Collections;
using PFound.ECS;

namespace PFound.ECS.Unity
{
    /// <summary>
    /// Unity-layer adapters that copy the pure core's managed query/interaction views into
    /// <see cref="NativeArray{T}"/> for Burst/jobs interop. The core deliberately returns managed
    /// <see cref="QueryResult"/> / <see cref="InteractionView"/> (no engine dependency); these
    /// extensions live in the engine-referencing assembly so the core stays pure.
    ///
    /// Each method allocates with the caller-supplied <see cref="Allocator"/>; the caller owns the
    /// result and must <c>Dispose()</c> it (or use <see cref="Allocator.Temp"/> for one-frame use).
    /// <see cref="Entity"/> and <see cref="Interaction"/> are blittable, so the copies are valid
    /// NativeArray element types.
    /// </summary>
    public static class NativeEcsAdapters
    {
        /// <summary>Copies a query result's matched entities into a freshly allocated NativeArray.</summary>
        public static NativeArray<Entity> ToNativeArray(this QueryResult result, Allocator allocator)
        {
            int count = result.Count;
            var array = new NativeArray<Entity>(count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < count; i++) array[i] = result[i];
            return array;
        }

        /// <summary>Convenience: resolve <paramref name="id"/> on <paramref name="world"/> and copy to a NativeArray.</summary>
        public static NativeArray<Entity> EntitiesNative(this World world, QueryId id, Allocator allocator)
            => world.Entities(id).ToNativeArray(allocator);

        /// <summary>Copies an interaction view into a freshly allocated NativeArray.</summary>
        public static NativeArray<Interaction> ToNativeArray(this InteractionView view, Allocator allocator)
        {
            int count = view.Count;
            var array = new NativeArray<Interaction>(count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < count; i++) array[i] = view[i];
            return array;
        }

        /// <summary>Convenience: get all interactions of <paramref name="interactionType"/> as a NativeArray.</summary>
        public static NativeArray<Interaction> GetNative(this InteractionManager interactions, int interactionType, Allocator allocator)
            => interactions.Get(interactionType).ToNativeArray(allocator);
    }
}
