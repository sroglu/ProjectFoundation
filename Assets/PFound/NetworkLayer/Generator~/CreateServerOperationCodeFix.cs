#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// The Alt+Enter fix for <c>PFNET0010</c> ("this assembly requires a ServerOperation"). When the developer
    /// lands on a raw direct <c>&lt;Op&gt;.Execute(...)</c> the policy forbids, this offers
    /// "Create ServerOperation flow for &lt;Op&gt;" and inserts a ready-to-fill
    /// <c>&lt;Op&gt;Flow : ServerOperationFlow&lt;&lt;Op&gt;.RequestMessage, &lt;Op&gt;.ReplyMessage, ServerOperationResult&gt;</c>
    /// class into the operation's OWN declaring file, right after the operation class (one file per operation). It
    /// inserts ONLY the flow — never an <c>Execute</c> on the operation partial: the source generator emits the
    /// operation's static <c>Execute(&lt;request members&gt;)</c> that news this flow up, so the operation class stays
    /// empty. The generated request/reply envelopes are already plugged in; the flow's ctor mirrors the request
    /// DTO's members and the lifecycle hook bodies are left as <c>NotImplementedException</c> so the file still
    /// compiles. Shipped in the same DLL as the analyzer/generator; the IDE loads it while Unity's own compiler
    /// ignores it (it only ever looks for analyzers and source generators).
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CreateServerOperationCodeFix)), Shared]
    public sealed class CreateServerOperationCodeFix : CodeFixProvider
    {
        const string DiagnosticId = "PFNET0010";
        const string RemoteProcedureAttribute = "PFound.NetworkLayer.RemoteProcedureAttribute";
        const string KeyAttribute = "MessagePack.KeyAttribute";
        const string ServerOperationNamespace = "PFound.ServerOperation.Core";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

        // No fix-all: several call sites of the same operation would each try to insert the same flow class.
        // One call site at a time is correct.
        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
                return;

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
                return;

            var solution = context.Document.Project.Solution;

            foreach (var diagnostic in context.Diagnostics)
            {
                var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                if (invocation is null)
                    continue;

                // Resolve the flagged Execute's operation type. If anything is unresolved (a broken build
                // state, a non-[RemoteProcedure] target), offer no fix rather than scaffolding garbage.
                var scaffold = DescribeOperation(semanticModel, invocation, context.CancellationToken);
                if (scaffold is null)
                    continue;

                var op = scaffold.Value;

                // Primary placement: insert the flow class into the operation's OWN declaring file, right after
                // the operation class (same file + namespace). Skip when the flow already exists (never duplicate).
                if (TryGetOperationDeclaration(op.OperationType, solution, context.CancellationToken, out var operationDocument, out var operationDeclaration)
                    && !FlowClassAlreadyExists(operationDeclaration, op.OperationName + "Flow"))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Create ServerOperation flow for {op.OperationName}",
                            createChangedSolution: ct => InsertFlowIntoDeclaringFileAsync(operationDocument, operationDeclaration, op, ct),
                            equivalenceKey: "PFNET0010.CreateServerOperationFlow"),
                        diagnostic);
                    continue;
                }

                // Fallback: the operation type has no resolvable declaring document in this solution (e.g. it
                // lives in a referenced assembly). Insert the flow at the call site instead of failing.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Create ServerOperation flow for {op.OperationName}",
                        createChangedSolution: ct => InsertFlowAtCallSiteAsync(context.Document, invocation, op, ct),
                        equivalenceKey: "PFNET0010.CreateServerOperationFlow"),
                    diagnostic);
            }
        }

        readonly struct OperationScaffold
        {
            public readonly INamedTypeSymbol OperationType;
            public readonly string OperationName;
            public readonly string OperationTypeFqn;
            public readonly string RequestDtoFqn;
            public readonly ImmutableArray<RequestMember> RequestMembers;

            public OperationScaffold(INamedTypeSymbol operationType, string operationName, string operationTypeFqn, string requestDtoFqn, ImmutableArray<RequestMember> requestMembers)
            {
                OperationType = operationType;
                OperationName = operationName;
                OperationTypeFqn = operationTypeFqn;
                RequestDtoFqn = requestDtoFqn;
                RequestMembers = requestMembers;
            }
        }

        // One keyed member of the request DTO, mirrored from the operation's [RemoteProcedure] request type — the
        // flow's ctor takes exactly these (so the generated Execute(<members>) can news the flow up), and
        // BuildRequest reassembles the DTO from them.
        readonly struct RequestMember
        {
            public readonly int Index;
            public readonly string Name;
            public readonly string TypeFqn;
            public RequestMember(int index, string name, string typeFqn) { Index = index; Name = name; TypeFqn = typeFqn; }
        }

        static OperationScaffold? DescribeOperation(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellation)
        {
            if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method)
                return null;

            var operationType = method.ContainingType;
            if (operationType is null)
                return null;

            var remoteProcedure = operationType.GetAttributes().FirstOrDefault(
                a => a.AttributeClass?.ToDisplayString() == RemoteProcedureAttribute);
            if (remoteProcedure is null)
                return null;

            // RemoteProcedure(domain, op, typeof(TRequest), typeof(TReply)) — validate the operation is a
            // well-formed remote procedure before offering the fix; the flow binds against the operation's
            // generated RequestMessage/ReplyMessage envelopes (what ServerOperationContext.Transport carries).
            var args = remoteProcedure.ConstructorArguments;
            if (args.Length != 4)
                return null;
            if (args[2].Value is not INamedTypeSymbol requestDto || args[3].Value is not INamedTypeSymbol)
                return null;

            return new OperationScaffold(
                operationType,
                operationType.Name,
                operationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                requestDto.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                KeyedMembers(requestDto));
        }

        // The request DTO's [Key]-tagged members, in key order — the flow ctor mirrors these.
        static ImmutableArray<RequestMember> KeyedMembers(INamedTypeSymbol dto)
        {
            var members = new List<RequestMember>();
            foreach (var member in dto.GetMembers())
            {
                (string Name, ITypeSymbol Type)? m = member switch
                {
                    IPropertySymbol p when !p.IsStatic && !p.IsIndexer => (p.Name, p.Type),
                    IFieldSymbol f when !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared => (f.Name, f.Type),
                    _ => ((string, ITypeSymbol)?)null,
                };
                if (m is null)
                    continue;

                foreach (var ka in member.GetAttributes())
                {
                    if (ka.AttributeClass?.ToDisplayString() != KeyAttribute)
                        continue;
                    if (ka.ConstructorArguments.Length == 1 && ka.ConstructorArguments[0].Value is int index && index >= 0)
                        members.Add(new RequestMember(index, m.Value.Name,
                            m.Value.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }
            return members.OrderBy(f => f.Index).ToImmutableArray();
        }

        // Resolve the document + type declaration that declares the operation type. Returns false when the type
        // declares no syntax in this solution (nothing to sit next to → the caller falls back to call-site insertion).
        static bool TryGetOperationDeclaration(INamedTypeSymbol operationType, Solution solution, CancellationToken cancellation, out Document document, out TypeDeclarationSyntax declaration)
        {
            document = null!;
            declaration = null!;
            foreach (var reference in operationType.DeclaringSyntaxReferences)
            {
                var owningDocument = solution.GetDocument(reference.SyntaxTree);
                if (owningDocument is null)
                    continue;
                if (reference.GetSyntax(cancellation) is not TypeDeclarationSyntax syntax)
                    continue;
                document = owningDocument;
                declaration = syntax;
                return true;
            }
            return false;
        }

        static bool FlowClassAlreadyExists(TypeDeclarationSyntax operationDeclaration, string flowClassName)
        {
            var root = operationDeclaration.SyntaxTree.GetRoot();
            return root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Any(t => t.Identifier.ValueText == flowClassName);
        }

        static async Task<Solution> InsertFlowIntoDeclaringFileAsync(Document operationDocument, TypeDeclarationSyntax operationDeclaration, OperationScaffold op, CancellationToken cancellation)
        {
            var solution = operationDocument.Project.Solution;

            var root = await operationDocument.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
            if (root is null)
                return solution;

            // The anchor node from the freshly-read root (span-matched so InsertNodesAfter targets this tree).
            var anchor = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(t => t.Span == operationDeclaration.Span && t.Identifier.ValueText == op.OperationName);
            if (anchor is null)
                return solution;

            var flowClass = SyntaxFactory.ParseMemberDeclaration(BuildFlowClassText(op))?.WithAdditionalAnnotations(Formatter.Annotation);
            if (flowClass is null)
                return solution;

            var newRoot = root.InsertNodesAfter(anchor, new[] { flowClass });
            newRoot = EnsureUsing(newRoot, ServerOperationNamespace);
            return solution.WithDocumentSyntaxRoot(operationDocument.Id, newRoot);
        }

        // The flow class inserted next to the operation. This is the ONLY thing the fix adds — the source generator
        // emits the operation's static Execute(<request DTO>) (plus a members convenience overload) that news this
        // flow up, so the operation partial stays empty. The flow's ctor takes the request DTO itself (the exact type
        // the generated Execute passes), and BuildRequest just wraps it — so a single-field DTO reads as its own type
        // (e.g. PlayerId), not a decomposed primitive. Envelope names resolve because the flow sits in the operation's
        // own namespace, and this file carries `using PFound.ServerOperation.Core;` (added if missing); the base ctor
        // resolves the context from ServerOperationHost.Current — so the author fills only PreCheck / Interpret /
        // ApplySuccess.
        static string BuildFlowClassText(OperationScaffold op)
        {
            var request = op.OperationName + ".RequestMessage";
            var reply = op.OperationName + ".ReplyMessage";

            return
$@"/// <summary>Server-authoritative flow for {op.OperationName} — fill PreCheck / Interpret / ApplySuccess.</summary>
public sealed class {op.OperationName}Flow
    : ServerOperationFlow<{request}, {reply}, ServerOperationResult>
{{
    readonly {op.RequestDtoFqn} _request;

    public {op.OperationName}Flow({op.RequestDtoFqn} request) => _request = request;

    protected override ServerOperationResult PreCheck() => ServerOperationResult.Success();

    protected override {request} BuildRequest() => new {request} {{ Content = _request }};

    protected override ServerOperationResult Interpret({reply} reply) => throw new System.NotImplementedException();

    protected override void ApplySuccess(ServerOperationResult result) => throw new System.NotImplementedException();
}}";
        }

        static SyntaxNode EnsureUsing(SyntaxNode root, string namespaceName)
        {
            if (root is not CompilationUnitSyntax compilationUnit)
                return root;
            if (compilationUnit.Usings.Any(u => u.Name?.ToString() == namespaceName))
                return root;
            var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
                .WithAdditionalAnnotations(Formatter.Annotation);
            return compilationUnit.AddUsings(directive);
        }

        // Fallback path only: append the flow as a member of the call site's document, fully qualified so it
        // resolves regardless of that file's namespace.
        static async Task<Solution> InsertFlowAtCallSiteAsync(Document callSiteDocument, InvocationExpressionSyntax invocation, OperationScaffold scaffold, CancellationToken cancellation)
        {
            var solution = callSiteDocument.Project.Solution;

            var member = BuildFlowMemberFullyQualified(scaffold);
            if (member is null)
                return solution;

            var callSiteRoot = await callSiteDocument.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
            if (callSiteRoot is null)
                return solution;

            var containingNamespace = invocation.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            SyntaxNode newCallSiteRoot;
            if (containingNamespace is not null)
                newCallSiteRoot = callSiteRoot.ReplaceNode(containingNamespace, containingNamespace.AddMembers(member));
            else if (callSiteRoot is CompilationUnitSyntax compilationUnit)
                newCallSiteRoot = compilationUnit.AddMembers(member);
            else
                return solution;

            return solution.WithDocumentSyntaxRoot(callSiteDocument.Id, newCallSiteRoot);
        }

        static MemberDeclarationSyntax? BuildFlowMemberFullyQualified(OperationScaffold scaffold)
        {
            var op = scaffold.OperationName;
            var request = scaffold.OperationTypeFqn + ".RequestMessage";
            var reply = scaffold.OperationTypeFqn + ".ReplyMessage";
            const string result = "global::PFound.ServerOperation.Core.ServerOperationResult";

            // Fallback path: the operation lives in a referenced assembly, so its partial cannot be re-opened here.
            // Only the flow is emitted; construct it directly and await RunAsync. The ctor takes the request DTO
            // itself (the exact type the generated Execute passes) and BuildRequest just wraps it; the base ctor
            // resolves the context from ServerOperationHost.Current.
            var text =
$@"/// <summary>Server-authoritative flow for {op} — fill PreCheck / Interpret / ApplySuccess.</summary>
public sealed class {op}Flow
    : global::PFound.ServerOperation.Core.ServerOperationFlow<{request}, {reply}, {result}>
{{
    readonly {scaffold.RequestDtoFqn} _request;

    public {op}Flow({scaffold.RequestDtoFqn} request) => _request = request;

    protected override {result} PreCheck() => {result}.Success();

    protected override {request} BuildRequest() => new {request} {{ Content = _request }};

    protected override {result} Interpret({reply} reply) => throw new global::System.NotImplementedException();

    protected override void ApplySuccess({result} result) => throw new global::System.NotImplementedException();
}}
";

            return SyntaxFactory.ParseMemberDeclaration(text)?.WithAdditionalAnnotations(Formatter.Annotation);
        }

        static string ParamName(string memberName)
        {
            var camel = char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);
            return IsKeyword(camel) ? "@" + camel : camel;
        }

        // The backing field name — a leading underscore already keeps it clear of keywords, so no @-escape.
        static string FieldName(string memberName)
            => "_" + char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);

        static bool IsKeyword(string s) => Array.IndexOf(Keywords, s) >= 0;

        static readonly string[] Keywords =
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
            "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
            "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
            "internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
            "private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
            "static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
            "unsafe","ushort","using","virtual","void","volatile","while",
        };
    }
}
