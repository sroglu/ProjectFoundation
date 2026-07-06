using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;

namespace PFound.LoopScheduler
{
    /// <summary>
    /// Deterministic multi-phase frame scheduler injected into Unity's PlayerLoop. Replaces
    /// scattered <c>Update()</c> with ordered <see cref="LoopPhase"/> phases plus owner-scoped,
    /// re-entrancy-safe callbacks and per-phase Profiler markers.
    ///
    /// Persistent callbacks attach to the Update phase (<see cref="RegisterUpdateLoop"/>) or the
    /// pre-render hook (<see cref="RegisterBeforeRenderLoop"/>); <see cref="InvokeOnceAt"/> runs a
    /// one-shot at any phase. Logic phases run in one early pipeline pass each frame;
    /// <see cref="LoopPhase.BeforeRender"/> runs on the engine's pre-render hook.
    /// </summary>
    public static class LoopScheduler
    {
        private static readonly PhaseCallbacks[] s_phases;
        private static readonly ProfilerMarker[] s_markers;
        private static readonly Func<object, bool> s_ownerIsDead = OwnerIsDead;
        private static bool s_injected;

        static LoopScheduler()
        {
            int n = Enum.GetValues(typeof(LoopPhase)).Length;
            s_phases = new PhaseCallbacks[n];
            s_markers = new ProfilerMarker[n];
            for (int i = 0; i < n; i++)
            {
                s_phases[i] = new PhaseCallbacks();
                s_markers[i] = new ProfilerMarker("LoopScheduler." + (LoopPhase)i);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (s_injected) return;
            s_injected = true;
            InjectIntoPlayerLoop();
            Application.onBeforeRender -= RunBeforeRender;
            Application.onBeforeRender += RunBeforeRender;
        }

        /// <summary>Adds a persistent callback to the Update phase; auto-removed when <paramref name="owner"/> dies.</summary>
        public static void RegisterUpdateLoop(Action callback, UnityEngine.Object owner) =>
            s_phases[(int)LoopPhase.Update].Add(callback, owner, once: false);

        public static void DeregisterUpdateLoop(Action callback) =>
            s_phases[(int)LoopPhase.Update].Remove(callback);

        /// <summary>Adds a persistent callback to the pre-render hook; auto-removed when <paramref name="owner"/> dies.</summary>
        public static void RegisterBeforeRenderLoop(Action callback, UnityEngine.Object owner) =>
            s_phases[(int)LoopPhase.BeforeRender].Add(callback, owner, once: false);

        public static void DeregisterBeforeRenderLoop(Action callback) =>
            s_phases[(int)LoopPhase.BeforeRender].Remove(callback);

        /// <summary>Runs <paramref name="callback"/> once at the next occurrence of <paramref name="phase"/>.</summary>
        public static void InvokeOnceAt(LoopPhase phase, Action callback) =>
            s_phases[(int)phase].Add(callback, null, once: true);

        private static void InjectIntoPlayerLoop()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();
            var custom = new PlayerLoopSystem { type = typeof(LoopScheduler), updateDelegate = RunLogicPhases };

            var subs = root.subSystemList;
            var updated = new PlayerLoopSystem[subs.Length + 1];
            updated[0] = custom; // first each frame, before the engine's own subsystems
            Array.Copy(subs, 0, updated, 1, subs.Length);
            root.subSystemList = updated;

            PlayerLoop.SetPlayerLoop(root);
        }

        private static void RunLogicPhases()
        {
            for (int i = 0; i < s_phases.Length; i++)
            {
                if (i == (int)LoopPhase.BeforeRender) continue; // driven by the pre-render hook
                RunPhase(i);
            }
        }

        private static void RunBeforeRender() => RunPhase((int)LoopPhase.BeforeRender);

        private static void RunPhase(int i)
        {
            s_phases[i].PruneDeadOwners(s_ownerIsDead);
            using (s_markers[i].Auto())
                s_phases[i].Invoke();
        }

        // True when the owner is a destroyed UnityEngine.Object (fake-null). A null owner means
        // "caller-managed lifetime" and is never pruned.
        private static bool OwnerIsDead(object owner) => owner is UnityEngine.Object uo && uo == null;
    }
}
