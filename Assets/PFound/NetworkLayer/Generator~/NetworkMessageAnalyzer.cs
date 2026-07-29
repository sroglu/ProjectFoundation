#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// Compile-time guard-rails for the network wire contract that the source generator cannot enforce on its
    /// own: wire-index reuse, retired (<c>[Reserved]</c>) index reuse, per-domain opcode collisions, the shape
    /// of the hand-filled operation partials and their <c>ServerOperation</c> subclasses, hand-written wire
    /// types that skip a MessagePack contract, and a perf nudge to pass large wire DTOs by <c>in</c>.
    /// Ships in the SAME assembly as <see cref="NetworkMessageGenerator"/>. It matches the framework types by
    /// their metadata-name strings, because an analyzer cannot reference the runtime assembly it inspects.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NetworkMessageAnalyzer : DiagnosticAnalyzer
    {
        const string Category = "PFound.NetworkLayer";

        // ---- metadata-name anchors (analyzer cannot reference the runtime asmdef) ----
        const string NetworkOpAttr = "PFound.NetworkLayer.NetworkOpAttribute";
        const string NetworkNotifyAttr = "PFound.NetworkLayer.NetworkNotifyAttribute";
        const string RequestAttr = "PFound.NetworkLayer.RequestAttribute";
        const string ReplyAttr = "PFound.NetworkLayer.ReplyAttribute";
        const string FieldAttr = "PFound.NetworkLayer.FieldAttribute";
        const string ReservedAttr = "PFound.NetworkLayer.ReservedAttribute";
        const string RequestMessageBase = "PFound.NetworkLayer.RequestMessage";
        const string ReplyMessageBase = "PFound.NetworkLayer.ReplyMessage";
        const string NotifyMessageBase = "PFound.NetworkLayer.NotifyMessage";
        const string MessagePackObjectAttr = "MessagePack.MessagePackObjectAttribute";
        const string ServerOperationBase = "PFound.ServerOperation.Core.ServerOperation`3";

        const string RefNudgeConfigKey = "pfnet_ref_nudge_min_bytes";
        const int DefaultRefNudgeMinBytes = 16;

        // ---- diagnostics ----
        static readonly DiagnosticDescriptor DuplicateIndex = new(
            "PFNET0001", "Duplicate wire index",
            "Wire index {0} is used by more than one [{1}] field on '{2}' — each index defines one wire slot and must be unique",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Two fields sharing a wire index would collide on the same MessagePack key. Give each field a distinct index.");

        static readonly DiagnosticDescriptor ReservedIndexReuse = new(
            "PFNET0002", "Reuse of a reserved wire index",
            "Wire index {0} on '{1}' is marked [Reserved] (a retired field's number) and must never be reused",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Reusing a retired wire index corrupts data: an old peer's bytes for the deleted field are read as the new field. Pick a fresh, higher index.");

        static readonly DiagnosticDescriptor OpcodeCollision = new(
            "PFNET0003", "Opcode collision within a domain",
            "Domain {0} op {1} is declared by both '{2}' and '{3}' — each request/notify in a domain needs a distinct op value",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Two operations in the same domain sharing an op value would enrol under the same opcode. Give each a distinct op.");

        static readonly DiagnosticDescriptor ServerOperationNotSealed = new(
            "PFNET0004", "ServerOperation subclass should be sealed",
            "ServerOperation subclass '{0}' should be sealed — the lifecycle is meant to be filled, not extended",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "A concrete operation fills the sealed lifecycle's hooks; it is not intended to be a base class. Seal it.");

        static readonly DiagnosticDescriptor ServerOperationPairMismatch = new(
            "PFNET0005", "Mismatched ServerOperation request/reply pair",
            "ServerOperation '{0}' request/reply type arguments are not a matched generated '<Op>.RequestMessage'/'<Op>.ReplyMessage' pair",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "A generated operation's request and reply envelopes are nested in the same op partial. Cross-wired or half-generated pairs are almost always a mistake.");

        static readonly DiagnosticDescriptor WireTypeMissingContract = new(
            "PFNET0006", "Hand-written wire type carries a contractless payload",
            "Wire message '{0}' carries payload '{1}' of type '{2}', which has no [MessagePackObject] contract — declare the DTO with explicit [Key]s or generate the message from a [NetworkOp]/[NetworkNotify]",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "A payload that crosses the wire without a MessagePack contract relies on fragile field-order serialization. Give it [MessagePackObject] + [Key]s, or let the generator emit it.");

        static readonly DiagnosticDescriptor OpTargetNotPartialClass = new(
            "PFNET0007", "Operation attribute needs a partial class",
            "[{0}] must be applied to a 'partial class' so the generator can add the wire types to '{1}'",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "The generator emits the wire structs and envelopes into a second part of this type; it must be declared partial.");

        static readonly DiagnosticDescriptor WireIndexWrongTarget = new(
            "PFNET0008", "Wire-index attribute on the wrong operation kind",
            "[{0}] does not belong on a {1}; use {2} — otherwise the generator silently drops the field",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "[Request]/[Reply] belong on a [NetworkOp]; [Field] belongs on a [NetworkNotify]. A mismatched attribute is ignored by the generator, so the field never reaches the wire.");

        static readonly DiagnosticDescriptor LargeStructByValue = new(
            "PFNET0009", "Large wire DTO passed by value",
            "Wire DTO '{0}' (~{1} bytes) is passed by value as read-only parameter '{2}'; pass it by 'in' to avoid copying the struct",
            Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
            description: "A large immutable wire struct read but not reassigned is cheaper to pass by 'in' (or read via the envelope's 'ref readonly View').");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            DuplicateIndex, ReservedIndexReuse, OpcodeCollision, ServerOperationNotSealed,
            ServerOperationPairMismatch, WireTypeMissingContract, OpTargetNotPartialClass,
            WireIndexWrongTarget, LargeStructByValue);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        // ---- well-known symbols for one compilation ----
        sealed class Known
        {
            public readonly INamedTypeSymbol? NetworkOp;
            public readonly INamedTypeSymbol? NetworkNotify;
            public readonly INamedTypeSymbol? Request;
            public readonly INamedTypeSymbol? Reply;
            public readonly INamedTypeSymbol? Field;
            public readonly INamedTypeSymbol? Reserved;
            public readonly INamedTypeSymbol? RequestMessage;
            public readonly INamedTypeSymbol? ReplyMessage;
            public readonly INamedTypeSymbol? NotifyMessage;
            public readonly INamedTypeSymbol? MessagePackObject;
            public readonly INamedTypeSymbol? ServerOperation;

            public Known(Compilation c)
            {
                NetworkOp = c.GetTypeByMetadataName(NetworkOpAttr);
                NetworkNotify = c.GetTypeByMetadataName(NetworkNotifyAttr);
                Request = c.GetTypeByMetadataName(RequestAttr);
                Reply = c.GetTypeByMetadataName(ReplyAttr);
                Field = c.GetTypeByMetadataName(FieldAttr);
                Reserved = c.GetTypeByMetadataName(ReservedAttr);
                RequestMessage = c.GetTypeByMetadataName(RequestMessageBase);
                ReplyMessage = c.GetTypeByMetadataName(ReplyMessageBase);
                NotifyMessage = c.GetTypeByMetadataName(NotifyMessageBase);
                MessagePackObject = c.GetTypeByMetadataName(MessagePackObjectAttr);
                ServerOperation = c.GetTypeByMetadataName(ServerOperationBase);
            }

            public bool AnyOpTypes => NetworkOp is not null || NetworkNotify is not null;
            public bool AnyWireBases => RequestMessage is not null || ReplyMessage is not null || NotifyMessage is not null;
        }

        // one entry per attributed operation, collected during symbol analysis
        readonly struct OpcodeEntry
        {
            public readonly long Domain;
            public readonly long Op;
            public readonly string Display;
            public readonly Location Location;
            public OpcodeEntry(long domain, long op, string display, Location location)
            { Domain = domain; Op = op; Display = display; Location = location; }
        }

        void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var known = new Known(context.Compilation);
            if (!known.AnyOpTypes && !known.AnyWireBases && known.ServerOperation is null)
                return; // this assembly does not use the network layer

            var opcodes = new ConcurrentBag<OpcodeEntry>();

            context.RegisterSymbolAction(sc => AnalyzeNamedType(sc, known, opcodes), SymbolKind.NamedType);
            context.RegisterOperationBlockAction(bc => AnalyzeBlockForRefNudge(bc, known));
            context.RegisterCompilationEndAction(cc => ReportOpcodeCollisions(cc, opcodes));
        }

        // ---- per-type rules ----
        static void AnalyzeNamedType(SymbolAnalysisContext ctx, Known known, ConcurrentBag<OpcodeEntry> opcodes)
        {
            var type = (INamedTypeSymbol)ctx.Symbol;

            bool isOp = TryGetAttribute(type, known.NetworkOp, out var opData);
            bool isNotify = TryGetAttribute(type, known.NetworkNotify, out var notifyData);

            if (isOp || isNotify)
                AnalyzeOperationDeclaration(ctx, known, type, isOp, isOp ? opData! : notifyData!, opcodes);

            AnalyzeServerOperationSubclass(ctx, known, type);
            AnalyzeHandWrittenWireType(ctx, known, type);
        }

        static void AnalyzeOperationDeclaration(
            SymbolAnalysisContext ctx, Known known, INamedTypeSymbol type, bool isOp,
            AttributeData attr, ConcurrentBag<OpcodeEntry> opcodes)
        {
            string attrLabel = isOp ? "NetworkOp" : "NetworkNotify";

            // Rule 6 — target must be a partial class.
            if (type.TypeKind != TypeKind.Class || type.IsStatic || !IsDeclaredPartial(type))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    OpTargetNotPartialClass, PrimaryLocation(type), attrLabel, type.Name));
            }

            // Rule 3 — remember this opcode for the compilation-wide collision pass.
            if (attr.ConstructorArguments.Length == 2 &&
                TryConstant(attr.ConstructorArguments[0], out long domain) &&
                TryConstant(attr.ConstructorArguments[1], out long op))
            {
                opcodes.Add(new OpcodeEntry(domain, op,
                    type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), PrimaryLocation(type)));
            }

            AnalyzeWireFields(ctx, known, type, isOp);
        }

        // Rules 1, 2, 8 — the wire-index fields of one operation.
        static void AnalyzeWireFields(SymbolAnalysisContext ctx, Known known, INamedTypeSymbol type, bool isOp)
        {
            var reserved = CollectReservedIndices(known, type);

            // Duplicate detection is per wire struct: an RPC's request keys and reply keys are independent
            // spaces; a notify has a single payload space.
            var requestSpace = new Dictionary<int, string>();
            var replySpace = new Dictionary<int, string>();

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic || field.IsConst || field.IsImplicitlyDeclared)
                    continue;

                foreach (var fa in field.GetAttributes())
                {
                    var cls = fa.AttributeClass;
                    bool isRequest = SymbolEquals(cls, known.Request);
                    bool isReply = SymbolEquals(cls, known.Reply);
                    bool isField = SymbolEquals(cls, known.Field);
                    if (!isRequest && !isReply && !isField)
                        continue;

                    string label = isRequest ? "Request" : isReply ? "Reply" : "Field";
                    var loc = AttributeLocation(fa, field);

                    // Rule 8 — attribute vs operation kind.
                    if (isOp && isField)
                        ctx.ReportDiagnostic(Diagnostic.Create(WireIndexWrongTarget, loc, "Field", "[NetworkOp] operation", "[Request(n)] / [Reply(n)]"));
                    else if (!isOp && (isRequest || isReply))
                        ctx.ReportDiagnostic(Diagnostic.Create(WireIndexWrongTarget, loc, label, "[NetworkNotify] operation", "[Field(n)]"));

                    if (!TryIndex(fa, out int index))
                        continue;

                    // Rule 2 — reuse of a reserved (retired) index.
                    if (reserved.Contains(index))
                        ctx.ReportDiagnostic(Diagnostic.Create(ReservedIndexReuse, loc, index, type.Name));

                    // Rule 1 — duplicate index within the field's own wire struct.
                    bool goesToReply = isOp && isReply;
                    var space = goesToReply ? replySpace : requestSpace;
                    if (space.ContainsKey(index))
                        ctx.ReportDiagnostic(Diagnostic.Create(DuplicateIndex, loc, index, label, type.Name));
                    else
                        space[index] = field.Name;
                }
            }
        }

        // Rules 4, 5 — ServerOperation<TRequest, TResponse, TResult> subclass shape.
        static void AnalyzeServerOperationSubclass(SymbolAnalysisContext ctx, Known known, INamedTypeSymbol type)
        {
            if (known.ServerOperation is null || type.TypeKind != TypeKind.Class || type.IsAbstract)
                return;

            var op = FindServerOperationBase(type, known.ServerOperation);
            if (op is null)
                return;

            // Rule 4 — a concrete operation should be sealed.
            if (!type.IsSealed)
                ctx.ReportDiagnostic(Diagnostic.Create(ServerOperationNotSealed, PrimaryLocation(type), type.Name));

            // Rule 5 — request/reply type args should be a matched generated pair (or a fully hand-written
            // pair, which is the supported manual fallback — never flagged). Only a cross-wired/half-generated
            // pair is flagged.
            if (op.TypeArguments.Length == 3)
            {
                var reqArg = op.TypeArguments[0] as INamedTypeSymbol;
                var repArg = op.TypeArguments[1] as INamedTypeSymbol;
                if (reqArg is not null && repArg is not null &&
                    IsWireMessage(reqArg, known.RequestMessage) && IsWireMessage(repArg, known.ReplyMessage))
                {
                    bool reqGenerated = IsGeneratedEnvelope(reqArg, "RequestMessage");
                    bool repGenerated = IsGeneratedEnvelope(repArg, "ReplyMessage");
                    bool matched =
                        (reqGenerated && repGenerated && SymbolEquals(reqArg.ContainingType, repArg.ContainingType)) ||
                        (!reqGenerated && !repGenerated);
                    if (!matched)
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            ServerOperationPairMismatch, PrimaryLocation(type), type.Name));
                }
            }
        }

        // Rule 5 (the other one) — a hand-written wire envelope carrying a contractless payload DTO.
        static void AnalyzeHandWrittenWireType(SymbolAnalysisContext ctx, Known known, INamedTypeSymbol type)
        {
            if (!known.AnyWireBases || known.MessagePackObject is null)
                return;
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                return;
            if (!DerivesFromAny(type, known.RequestMessage, known.ReplyMessage, known.NotifyMessage))
                return;

            foreach (var member in type.GetMembers())
            {
                INamedTypeSymbol? payload = member switch
                {
                    IFieldSymbol f when !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared => f.Type as INamedTypeSymbol,
                    IPropertySymbol p when !p.IsStatic && p.SetMethod is not null => p.Type as INamedTypeSymbol,
                    _ => null,
                };
                if (payload is null || !IsCarriedDtoType(payload))
                    continue;
                if (HasAttribute(payload, known.MessagePackObject))
                    continue;

                ctx.ReportDiagnostic(Diagnostic.Create(
                    WireTypeMissingContract, PrimaryLocation(member),
                    type.Name, member.Name, payload.Name));
            }
        }

        // ---- rule 7: ref nudge ----
        static void AnalyzeBlockForRefNudge(OperationBlockAnalysisContext ctx, Known known)
        {
            if (known.MessagePackObject is null)
                return;
            if (ctx.OwningSymbol is not IMethodSymbol method || method.Parameters.IsEmpty)
                return;

            int threshold = ReadRefNudgeThreshold(ctx);

            foreach (var p in method.Parameters)
            {
                if (p.RefKind != RefKind.None)
                    continue;
                if (p.Type is not INamedTypeSymbol pt || pt.TypeKind != TypeKind.Struct || !pt.IsReadOnly)
                    continue;
                if (!HasAttribute(pt, known.MessagePackObject))
                    continue;

                int size = EstimateStructSize(pt, 0);
                if (size <= threshold)
                    continue;
                if (IsWrittenOrPassedByRef(p, ctx.OperationBlocks))
                    continue;

                ctx.ReportDiagnostic(Diagnostic.Create(
                    LargeStructByValue, p.Locations.FirstOrDefault() ?? Location.None, pt.Name, size, p.Name));
            }
        }

        static int ReadRefNudgeThreshold(OperationBlockAnalysisContext ctx)
        {
            var options = ctx.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (options.TryGetValue(RefNudgeConfigKey, out var raw) &&
                int.TryParse(raw, out int parsed) && parsed >= 0)
                return parsed;
            return DefaultRefNudgeMinBytes;
        }

        static bool IsWrittenOrPassedByRef(IParameterSymbol parameter, ImmutableArray<IOperation> blocks)
        {
            foreach (var block in blocks)
            {
                foreach (var op in block.DescendantsAndSelf())
                {
                    switch (op)
                    {
                        case IAssignmentOperation assign
                            when RefersToParameter(assign.Target, parameter):
                            return true;
                        case IIncrementOrDecrementOperation inc
                            when RefersToParameter(inc.Target, parameter):
                            return true;
                        case IArgumentOperation arg
                            when (arg.Parameter?.RefKind is RefKind.Ref or RefKind.Out) &&
                                 RefersToParameter(arg.Value, parameter):
                            return true;
                    }
                }
            }
            return false;
        }

        static bool RefersToParameter(IOperation op, IParameterSymbol parameter)
            => op is IParameterReferenceOperation pr && SymbolEquals(pr.Parameter, parameter);

        // ---- rule 3: compilation-wide opcode collisions ----
        static void ReportOpcodeCollisions(CompilationAnalysisContext ctx, ConcurrentBag<OpcodeEntry> opcodes)
        {
            var byKey = new Dictionary<(long, long), List<OpcodeEntry>>();
            foreach (var e in opcodes)
            {
                var key = (e.Domain, e.Op);
                if (!byKey.TryGetValue(key, out var list))
                    byKey[key] = list = new List<OpcodeEntry>();
                list.Add(e);
            }

            foreach (var pair in byKey)
            {
                var list = pair.Value;
                if (list.Count < 2)
                    continue;

                // Report each colliding declaration, pointing at the others.
                for (int i = 0; i < list.Count; i++)
                {
                    var others = string.Join(", ", list.Where((_, j) => j != i).Select(e => "'" + e.Display + "'"));
                    var extra = list.Where((_, j) => j != i).Select(e => e.Location);
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        OpcodeCollision, list[i].Location, extra,
                        pair.Key.Item1, pair.Key.Item2, list[i].Display, others));
                }
            }
        }

        // ---- helpers ----
        static bool TryGetAttribute(INamedTypeSymbol type, INamedTypeSymbol? attrType, out AttributeData? data)
        {
            data = null;
            if (attrType is null)
                return false;
            foreach (var a in type.GetAttributes())
            {
                if (SymbolEquals(a.AttributeClass, attrType))
                {
                    data = a;
                    return true;
                }
            }
            return false;
        }

        static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attrType)
        {
            if (attrType is null)
                return false;
            foreach (var a in symbol.GetAttributes())
                if (SymbolEquals(a.AttributeClass, attrType))
                    return true;
            return false;
        }

        static ImmutableHashSet<int> CollectReservedIndices(Known known, INamedTypeSymbol type)
        {
            if (known.Reserved is null)
                return ImmutableHashSet<int>.Empty;
            var builder = ImmutableHashSet.CreateBuilder<int>();
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEquals(a.AttributeClass, known.Reserved) || a.ConstructorArguments.Length != 1)
                    continue;
                var arg = a.ConstructorArguments[0];
                if (arg.Kind != TypedConstantKind.Array)
                    continue;
                foreach (var element in arg.Values)
                    if (element.Value is int n)
                        builder.Add(n);
            }
            return builder.ToImmutable();
        }

        static bool TryIndex(AttributeData attr, out int index)
        {
            if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int i)
            {
                index = i;
                return true;
            }
            index = -1;
            return false;
        }

        static bool TryConstant(TypedConstant c, out long value)
        {
            value = 0;
            if (c.IsNull || c.Value is null)
                return false;
            try
            {
                value = Convert.ToInt64(c.Value);
                return true;
            }
            catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
            {
                return false;
            }
        }

        static INamedTypeSymbol? FindServerOperationBase(INamedTypeSymbol type, INamedTypeSymbol serverOperation)
        {
            for (var t = type.BaseType; t is not null; t = t.BaseType)
                if (SymbolEquals(t.OriginalDefinition, serverOperation))
                    return t;
            return null;
        }

        static bool IsWireMessage(INamedTypeSymbol type, INamedTypeSymbol? messageBase)
            => messageBase is not null && DerivesFrom(type, messageBase);

        static bool IsGeneratedEnvelope(INamedTypeSymbol type, string envelopeName)
            => type.Name == envelopeName && type.ContainingType is not null;

        static bool DerivesFromAny(INamedTypeSymbol type, params INamedTypeSymbol?[] bases)
        {
            foreach (var b in bases)
                if (b is not null && DerivesFrom(type, b))
                    return true;
            return false;
        }

        static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var t = type.BaseType; t is not null; t = t.BaseType)
                if (SymbolEquals(t, baseType))
                    return true;
            return false;
        }

        // A "carried DTO" is a user-defined struct/class that would ride the wire as a composite payload —
        // not a primitive, string, enum, or array. Those serialize fine without their own contract.
        static bool IsCarriedDtoType(INamedTypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
                return false; // primitives, string, object, etc.
            if (type.TypeKind is not (TypeKind.Struct or TypeKind.Class))
                return false;
            if (type.EnumUnderlyingType is not null)
                return false;
            // only DTOs declared in source — framework/library types are out of our hands
            return type.Locations.Any(l => l.IsInSource);
        }

        static int EstimateStructSize(INamedTypeSymbol type, int depth)
        {
            if (depth > 8)
                return 0;
            int size = 0;
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol f || f.IsStatic || f.IsConst)
                    continue;
                size += SizeOfType(f.Type, depth);
            }
            return size;
        }

        static int SizeOfType(ITypeSymbol type, int depth)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                    return 1;
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Char:
                    return 2;
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Single:
                    return 4;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Double:
                    return 8;
                case SpecialType.System_Decimal:
                    return 16;
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                    return 8;
            }

            if (type is INamedTypeSymbol named)
            {
                if (named.EnumUnderlyingType is not null)
                    return SizeOfType(named.EnumUnderlyingType, depth);
                if (named.TypeKind == TypeKind.Struct)
                    return EstimateStructSize(named, depth + 1);
            }
            return 8; // reference-typed field: a pointer
        }

        static bool IsDeclaredPartial(INamedTypeSymbol type)
        {
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is TypeDeclarationSyntax decl &&
                    decl.Modifiers.Any(SyntaxKind.PartialKeyword))
                    return true;
            }
            return false;
        }

        static Location PrimaryLocation(ISymbol symbol)
            => symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? symbol.Locations.FirstOrDefault() ?? Location.None;

        static Location AttributeLocation(AttributeData attr, ISymbol fallback)
        {
            var reference = attr.ApplicationSyntaxReference;
            return reference is not null
                ? Location.Create(reference.SyntaxTree, reference.Span)
                : PrimaryLocation(fallback);
        }

        static bool SymbolEquals(ISymbol? a, ISymbol? b)
            => SymbolEqualityComparer.Default.Equals(a, b);
    }
}
