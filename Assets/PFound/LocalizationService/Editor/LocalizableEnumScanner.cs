using System;
using System.Collections.Generic;

namespace PFound.LocalizationService.EditorTools
{
    /// <summary>
    /// Discovers <c>[LocalizableEnum]</c> enums across the loaded assemblies so their values can be
    /// auto-added to the tables. Framework/vendor assemblies are skipped by name prefix to keep the scan
    /// cheap and to avoid reflecting types that can never be user localizable enums.
    /// </summary>
    public static class LocalizableEnumScanner
    {
        private static readonly string[] SkippedAssemblyPrefixes =
        {
            "Unity", "System", "mscorlib", "Mono.", "netstandard", "nunit", "Newtonsoft"
        };

        public static List<Type> Scan()
        {
            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (IsSkipped(name)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }

                foreach (var type in types)
                {
                    if (type != null && EnumKeyGenerator.IsLocalizable(type))
                        result.Add(type);
                }
            }
            return result;
        }

        private static bool IsSkipped(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return true;
            for (int i = 0; i < SkippedAssemblyPrefixes.Length; i++)
                if (assemblyName.StartsWith(SkippedAssemblyPrefixes[i], StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
