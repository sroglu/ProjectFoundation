using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    // System registry + scheduler on World: register systems, order them by phase and then by
    // [DependsOn] within a phase, filter by [ActiveInScene], and tick on Update()/RunPhase().
    public sealed partial class World
    {
        /// <summary>Unscaled frame delta, set by the host loop; read by Unscaled-time systems.</summary>
        public float UnscaledDeltaTime;

        private readonly List<SystemBase> _systems = new List<SystemBase>();
        private readonly Dictionary<Type, SystemBase> _byType = new Dictionary<Type, SystemBase>();
        private SystemBase[] _ordered = Array.Empty<SystemBase>();
        private bool _systemsDirty;
        private string _currentScene = string.Empty;

        /// <summary>The scene name last set via <see cref="SceneChanged"/> (empty until set).</summary>
        public string CurrentScene => _currentScene;

        /// <summary>Registers and attaches a system instance (runs its lifecycle hooks once).</summary>
        public void RegisterSystem(SystemBase system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            system.Attach(this);
            _systems.Add(system);
            _byType[system.GetType()] = system;
            _systemsDirty = true;
        }

        /// <summary>Returns the registered system of type <typeparamref name="T"/> (throws if absent).</summary>
        public T GetSystem<T>() where T : SystemBase
        {
            if (_byType.TryGetValue(typeof(T), out var system)) return (T)system;
            throw new InvalidOperationException("No registered system of type " + typeof(T).Name + ".");
        }

        /// <summary>Tries to return the registered system of type <typeparamref name="T"/>.</summary>
        public bool TryGetSystem<T>(out T system) where T : SystemBase
        {
            if (_byType.TryGetValue(typeof(T), out var s)) { system = (T)s; return true; }
            system = null;
            return false;
        }

        /// <summary>
        /// Instantiates each type (parameterless ctor) and registers it. This is the seam the
        /// generated registry — or reflection over loaded assemblies — feeds its system type list to.
        /// </summary>
        public void DiscoverSystems(IEnumerable<Type> systemTypes)
        {
            if (systemTypes == null) throw new ArgumentNullException(nameof(systemTypes));
            foreach (var type in systemTypes)
            {
                if (type == null || type.IsAbstract || !typeof(SystemBase).IsAssignableFrom(type))
                    throw new ArgumentException(type + " is not a concrete SystemBase.", nameof(systemTypes));
                RegisterSystem((SystemBase)Activator.CreateInstance(type, nonPublic: true));
            }
        }

        /// <summary>Sets the active scene; re-filters which <see cref="ActiveInSceneAttribute"/> systems run.</summary>
        public void SceneChanged(string sceneName) => _currentScene = sceneName ?? string.Empty;

        /// <summary>
        /// Destructive scene reload (legacy lifecycle): disposes every system, clears all entities /
        /// components / events / interactions, switches to <paramref name="sceneName"/>, then
        /// re-initializes the systems. Use when a scene load should reset world state; the lighter
        /// <see cref="SceneChanged"/> just re-filters scene-scoped systems without a reset.
        /// </summary>
        public void ReloadScene(string sceneName)
        {
            for (int i = _systems.Count - 1; i >= 0; i--) _systems[i].Dispose();
            ClearWorldState();
            _currentScene = sceneName ?? string.Empty;
            for (int i = 0; i < _systems.Count; i++) _systems[i].Reinitialize();
            _systemsDirty = true;
        }

        // Wipes entity/component/event/interaction state; keeps registered systems.
        private void ClearWorldState()
        {
            DestroyAll();
            _events?.Clear();
            _interactions?.Clear();
            _structureVersion++;
        }

        // Tears down and forgets every registered system (World.Dispose).
        private void DisposeSystems()
        {
            for (int i = _systems.Count - 1; i >= 0; i--) _systems[i].Dispose();
        }

        /// <summary>Runs every active system, in phase then dependency order (scaled systems pause when DeltaTime is 0).</summary>
        public void Update()
        {
            EnsureOrdered();
            for (int i = 0; i < _ordered.Length; i++)
            {
                var s = _ordered[i];
                if (ShouldRun(s) && s.IsActiveIn(_currentScene)) s.Run();
            }
        }

        /// <summary>Runs only the active systems of one phase, in dependency order.</summary>
        public void RunPhase(ECSPhase phase)
        {
            EnsureOrdered();
            for (int i = 0; i < _ordered.Length; i++)
            {
                var s = _ordered[i];
                if (s.Phase == phase && ShouldRun(s) && s.IsActiveIn(_currentScene)) s.Run();
            }
        }

        // Pause gate: Scaled systems only run while time advances (DeltaTime > 0); Unscaled always run.
        private bool ShouldRun(SystemBase s) => s.Time == UpdateTime.Unscaled || DeltaTime > 0f;

        private void EnsureOrdered()
        {
            if (!_systemsDirty) return;

            var ordered = new List<SystemBase>(_systems.Count);
            // Phases run in enum declaration order; topo-sort by [DependsOn] within each phase.
            foreach (ECSPhase phase in (ECSPhase[])Enum.GetValues(typeof(ECSPhase)))
            {
                var inPhase = new List<SystemBase>();
                foreach (var s in _systems) // preserve registration order as the tie-break
                    if (s.Phase == phase) inPhase.Add(s);
                TopoSort(inPhase, ordered);
            }

            _ordered = ordered.ToArray();
            _systemsDirty = false;
        }

        // Depth-first topo sort: a system is emitted after the in-phase systems it [DependsOn].
        // A dependency cycle is an error and is reported rather than silently broken.
        private static void TopoSort(List<SystemBase> inPhase, List<SystemBase> output)
        {
            var done = new HashSet<SystemBase>();
            var visiting = new HashSet<SystemBase>();
            foreach (var s in inPhase) Visit(s, inPhase, done, visiting, output);
        }

        private static void Visit(SystemBase s, List<SystemBase> inPhase,
            HashSet<SystemBase> done, HashSet<SystemBase> visiting, List<SystemBase> output)
        {
            if (done.Contains(s)) return;
            if (!visiting.Add(s)) // re-entering a node on the current stack ⇒ real cycle
                throw new InvalidOperationException(
                    "ECS system dependency cycle detected in phase " + s.Phase +
                    ", involving " + s.GetType().Name + ".");

            var deps = s.Dependencies;
            for (int d = 0; d < deps.Length; d++)
            {
                var depType = deps[d];
                foreach (var candidate in inPhase) // only order against deps in the same phase
                    if (candidate.GetType() == depType)
                        Visit(candidate, inPhase, done, visiting, output);
            }

            visiting.Remove(s);
            done.Add(s);
            output.Add(s);
        }
    }
}
