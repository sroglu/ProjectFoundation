#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// Enforces the project-wide "require ServerOperation" policy at compile time. When the compiled assembly
    /// carries <c>[assembly: PFound.NetworkLayer.RequireServerOperationFlow]</c>, this reports
    /// <c>PFNET0010</c> (Error) on every raw direct <c>Execute</c> invocation whose target sits on a
    /// <c>[RemoteProcedure]</c> operation — steering those calls onto a <c>ServerOperation</c> instead. A
    /// <c>[Notify]</c> <c>Send(...)</c> is left alone (one-way, no server-authoritative lifecycle), and when the
    /// attribute is absent the analyzer reports nothing at all (free mode, zero friction).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RequireServerOperationAnalyzer : DiagnosticAnalyzer
    {
        const string PolicyAttribute = "PFound.NetworkLayer.RequireServerOperationFlowAttribute";
        const string RemoteProcedureAttribute = "PFound.NetworkLayer.RemoteProcedureAttribute";
        const string CallEntryPointName = "Execute";

        static readonly DiagnosticDescriptor RequireServerOperationRule = new DiagnosticDescriptor(
            id: "PFNET0010",
            title: "Server calls must go through a ServerOperationFlow",
            messageFormat: "This assembly requires server calls to go through a ServerOperationFlow "
                + "([RequireServerOperationFlow] is set). Build a ServerOperationFlow for '{0}' instead of calling "
                + "the direct Execute shortcut.",
            category: "PFound.NetworkLayer",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "When an assembly is marked [assembly: RequireServerOperationFlow], every mutating server "
                + "call must be expressed as a ServerOperationFlow. The raw generated direct Execute shortcut on a "
                + "[RemoteProcedure] operation is forbidden so it cannot be reached for by mistake (the "
                + "flow-running Execute the code fix adds is allowed). Remove the assembly attribute to allow the "
                + "direct Execute again (free mode).");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(RequireServerOperationRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            // Skip generated code — the generated direct Execute convenience overload itself calls Execute, and
            // that internal wiring is not a call site we want to flag.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(static start =>
            {
                var policyAttribute = start.Compilation.GetTypeByMetadataName(PolicyAttribute);
                var remoteProcedureAttribute = start.Compilation.GetTypeByMetadataName(RemoteProcedureAttribute);

                // The policy types are unresolvable, or the switch is off for this assembly → free mode, report
                // nothing. Gating everything here keeps free projects completely unaffected.
                if (policyAttribute is null || remoteProcedureAttribute is null)
                    return;
                if (!AssemblyRequiresServerOperation(start.Compilation.Assembly, policyAttribute))
                    return;

                start.RegisterOperationAction(
                    operationContext => AnalyzeInvocation(operationContext, remoteProcedureAttribute),
                    OperationKind.Invocation);
            });
        }

        static bool AssemblyRequiresServerOperation(IAssemblySymbol assembly, INamedTypeSymbol policyAttribute)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, policyAttribute))
                    return true;
            }
            return false;
        }

        static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol remoteProcedureAttribute)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var target = invocation.TargetMethod;
            if (target.Name != CallEntryPointName)
                return;

            // Flag only the raw direct request→reply shortcut, which returns a Task<TReply> (generic). The
            // flow-running Execute the code fix adds returns a plain non-generic Task and IS the sanctioned entry
            // point, so it must never be flagged even though it shares the Execute name on the same operation.
            if (!IsDirectShortcut(target))
                return;

            // Flag only Execute that lives on a generated [RemoteProcedure] operation — precise, so any unrelated
            // Execute method (a task helper, a third-party client) is never touched. A [Notify] Send(...) is a
            // different method name and is inherently ignored.
            if (!HasRemoteProcedureAttribute(target.ContainingType, remoteProcedureAttribute))
                return;

            var operationName = target.ContainingType.Name;
            context.ReportDiagnostic(Diagnostic.Create(
                RequireServerOperationRule, invocation.Syntax.GetLocation(), operationName));
        }

        // Both Executes now return the reply DTO (Task<TReply>), so the flagged one is distinguished by ACCESSIBILITY:
        // the generator emits the direct request→reply shortcut as INTERNAL under the mandatory policy, while the
        // sanctioned flow-running Execute is always PUBLIC. So flag only the static, internal Execute.
        static bool IsDirectShortcut(IMethodSymbol target)
            => target.IsStatic && target.DeclaredAccessibility == Accessibility.Internal;

        static bool HasRemoteProcedureAttribute(INamedTypeSymbol? type, INamedTypeSymbol remoteProcedureAttribute)
        {
            if (type is null)
                return false;
            return type.GetAttributes().Any(
                a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, remoteProcedureAttribute));
        }
    }
}
