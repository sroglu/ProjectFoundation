using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kunai
{
    /// <summary>
    /// Generic attribute walker reused by D4 ([KuiOption]) and D5 ([KuiCommand]).
    /// Scans every loaded assembly for static (or any-target, depending on the
    /// attribute's AttributeUsage) members tagged with <typeparamref name="TAttr"/>
    /// and invokes the visitor exactly once per match.
    ///
    /// Per-assembly try/catch: a single broken assembly (e.g. a third-party DLL
    /// missing a transitive dep) is recorded into <see cref="KuiReflectionResult.Errors"/>
    /// and the scan continues with the next assembly.
    /// </summary>
    internal static class KuiReflectionScanner
    {
        // Static + public + non-public + declared-only matches the Phase 2 scope:
        // [KuiOption] / [KuiCommand] target static members, and DeclaredOnly avoids
        // re-visiting members through derived types (the base type already gets them).
        const BindingFlags MemberFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static KuiReflectionResult Scan<TAttr>(Action<MemberInfo, TAttr> visit)
            where TAttr : Attribute
        {
            var result = new KuiReflectionResult { Errors = new List<KuiReflectionError>() };
            if (visit == null) return result;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException tlex)
                {
                    // Salvage the loadable types and continue.
                    var partial = tlex.Types;
                    types = SkipNullEntries(partial);
                    result.Errors.Add(new KuiReflectionError
                    {
                        AssemblyName = SafeName(asm),
                        Message      = tlex.Message ?? "ReflectionTypeLoadException",
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new KuiReflectionError
                    {
                        AssemblyName = SafeName(asm),
                        Message      = ex.Message ?? ex.GetType().Name,
                    });
                    continue;
                }

                ScanTypes(types, visit, ref result);
            }

            return result;
        }

        static void ScanTypes<TAttr>(Type[] types, Action<MemberInfo, TAttr> visit, ref KuiReflectionResult result)
            where TAttr : Attribute
        {
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null) continue;
                result.TypesScanned++;

                // Each per-type call is also wrapped — a single broken type
                // (e.g. generic that can't resolve) shouldn't poison sibling types.
                try
                {
                    var members = t.GetMembers(MemberFlags);
                    for (int m = 0; m < members.Length; m++)
                    {
                        var mi = members[m];
                        // GetMembers returns MethodBase entries (constructors, methods, etc.)
                        // — only consider field/property/method, matching the attribute targets
                        // we currently support.
                        if (mi.MemberType != MemberTypes.Field
                         && mi.MemberType != MemberTypes.Property
                         && mi.MemberType != MemberTypes.Method)
                            continue;

                        var attr = mi.GetCustomAttribute<TAttr>(inherit: true);
                        if (attr == null) continue;

                        result.MembersMatched++;
                        try { visit(mi, attr); }
                        catch (Exception visitEx)
                        {
                            result.Errors.Add(new KuiReflectionError
                            {
                                AssemblyName = SafeName(t.Assembly),
                                Message      = $"visit({t.FullName}.{mi.Name}): {visitEx.Message}",
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new KuiReflectionError
                    {
                        AssemblyName = SafeName(t.Assembly),
                        Message      = $"GetMembers({t.FullName}): {ex.Message}",
                    });
                }
            }
        }

        static Type[] SkipNullEntries(Type[] src)
        {
            if (src == null || src.Length == 0) return Array.Empty<Type>();
            int n = 0;
            for (int i = 0; i < src.Length; i++) if (src[i] != null) n++;
            if (n == src.Length) return src;
            var dst = new Type[n];
            int j = 0;
            for (int i = 0; i < src.Length; i++) if (src[i] != null) dst[j++] = src[i];
            return dst;
        }

        static string SafeName(Assembly asm)
        {
            try { return asm?.GetName().Name ?? "<unknown>"; }
            catch { return "<unknown>"; }
        }
    }
}
