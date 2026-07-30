using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    public sealed partial class World
    {
        /// <summary>
        /// A fresh command buffer bound to this world. Record structural changes during
        /// iteration, then <see cref="CommandBuffer.Playback"/> them once iteration is done.
        /// </summary>
        public CommandBuffer CreateCommandBuffer() => new CommandBuffer(this);
    }

    /// <summary>
    /// Records deferred structural changes (create / destroy / add / remove) and applies them
    /// in recorded order on <see cref="Playback"/>. Lets systems mutate structure mid-iteration
    /// without invalidating the query being walked. Component values are kept in typed stores
    /// (no boxing). Placeholder entities returned by <see cref="Create"/> resolve to the real
    /// entity created during playback. Not thread-safe; main-thread use only.
    /// </summary>
    public sealed class CommandBuffer
    {
        private enum Op { Create, Destroy, Add, Remove }

        private readonly struct Cmd
        {
            public readonly Op Operation;
            public readonly Entity Target;
            public readonly int TypeId;     // for Add/Remove
            public readonly int ValueIndex; // index into the type's store, for Add

            public Cmd(Op op, Entity target, int typeId, int valueIndex)
            {
                Operation = op; Target = target; TypeId = typeId; ValueIndex = valueIndex;
            }
        }

        // Type-erased view of a per-component-type value store, so Playback can apply an
        // Add/Remove without knowing T at the call site (the store closes over T).
        private interface IDeferredStore
        {
            void ApplyAdd(World w, Entity e, int index);
            void ApplyRemove(World w, Entity e);
            void Clear();
        }

        private sealed class DeferredStore<T> : IDeferredStore where T : struct
        {
            private T[] _items = new T[4];
            private int _count;

            public int Record(in T value)
            {
                if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
                _items[_count] = value;
                return _count++;
            }

            public void ApplyAdd(World w, Entity e, int index) => w.Add(e, _items[index]);
            public void ApplyRemove(World w, Entity e) => w.RemoveIfExists<T>(e);

            public void Clear()
            {
                Array.Clear(_items, 0, _count); // drop any managed refs held by the structs
                _count = 0;
            }
        }

        private readonly World _world;
        private readonly List<Cmd> _cmds = new List<Cmd>();
        private readonly Dictionary<int, IDeferredStore> _stores = new Dictionary<int, IDeferredStore>();
        private readonly Dictionary<int, Entity> _resolved = new Dictionary<int, Entity>();
        private int _placeholderCount;

        internal CommandBuffer(World world) { _world = world; }

        /// <summary>Number of recorded-but-not-yet-played-back commands.</summary>
        public int Count => _cmds.Count;

        /// <summary>
        /// Reserves a placeholder entity. It is not alive until <see cref="Playback"/>; use it
        /// as the target of deferred <see cref="Add{T}"/> calls. Placeholder ids are negative
        /// (≤ -2) so they never collide with real or <see cref="Entity.Invalid"/> handles.
        /// </summary>
        public Entity Create()
        {
            var placeholder = new Entity(-2 - _placeholderCount, 0);
            _placeholderCount++;
            _cmds.Add(new Cmd(Op.Create, placeholder, 0, 0));
            return placeholder;
        }

        public void Destroy(Entity entity) => _cmds.Add(new Cmd(Op.Destroy, entity, 0, 0));

        public void Add<T>(Entity entity, in T value) where T : struct
        {
            int typeId = ComponentType<T>.Id;
            int index = Store<T>(typeId).Record(value);
            _cmds.Add(new Cmd(Op.Add, entity, typeId, index));
        }

        public void Remove<T>(Entity entity) where T : struct
        {
            int typeId = ComponentType<T>.Id;
            Store<T>(typeId); // ensure a store exists so Playback can dispatch ApplyRemove
            _cmds.Add(new Cmd(Op.Remove, entity, typeId, -1));
        }

        /// <summary>Applies every recorded command in order, then empties the buffer for reuse.</summary>
        public void Playback()
        {
            for (int i = 0; i < _cmds.Count; i++)
            {
                var cmd = _cmds[i];
                switch (cmd.Operation)
                {
                    case Op.Create:
                        _resolved[cmd.Target.Id] = _world.Create();
                        break;
                    case Op.Destroy:
                        _world.Destroy(Resolve(cmd.Target));
                        break;
                    case Op.Add:
                        _stores[cmd.TypeId].ApplyAdd(_world, Resolve(cmd.Target), cmd.ValueIndex);
                        break;
                    case Op.Remove:
                        _stores[cmd.TypeId].ApplyRemove(_world, Resolve(cmd.Target));
                        break;
                }
            }
            Reset();
        }

        private Entity Resolve(Entity target) =>
            target.Id <= -2 ? _resolved[target.Id] : target;

        private DeferredStore<T> Store<T>(int typeId) where T : struct
        {
            if (_stores.TryGetValue(typeId, out var s)) return (DeferredStore<T>)s;
            var store = new DeferredStore<T>();
            _stores[typeId] = store;
            return store;
        }

        private void Reset()
        {
            _cmds.Clear();
            _resolved.Clear();
            _placeholderCount = 0;
            foreach (var store in _stores.Values) store.Clear();
        }
    }
}
