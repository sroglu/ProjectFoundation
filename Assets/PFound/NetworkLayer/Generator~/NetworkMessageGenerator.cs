#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// Turns each <c>[RemoteProcedure]</c> / <c>[Notify]</c> partial class into its runtime wiring: the
    /// poolable envelope(s) that CARRY a named DTO (a <see cref="PFound.NetworkLayer.IMessagePayload"/> so the
    /// codec serializes the DTO and never the envelope), a per-operation <c>Register</c>, and a uniform call
    /// entry point (<c>CallAsync</c> returning the reply DTO for a remote procedure, <c>Send</c> for a notify)
    /// plus a convenience overload built from the request DTO's keyed members; and one assembly-wide aggregator
    /// that enrols every operation. The DTO types come from the attribute's <c>typeof</c> arguments — this
    /// generator no longer synthesizes request/reply structs; every wire type is a first-party
    /// <c>[MessagePackObject]</c> DTO whose formatter MessagePack's own generator produces.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class NetworkMessageGenerator : IIncrementalGenerator
    {
        const string OpAttribute = "PFound.NetworkLayer.RemoteProcedureAttribute";
        const string NotifyAttribute = "PFound.NetworkLayer.NotifyAttribute";
        const string KeyAttribute = "MessagePack.KeyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var ops = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    OpAttribute,
                    predicate: static (node, _) => true,
                    transform: static (ctx, _) => Describe(ctx, isNotify: false))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            var notifies = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    NotifyAttribute,
                    predicate: static (node, _) => true,
                    transform: static (ctx, _) => Describe(ctx, isNotify: true))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            context.RegisterSourceOutput(ops, static (spc, m) => spc.AddSource(m.HintName, Emit(m)));
            context.RegisterSourceOutput(notifies, static (spc, m) => spc.AddSource(m.HintName, Emit(m)));

            var all = ops.Collect().Combine(notifies.Collect());
            context.RegisterSourceOutput(all, static (spc, pair) =>
            {
                if (pair.Left.IsEmpty && pair.Right.IsEmpty)
                    return;
                spc.AddSource("GeneratedMessages.g.cs", EmitAggregator(pair.Left, pair.Right));
            });
        }

        // ---- model ---------------------------------------------------------

        sealed class OpModel
        {
            public bool IsNotify;
            public string? Namespace;
            public ImmutableArray<string> ContainingTypes; // outer→inner, empty when top level
            public string TypeName = "";
            public string FullyQualifiedName = "";
            public string DomainExpr = "";
            public string OpExpr = "";
            public string RequestTypeFqn = "";              // TRequest / TPayload (notify)
            public string ReplyTypeFqn = "";                // TReply (RPC only)
            public ImmutableArray<MemberModel> RequestMembers; // TRequest/TPayload keyed members (for overload)
            public string HintName = "";
        }

        readonly struct MemberModel
        {
            public readonly int Index;
            public readonly string Name;
            public readonly string TypeFqn;
            public MemberModel(int index, string name, string typeFqn) { Index = index; Name = name; TypeFqn = typeFqn; }
        }

        static OpModel? Describe(GeneratorAttributeSyntaxContext ctx, bool isNotify)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol type)
                return null;

            var attr = ctx.Attributes[0];
            var args = attr.ConstructorArguments;
            // RPC: (domain, op, typeof(TRequest), typeof(TReply)); Notify: (domain, op, typeof(TPayload)).
            if (isNotify ? args.Length != 3 : args.Length != 4)
                return null;

            var requestType = args[2].Value as INamedTypeSymbol;
            if (requestType is null)
                return null;
            var replyType = isNotify ? null : args[3].Value as INamedTypeSymbol;
            if (!isNotify && replyType is null)
                return null;

            var model = new OpModel
            {
                IsNotify = isNotify,
                Namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : null,
                ContainingTypes = ContainingTypeChain(type),
                TypeName = type.Name,
                FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DomainExpr = EnumMemberExpression(args[0]),
                OpExpr = EnumMemberExpression(args[1]),
                RequestTypeFqn = requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ReplyTypeFqn = replyType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "",
                RequestMembers = KeyedMembers(requestType),
            };
            model.HintName = (model.Namespace is null ? "" : model.Namespace + ".") + model.TypeName + ".g.cs";
            return model;
        }

        static ImmutableArray<MemberModel> KeyedMembers(INamedTypeSymbol dto)
        {
            var members = new List<MemberModel>();
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
                        members.Add(new MemberModel(index, m.Value.Name,
                            m.Value.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }
            return members.OrderBy(f => f.Index).ToImmutableArray();
        }

        static ImmutableArray<string> ContainingTypeChain(INamedTypeSymbol type)
        {
            var stack = new Stack<string>();
            for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
                stack.Push(outer.Name);
            return stack.ToImmutableArray();
        }

        static string EnumMemberExpression(TypedConstant constant)
        {
            if (constant.Type is not INamedTypeSymbol enumType || enumType.TypeKind != TypeKind.Enum)
                return "default";

            var enumFqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol f && f.HasConstantValue && Equals(f.ConstantValue, constant.Value))
                    return enumFqn + "." + f.Name;
            }
            return "(" + enumFqn + ")(" + (constant.Value?.ToString() ?? "0") + ")";
        }

        // ---- emission ------------------------------------------------------

        static SourceText Emit(OpModel m)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable disable");
            sb.AppendLine();

            var indent = 0;
            if (m.Namespace is not null)
            {
                sb.Append(' ', indent * 4).AppendLine($"namespace {m.Namespace}");
                sb.Append(' ', indent * 4).AppendLine("{");
                indent++;
            }
            foreach (var outer in m.ContainingTypes)
            {
                sb.Append(' ', indent * 4).AppendLine($"partial class {outer}");
                sb.Append(' ', indent * 4).AppendLine("{");
                indent++;
            }

            sb.Append(' ', indent * 4).AppendLine($"partial class {m.TypeName}");
            sb.Append(' ', indent * 4).AppendLine("{");
            indent++;

            if (m.IsNotify)
            {
                EmitEnvelope(sb, indent, "NotifyMessage", "global::PFound.NetworkLayer.NotifyMessage", m.RequestTypeFqn, callBase: false);
                EmitNotifyRegister(sb, indent, m);
                EmitSend(sb, indent, m);
            }
            else
            {
                EmitEnvelope(sb, indent, "RequestMessage", "global::PFound.NetworkLayer.RequestMessage", m.RequestTypeFqn, callBase: false);
                EmitEnvelope(sb, indent, "ReplyMessage", "global::PFound.NetworkLayer.ReplyMessage", m.ReplyTypeFqn, callBase: true);
                EmitOpRegister(sb, indent, m);
                EmitCall(sb, indent, m);
            }

            indent--;
            sb.Append(' ', indent * 4).AppendLine("}");
            foreach (var _ in m.ContainingTypes)
            {
                indent--;
                sb.Append(' ', indent * 4).AppendLine("}");
            }
            if (m.Namespace is not null)
                sb.AppendLine("}");

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        // A poolable envelope that CARRIES the payload DTO. It is a runtime-only routing/pooling carrier and is
        // never serialized — the codec reads the DTO out via IMessagePayload and writes the decoded DTO back in.
        static void EmitEnvelope(StringBuilder sb, int indent, string name, string baseType, string payloadTypeFqn, bool callBase)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            var pad2 = new string(' ', (indent + 2) * 4);

            sb.AppendLine();
            sb.Append(pad).AppendLine($"public sealed class {name} : {baseType}, global::PFound.NetworkLayer.IMessagePayload");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine($"public {payloadTypeFqn} Content;");
            sb.Append(pad1).AppendLine("/// <summary>Zero-copy read of the immutable payload — avoids copying the struct when reading its members.</summary>");
            sb.Append(pad1).AppendLine($"public ref readonly {payloadTypeFqn} View => ref this.Content;");
            sb.Append(pad1).AppendLine($"global::System.Type global::PFound.NetworkLayer.IMessagePayload.PayloadType => typeof({payloadTypeFqn});");
            sb.Append(pad1).AppendLine("object global::PFound.NetworkLayer.IMessagePayload.Payload");
            sb.Append(pad1).AppendLine("{");
            sb.Append(pad2).AppendLine("get => this.Content;");
            sb.Append(pad2).AppendLine($"set => this.Content = ({payloadTypeFqn})value;");
            sb.Append(pad1).AppendLine("}");
            sb.Append(pad1).AppendLine("public override void Clear()");
            sb.Append(pad1).AppendLine("{");
            if (callBase)
                sb.Append(pad2).AppendLine("base.Clear();");
            sb.Append(pad2).AppendLine("this.Content = default;");
            sb.Append(pad1).AppendLine("}");
            sb.Append(pad).AppendLine("}");
        }

        static void EmitOpRegister(StringBuilder sb, int indent, OpModel m)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            sb.AppendLine();
            sb.Append(pad).AppendLine("public static void Register(global::PFound.NetworkLayer.MessageCatalog catalog)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine($"catalog.ForDomain({m.DomainExpr}).Enroll<RequestMessage, ReplyMessage>({m.OpExpr});");
            sb.Append(pad).AppendLine("}");
        }

        static void EmitNotifyRegister(StringBuilder sb, int indent, OpModel m)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            sb.AppendLine();
            sb.Append(pad).AppendLine("public static void Register(global::PFound.NetworkLayer.MessageCatalog catalog)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine($"catalog.ForDomain({m.DomainExpr}).Enroll<NotifyMessage>({m.OpExpr});");
            sb.Append(pad).AppendLine("}");
        }

        // The uniform call: build the request envelope around the request DTO, send it through the ambient
        // client, await the correlated reply envelope, and return its reply DTO. A convenience overload accepts
        // the request DTO's keyed members directly and builds the DTO for the caller.
        static void EmitCall(StringBuilder sb, int indent, OpModel m)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);

            sb.AppendLine();
            sb.Append(pad).AppendLine("/// <summary>");
            sb.Append(pad).AppendLine("/// Call this operation and await its reply DTO. Builds the request envelope, sends it through the");
            sb.Append(pad).AppendLine("/// ambient <see cref=\"global::PFound.NetworkLayer.NetworkClient\"/> (configure <c>Current</c> once at");
            sb.Append(pad).AppendLine("/// startup), and returns the reply DTO.");
            sb.Append(pad).AppendLine("/// </summary>");
            sb.Append(pad).AppendLine($"public static async global::System.Threading.Tasks.Task<{m.ReplyTypeFqn}> CallAsync({m.RequestTypeFqn} request)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine("var envelope = new RequestMessage { Content = request };");
            sb.Append(pad1).AppendLine("var reply = await global::PFound.NetworkLayer.NetworkClient.Current.CallAsync<ReplyMessage>(envelope);");
            sb.Append(pad1).AppendLine("return reply.Content;");
            sb.Append(pad).AppendLine("}");

            if (m.RequestMembers.Length > 0)
            {
                var paramList = string.Join(", ", m.RequestMembers.Select(f => $"{f.TypeFqn} {ParamName(f.Name)}"));
                var init = string.Join(", ", m.RequestMembers.Select(f => $"{f.Name} = {ParamName(f.Name)}"));
                sb.AppendLine();
                sb.Append(pad).AppendLine("/// <summary>Convenience overload: pass the request DTO's members directly.</summary>");
                sb.Append(pad).AppendLine($"public static global::System.Threading.Tasks.Task<{m.ReplyTypeFqn}> CallAsync({paramList})");
                sb.Append(pad1).AppendLine($"=> CallAsync(new {m.RequestTypeFqn} {{ {init} }});");
            }
        }

        // The uniform one-way send: build the notify envelope around the payload DTO and fire it fire-and-forget
        // through the ambient client. A convenience overload accepts the payload DTO's keyed members directly.
        static void EmitSend(StringBuilder sb, int indent, OpModel m)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);

            sb.AppendLine();
            sb.Append(pad).AppendLine("/// <summary>");
            sb.Append(pad).AppendLine("/// Fire this notify one-way (no reply) through the ambient");
            sb.Append(pad).AppendLine("/// <see cref=\"global::PFound.NetworkLayer.NetworkClient\"/> (configure <c>Current</c> once at startup).");
            sb.Append(pad).AppendLine("/// </summary>");
            sb.Append(pad).AppendLine($"public static void Send({m.RequestTypeFqn} payload)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine("var envelope = new NotifyMessage { Content = payload };");
            sb.Append(pad1).AppendLine("global::PFound.NetworkLayer.NetworkClient.Current.Post(envelope);");
            sb.Append(pad).AppendLine("}");

            if (m.RequestMembers.Length > 0)
            {
                var paramList = string.Join(", ", m.RequestMembers.Select(f => $"{f.TypeFqn} {ParamName(f.Name)}"));
                var init = string.Join(", ", m.RequestMembers.Select(f => $"{f.Name} = {ParamName(f.Name)}"));
                sb.AppendLine();
                sb.Append(pad).AppendLine("/// <summary>Convenience overload: pass the payload DTO's members directly.</summary>");
                sb.Append(pad).AppendLine($"public static void Send({paramList})");
                sb.Append(pad1).AppendLine($"=> Send(new {m.RequestTypeFqn} {{ {init} }});");
            }
        }

        static SourceText EmitAggregator(ImmutableArray<OpModel> ops, ImmutableArray<OpModel> notifies)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable disable");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// One call that enrols every generated network operation in a <see cref=\"global::PFound.NetworkLayer.MessageCatalog\"/>.");
            sb.AppendLine("/// A game boots its catalog with <c>GeneratedMessages.RegisterAll(catalog)</c> so no operation can be forgotten.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class GeneratedMessages");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll(global::PFound.NetworkLayer.MessageCatalog catalog)");
            sb.AppendLine("    {");
            foreach (var m in ops.Concat(notifies).OrderBy(m => m.FullyQualifiedName))
                sb.AppendLine($"        {m.FullyQualifiedName}.Register(catalog);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        static string ParamName(string memberName)
        {
            var camel = char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);
            return IsKeyword(camel) ? "@" + camel : camel;
        }

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
