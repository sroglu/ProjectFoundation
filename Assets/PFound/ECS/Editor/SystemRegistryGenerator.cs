using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PFound.ECS;

namespace PFound.ECS.EditorTools
{
    /// <summary>
    /// Editor codegen for the system registry: scans the project for <see cref="SystemBase"/> types
    /// and writes a reflection-free <c>Register(World)</c>. Run it after adding/removing a system.
    /// Reflection (<see cref="SystemDiscovery.FromLoadedAssemblies"/>) stays the parity oracle —
    /// this generator only moves that scan to compile time.
    /// </summary>
    public static class SystemRegistryGenerator
    {
        // The registry lives in the leaf assembly that references every system-defining assembly,
        // and lists only that assembly's systems (so its `new T()` calls compile). The Samples
        // assembly is that consumer; a real app retargets these three constants to its own assembly.
        private const string TargetAssembly = "PFound.ECS.Samples";
        private const string OutputPath = "Assets/PFound/ECS/Samples/SystemRegistry.g.cs";
        private const string Namespace = "PFound.ECS.Samples.Generated";
        private const string ClassName = "SystemRegistry";

        [MenuItem("Tools/PFound ECS/Regenerate System Registry")]
        public static void Generate()
        {
            var systems = SystemDiscovery.SelectSystems(InTargetAssembly(TypeCache.GetTypesDerivedFrom<SystemBase>()));

            // Parity check against the reflection oracle, scoped to the same assembly.
            var oracle = SystemDiscovery.SelectSystems(InTargetAssembly(SystemDiscovery.FromLoadedAssemblies()));
            if (!SameSet(systems, oracle))
                Debug.LogWarning("[PFound.ECS] Registry generation: TypeCache set differs from the " +
                                 "reflection oracle. Generated registry follows TypeCache.");

            var source = SystemRegistryEmitter.Emit(Namespace, ClassName, systems);
            var fullPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            if (File.Exists(fullPath) && File.ReadAllText(fullPath) == source)
            {
                Debug.Log($"[PFound.ECS] System registry already up to date ({systems.Count} system(s)).");
                return;
            }

            File.WriteAllText(fullPath, source);
            AssetDatabase.ImportAsset(OutputPath);
            Debug.Log($"[PFound.ECS] Generated {OutputPath} with {systems.Count} system(s).");
        }

        private static IEnumerable<System.Type> InTargetAssembly(IEnumerable<System.Type> types)
        {
            foreach (var t in types)
                if (t != null && t.Assembly.GetName().Name == TargetAssembly)
                    yield return t;
        }

        private static bool SameSet(List<System.Type> a, List<System.Type> b)
        {
            if (a.Count != b.Count) return false;
            var set = new HashSet<System.Type>(a);
            foreach (var t in b)
                if (!set.Contains(t)) return false;
            return true;
        }
    }
}
