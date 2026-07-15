using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.LowLevel;

namespace PFound.LoopScheduler
{
    /// <summary>
    /// PlayerLoop utilities for <see cref="LoopScheduler"/>: idempotent injection helpers used at
    /// install time, plus inspection/debug tooling to enumerate the injected runner nodes and
    /// pretty-print the current PlayerLoop tree.
    /// </summary>
    public static class LoopSchedulerTools
    {
        // --- injected node enumeration ------------------------------------------------------

        /// <summary>The runner node types the scheduler injects into the PlayerLoop.</summary>
        public static IEnumerable<Type> CustomLoops => LoopScheduler.NodeTypes;

        /// <summary>Invokes <paramref name="action"/> for each injected runner node type.</summary>
        public static void ForEachCustomLoop(Action<Type> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var type in LoopScheduler.NodeTypes)
                action(type);
        }

        /// <summary>True if <paramref name="system"/> is one of the scheduler's injected runner nodes.</summary>
        public static bool IsCustomLoop(this PlayerLoopSystem system)
        {
            if (system.type == null) return false;
            foreach (var type in LoopScheduler.NodeTypes)
                if (system.type == type) return true;
            return false;
        }

        // --- debug dump ---------------------------------------------------------------------

        /// <summary>Logs the current PlayerLoop tree, highlighting the scheduler's injected nodes.</summary>
        public static void LogPlayerLoop() => Debug.Log(DumpPlayerLoop());

        /// <summary>Returns a pretty-printed tree of the current PlayerLoop, marking injected nodes.</summary>
        public static string DumpPlayerLoop() => DumpPlayerLoop(PlayerLoop.GetCurrentPlayerLoop());

        /// <summary>Returns a pretty-printed tree of <paramref name="root"/>, marking injected nodes.</summary>
        public static string DumpPlayerLoop(PlayerLoopSystem root)
        {
            var text = new StringBuilder();
            Append(root, text, 0);
            return text.ToString();

            static void Append(PlayerLoopSystem system, StringBuilder text, int depth)
            {
                if (system.type != null)
                {
                    for (int i = 0; i < depth; i++) text.Append('\t');
                    text.AppendLine(system.IsCustomLoop()
                                        ? $"<color=#00FF00>{system.type.Name}</color>"
                                        : system.type.Name);
                }

                if (system.subSystemList == null) return;
                depth++;
                foreach (var child in system.subSystemList)
                    Append(child, text, depth);
            }
        }

        // --- injection helpers (idempotent) -------------------------------------------------

        /// <summary>
        /// Inserts <paramref name="node"/> as a root sibling immediately after the subsystem of type
        /// <typeparamref name="TAnchor"/>. No-op if a node of the same type is already present.
        /// </summary>
        internal static PlayerLoopSystem InjectAfter<TAnchor>(this in PlayerLoopSystem root, PlayerLoopSystem node)
            where TAnchor : struct =>
            root.IsAlreadyInjected(node.type) ? root : root.InsertSibling<TAnchor>(node);

        /// <summary>
        /// Appends <paramref name="node"/> as a subsystem of the subsystem of type
        /// <typeparamref name="TAnchor"/>. No-op if a node of the same type is already present.
        /// </summary>
        internal static PlayerLoopSystem InjectAsSubsystem<TAnchor>(this in PlayerLoopSystem root, PlayerLoopSystem node)
            where TAnchor : struct =>
            root.IsAlreadyInjected(node.type) ? root : root.AppendSubsystem<TAnchor>(node);

        private static bool IsAlreadyInjected(this PlayerLoopSystem system, Type type)
        {
            if (system.type == type) return true;
            if (system.subSystemList == null) return false;
            foreach (var child in system.subSystemList)
                if (child.IsAlreadyInjected(type)) return true;
            return false;
        }

        private static PlayerLoopSystem InsertSibling<TAnchor>(this in PlayerLoopSystem root, PlayerLoopSystem node)
            where TAnchor : struct
        {
            var result = root;
            var subs = new List<PlayerLoopSystem>(result.subSystemList.Length + 1);
            foreach (var child in result.subSystemList)
            {
                subs.Add(child);
                if (child.type == typeof(TAnchor)) subs.Add(node);
            }
            result.subSystemList = subs.ToArray();
            return result;
        }

        private static PlayerLoopSystem AppendSubsystem<TAnchor>(this in PlayerLoopSystem root, PlayerLoopSystem node)
            where TAnchor : struct
        {
            var result = root;
            var subs = new List<PlayerLoopSystem>(result.subSystemList.Length);
            foreach (var child in result.subSystemList)
            {
                if (child.type == typeof(TAnchor))
                {
                    var updated = child;
                    var existing = child.subSystemList;
                    var list = new List<PlayerLoopSystem>(existing?.Length + 1 ?? 1);
                    if (existing != null) list.AddRange(existing);
                    list.Add(node);
                    updated.subSystemList = list.ToArray();
                    subs.Add(updated);
                }
                else
                {
                    subs.Add(child);
                }
            }
            result.subSystemList = subs.ToArray();
            return result;
        }
    }
}
