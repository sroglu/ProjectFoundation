#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// Enforces "exactly one operation result-code enum per assembly". The ready-made
    /// <c>ServerOperationResult&lt;TCode&gt;</c> is generic over the game's result enum, and the "Create
    /// ServerOperation flow" quick-fix scaffolds a flow against the assembly's <c>[OperationResultCode]</c>-marked
    /// enum. If MORE than one enum carries the marker the choice is ambiguous, so this reports <c>PFNET0011</c>
    /// (Error) on each marked enum. Zero marked enums is allowed (a game may not use the scaffold); only the
    /// ambiguous case is an error.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class OperationResultCodeAnalyzer : DiagnosticAnalyzer
    {
        const string ResultCodeAttribute = "PFound.ServerOperationFlow.Core.OperationResultCodeAttribute";

        static readonly DiagnosticDescriptor MultipleResultCodeEnumsRule = new DiagnosticDescriptor(
            id: "PFNET0011",
            title: "Only one operation result-code enum is allowed per assembly",
            messageFormat: "This assembly marks {0} enums with [OperationResultCode]; exactly one is allowed so "
                + "ServerOperationResult<TCode> and the flow scaffold have a single unambiguous result enum.",
            category: "PFound.NetworkLayer",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ServerOperationResult<TCode> is generic over the game's result-code enum, and the "
                + "'Create ServerOperation flow' quick-fix scaffolds against the [OperationResultCode]-marked enum. "
                + "More than one marked enum makes that choice ambiguous — mark exactly one.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(MultipleResultCodeEnumsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationAction(compilationContext =>
            {
                var compilation = compilationContext.Compilation;
                var marker = compilation.GetTypeByMetadataName(ResultCodeAttribute);
                if (marker is null)
                    return;

                var marked = EnumsInAssembly(compilation.Assembly.GlobalNamespace)
                    .Where(e => e.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, marker)))
                    .ToList();
                if (marked.Count <= 1)
                    return;

                foreach (var markedEnum in marked)
                {
                    foreach (var location in markedEnum.Locations.Where(l => l.IsInSource))
                        compilationContext.ReportDiagnostic(Diagnostic.Create(
                            MultipleResultCodeEnumsRule, location, marked.Count));
                }
            });
        }

        static IEnumerable<INamedTypeSymbol> EnumsInAssembly(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                foreach (var found in EnumsInType(type))
                    yield return found;
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                foreach (var found in EnumsInAssembly(child))
                    yield return found;
            }
        }

        static IEnumerable<INamedTypeSymbol> EnumsInType(INamedTypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
                yield return type;
            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var found in EnumsInType(nested))
                    yield return found;
            }
        }
    }
}
