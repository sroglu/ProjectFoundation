using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace PFound.LoopScheduler
{
    /// <summary>
    /// Deterministic multi-phase frame scheduler injected into Unity's PlayerLoop. Replaces
    /// scattered <c>Update()</c> with ordered <see cref="LoopPhase"/> phases plus owner-scoped,
    /// re-entrancy-safe callbacks and per-phase Profiler markers.
    ///
    /// Every phase runs at its real engine moment: a small runner node is injected into each of the
    /// engine's frame slots (early-update, pre-update, update, pre-late-update, post-late-update)
    /// and each runner ticks the phases mapped to that slot in order; <see cref="LoopPhase.BeforeRender"/>
    /// is driven by the engine's pre-render hook. Callbacks are persistent via
    /// <see cref="RegisterAt"/> (any phase) or the <see cref="RegisterUpdateLoop"/> /
    /// <see cref="RegisterBeforeRenderLoop"/> shortcuts, or one-shot via <see cref="InvokeOnceAt"/>.
    /// </summary>
    public static class LoopScheduler
    {
        // --- marker node types: one injected PlayerLoop node per engine slot we tick in ---
        private struct EarlyPhaseRunner { }
        private struct PreUpdatePhaseRunner { }
        private struct UpdatePhaseRunner { }
        private struct PreLatePhaseRunner { }
        private struct PostLatePhaseRunner { }

        // Phase-index groups per engine slot, in run order. Pure data (no engine types) so the
        // ordering/coverage is inspectable and testable without Unity.
        private static readonly int[] s_earlyPhases =
            { (int)LoopPhase.EarlyUpdate, (int)LoopPhase.Scene, (int)LoopPhase.Network };
        private static readonly int[] s_preUpdatePhases =
            { (int)LoopPhase.Input, (int)LoopPhase.Selection };
        private static readonly int[] s_updatePhases =
            { (int)LoopPhase.Update, (int)LoopPhase.PostUpdate };
        private static readonly int[] s_preLatePhases =
            { (int)LoopPhase.LateUpdate, (int)LoopPhase.Camera, (int)LoopPhase.Bindings, (int)LoopPhase.PreRender };
        private static readonly int[] s_postLatePhases =
            { (int)LoopPhase.Render, (int)LoopPhase.PostRender, (int)LoopPhase.EndOfFrame };

        private static readonly PhaseCallbacks[] s_phases;
        private static readonly ProfilerMarker[] s_markers;
        private static readonly Func<object, bool> s_ownerIsDead = OwnerIsDead;
        private static bool s_injected;

        /// <summary>The runner node types injected into the PlayerLoop, for inspection tooling.</summary>
        internal static readonly Type[] NodeTypes =
        {
            typeof(EarlyPhaseRunner), typeof(PreUpdatePhaseRunner), typeof(UpdatePhaseRunner),
            typeof(PreLatePhaseRunner), typeof(PostLatePhaseRunner)
        };

        /// <summary>
        /// True once the frame is past its late-update work (set when the post-late-update runner
        /// begins) and false again at the start of the next frame's early phases. Lets callers that
        /// run in more than one phase tell which side of late-update they are on.
        /// </summary>
        public static bool IsAfterLateUpdate { get; private set; }

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

        // --- persistent registration -------------------------------------------------------

        /// <summary>
        /// Adds a persistent callback to <paramref name="phase"/>; auto-removed when
        /// <paramref name="owner"/> is destroyed. Pass a real owner for bounded-lifetime work; pass
        /// <c>null</c> only when you will <see cref="DeregisterAt"/> it yourself.
        /// </summary>
        public static void RegisterAt(LoopPhase phase, Action callback, UnityEngine.Object owner) =>
            s_phases[(int)phase].Add(callback, owner, once: false);

        /// <summary>Removes a persistent callback previously added to <paramref name="phase"/>.</summary>
        public static void DeregisterAt(LoopPhase phase, Action callback) =>
            s_phases[(int)phase].Remove(callback);

        /// <summary>Adds a persistent callback to the Update phase; auto-removed when <paramref name="owner"/> dies.</summary>
        public static void RegisterUpdateLoop(Action callback, UnityEngine.Object owner) =>
            RegisterAt(LoopPhase.Update, callback, owner);

        public static void DeregisterUpdateLoop(Action callback) =>
            DeregisterAt(LoopPhase.Update, callback);

        /// <summary>Adds a persistent callback to the pre-render hook; auto-removed when <paramref name="owner"/> dies.</summary>
        public static void RegisterBeforeRenderLoop(Action callback, UnityEngine.Object owner) =>
            RegisterAt(LoopPhase.BeforeRender, callback, owner);

        public static void DeregisterBeforeRenderLoop(Action callback) =>
            DeregisterAt(LoopPhase.BeforeRender, callback);

        /// <summary>Runs <paramref name="callback"/> once at the next occurrence of <paramref name="phase"/>.</summary>
        public static void InvokeOnceAt(LoopPhase phase, Action callback) =>
            s_phases[(int)phase].Add(callback, null, once: true);

        // --- injection ---------------------------------------------------------------------

        private static void InjectIntoPlayerLoop()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();

            root = root.InjectAfter<EarlyUpdate>(Node<EarlyPhaseRunner>(RunEarlyGroup))
                       .InjectAfter<PreUpdate>(Node<PreUpdatePhaseRunner>(RunPreUpdateGroup))
                       .InjectAfter<Update>(Node<UpdatePhaseRunner>(RunUpdateGroup))
                       .InjectAfter<PreLateUpdate>(Node<PreLatePhaseRunner>(RunPreLateGroup))
                       .InjectAfter<PostLateUpdate>(Node<PostLatePhaseRunner>(RunPostLateGroup));

            PlayerLoop.SetPlayerLoop(root);
        }

        private static PlayerLoopSystem Node<TNode>(PlayerLoopSystem.UpdateFunction update) where TNode : struct =>
            new PlayerLoopSystem { type = typeof(TNode), updateDelegate = update, subSystemList = null };

        // --- per-slot runners --------------------------------------------------------------

        private static void RunEarlyGroup()
        {
            IsAfterLateUpdate = false;
            RunGroup(s_earlyPhases);
        }

        private static void RunPreUpdateGroup() => RunGroup(s_preUpdatePhases);
        private static void RunUpdateGroup() => RunGroup(s_updatePhases);
        private static void RunPreLateGroup() => RunGroup(s_preLatePhases);

        private static void RunPostLateGroup()
        {
            IsAfterLateUpdate = true;
            RunGroup(s_postLatePhases);
        }

        private static void RunBeforeRender() => RunPhase((int)LoopPhase.BeforeRender);

        private static void RunGroup(int[] phases)
        {
            for (int i = 0; i < phases.Length; i++)
                RunPhase(phases[i]);
        }

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
