using System;

namespace PFound.ECS
{
    /// <summary>Non-generic view of a component pool, for type-erased structural ops.</summary>
    internal interface IComponentPool
    {
        int ComponentTypeId { get; }
        bool Has(int entityId);
        void Remove(int entityId);
    }

    /// <summary>
    /// Sparse-set storage for one component type: a sparse array (entity id → dense index)
    /// over packed dense arrays of entity ids and values. O(1) add/remove/get, cache-friendly
    /// packed iteration. Add/remove are swap-based so the dense region stays contiguous.
    /// </summary>
    internal sealed class ComponentPool<T> : IComponentPool where T : struct
    {
        private int[] _sparse = new int[0];   // entityId -> dense index, or -1
        private int[] _denseEntity = new int[8];
        private T[] _denseValue = new T[8];
        private int _count;

        public ComponentPool(int typeId) { ComponentTypeId = typeId; }

        public int ComponentTypeId { get; }
        public int Count => _count;

        private void EnsureSparse(int entityId)
        {
            if (entityId < _sparse.Length) return;
            int n = _sparse.Length == 0 ? 8 : _sparse.Length;
            while (n <= entityId) n *= 2;
            var ns = new int[n];
            for (int i = 0; i < n; i++) ns[i] = -1;
            Array.Copy(_sparse, ns, _sparse.Length);
            _sparse = ns;
        }

        public ref T Add(int entityId, in T value)
        {
            EnsureSparse(entityId);
            int existing = _sparse[entityId];
            if (existing >= 0)
            {
                _denseValue[existing] = value; // overwrite (upsert)
                return ref _denseValue[existing];
            }
            if (_count == _denseEntity.Length)
            {
                Array.Resize(ref _denseEntity, _count * 2);
                Array.Resize(ref _denseValue, _count * 2);
            }
            _denseEntity[_count] = entityId;
            _denseValue[_count] = value;
            _sparse[entityId] = _count;
            _count++;
            return ref _denseValue[_count - 1];
        }

        public bool Has(int entityId) => entityId < _sparse.Length && _sparse[entityId] >= 0;

        public ref T Get(int entityId) => ref _denseValue[_sparse[entityId]];

        /// <summary>The packed dense value array as a span [0, Count) — bulk component access.</summary>
        public Span<T> Values() => new Span<T>(_denseValue, 0, _count);

        public void Remove(int entityId)
        {
            if (entityId >= _sparse.Length) return;
            int idx = _sparse[entityId];
            if (idx < 0) return;
            int last = _count - 1;
            // Swap the last dense element into the hole.
            int movedEntity = _denseEntity[last];
            _denseEntity[idx] = movedEntity;
            _denseValue[idx] = _denseValue[last];
            _sparse[movedEntity] = idx;
            _sparse[entityId] = -1;
            _denseValue[last] = default; // drop any managed refs the struct may hold
            _denseEntity[last] = -1;
            _count--;
        }
    }
}
