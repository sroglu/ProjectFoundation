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
    /// Turns each <c>[NetworkOp]</c> / <c>[NetworkNotify]</c> partial class into its wire types: the
    /// immutable MessagePack payload struct(s), the poolable envelope(s), and a per-operation
    /// <c>Register</c>; plus one assembly-wide aggregator that enrols every operation. The declaration the
    /// developer writes is read as a spec — this generator DECLARES the keyed types itself, because a
    /// <c>[Key(n)]</c> must sit on the emitted member.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class NetworkMessageGenerator : IIncrementalGenerator
    {
        const string OpAttribute = "PFound.NetworkLayer.NetworkOpAttribute";
        const string NotifyAttribute = "PFound.NetworkLayer.NetworkNotifyAttribute";
        const string RequestAttribute = "PFound.NetworkLayer.RequestAttribute";
        const string ReplyAttribute = "PFound.NetworkLayer.ReplyAttribute";
        const string FieldAttribute = "PFound.NetworkLayer.FieldAttribute";

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

            // Emit one file per operation.
            context.RegisterSourceOutput(ops, static (spc, m) => spc.AddSource(m.HintName, Emit(m)));
            context.RegisterSourceOutput(notifies, static (spc, m) => spc.AddSource(m.HintName, Emit(m)));

            // Emit one aggregator over every operation in the compilation — but only when this
            // assembly actually declares operations. An empty aggregator would still reference
            // MessageCatalog, which would not resolve in an assembly that does not use the layer.
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
            public string FullyQualifiedName = ""; // global::...Spend
            public string DomainExpr = "";
            public string OpExpr = "";
            public ImmutableArray<FieldModel> RequestFields;
            public ImmutableArray<FieldModel> ReplyFields;
            public string HintName = "";
        }

        readonly struct FieldModel
        {
            public readonly int Index;
            public readonly string Name;
            public readonly string TypeFqn;
            public FieldModel(int index, string name, string typeFqn) { Index = index; Name = name; TypeFqn = typeFqn; }
        }

        static OpModel? Describe(GeneratorAttributeSyntaxContext ctx, bool isNotify)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol type)
                return null;

            var attr = ctx.Attributes[0];
            if (attr.ConstructorArguments.Length != 2)
                return null;

            var model = new OpModel
            {
                IsNotify = isNotify,
                Namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns
                    ? ns.ToDisplayString()
                    : null,
                ContainingTypes = ContainingTypeChain(type),
                TypeName = type.Name,
                FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DomainExpr = EnumMemberExpression(attr.ConstructorArguments[0]),
                OpExpr = EnumMemberExpression(attr.ConstructorArguments[1]),
            };

            var request = new List<FieldModel>();
            var reply = new List<FieldModel>();
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic || field.IsConst)
                    continue;

                foreach (var fa in field.GetAttributes())
                {
                    var name = fa.AttributeClass?.ToDisplayString();
                    var index = ReadIndex(fa);
                    if (index < 0)
                        continue;

                    var fm = new FieldModel(index, field.Name,
                        field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                    if (!isNotify && name == RequestAttribute) request.Add(fm);
                    else if (!isNotify && name == ReplyAttribute) reply.Add(fm);
                    else if (isNotify && name == FieldAttribute) request.Add(fm); // notify payload rides "request" slot
                }
            }

            model.RequestFields = request.OrderBy(f => f.Index).ToImmutableArray();
            model.ReplyFields = reply.OrderBy(f => f.Index).ToImmutableArray();
            model.HintName = (model.Namespace is null ? "" : model.Namespace + ".") + model.TypeName + ".g.cs";
            return model;
        }

        static ImmutableArray<string> ContainingTypeChain(INamedTypeSymbol type)
        {
            var stack = new Stack<string>();
            for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
                stack.Push(outer.Name);
            return stack.ToImmutableArray();
        }

        static int ReadIndex(AttributeData fa)
        {
            if (fa.ConstructorArguments.Length == 1 && fa.ConstructorArguments[0].Value is int i)
                return i;
            return -1;
        }

        // Recover "global::Ns.EnumType.Member" from an enum-valued attribute constant.
        static string EnumMemberExpression(TypedConstant constant)
        {
            if (constant.Type is not INamedTypeSymbol enumType || enumType.TypeKind != TypeKind.Enum)
                return "default";

            var enumFqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol f && f.HasConstantValue &&
                    Equals(f.ConstantValue, constant.Value))
                {
                    return enumFqn + "." + f.Name;
                }
            }

            // No named member matched the value — fall back to a checked cast.
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
                EmitStruct(sb, indent, "Data", m.RequestFields);
                EmitEnvelope(sb, indent, "NotifyMessage", "global::PFound.NetworkLayer.NotifyMessage", "Data", callBase: false);
                EmitNotifyRegister(sb, indent, m);
            }
            else
            {
                EmitStruct(sb, indent, "Req", m.RequestFields);
                EmitStruct(sb, indent, "Reply", m.ReplyFields);
                EmitEnvelope(sb, indent, "RequestMessage", "global::PFound.NetworkLayer.RequestMessage", "Req", callBase: false);
                EmitEnvelope(sb, indent, "ReplyMessage", "global::PFound.NetworkLayer.ReplyMessage", "Reply", callBase: true);
                EmitOpRegister(sb, indent, m);
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

        static void EmitStruct(StringBuilder sb, int indent, string name, ImmutableArray<FieldModel> fields)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            var pad2 = new string(' ', (indent + 2) * 4);

            sb.AppendLine();
            sb.Append(pad).AppendLine("[global::MessagePack.MessagePackObject]");
            sb.Append(pad).AppendLine($"public readonly struct {name} : global::System.IEquatable<{name}>");
            sb.Append(pad).AppendLine("{");

            foreach (var f in fields)
                sb.Append(pad1).AppendLine($"[global::MessagePack.Key({f.Index})] public readonly {f.TypeFqn} {f.Name};");

            if (fields.Length > 0)
            {
                sb.AppendLine();
                sb.Append(pad1).AppendLine("[global::MessagePack.SerializationConstructor]");
                var ctorParams = string.Join(", ", fields.Select(f => $"{f.TypeFqn} {ParamName(f.Name)}"));
                sb.Append(pad1).AppendLine($"public {name}({ctorParams})");
                sb.Append(pad1).AppendLine("{");
                foreach (var f in fields)
                    sb.Append(pad2).AppendLine($"this.{f.Name} = {ParamName(f.Name)};");
                sb.Append(pad1).AppendLine("}");
            }

            // value equality
            sb.AppendLine();
            if (fields.Length == 0)
            {
                sb.Append(pad1).AppendLine($"public bool Equals({name} other) => true;");
            }
            else
            {
                sb.Append(pad1).AppendLine($"public bool Equals({name} other) =>");
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    var prefix = i == 0 ? "" : "&& ";
                    var suffix = i == fields.Length - 1 ? ";" : "";
                    sb.Append(pad2).AppendLine(
                        $"{prefix}global::System.Collections.Generic.EqualityComparer<{f.TypeFqn}>.Default.Equals(this.{f.Name}, other.{f.Name}){suffix}");
                }
            }
            sb.Append(pad1).AppendLine($"public override bool Equals(object obj) => obj is {name} other && this.Equals(other);");

            sb.Append(pad1).AppendLine("public override int GetHashCode()");
            sb.Append(pad1).AppendLine("{");
            if (fields.Length == 0)
            {
                sb.Append(pad2).AppendLine("return 0;");
            }
            else
            {
                sb.Append(pad2).AppendLine("unchecked");
                sb.Append(pad2).AppendLine("{");
                sb.Append(pad2).AppendLine("    int hash = 17;");
                foreach (var f in fields)
                    sb.Append(pad2).AppendLine(
                        $"    hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{f.TypeFqn}>.Default.GetHashCode(this.{f.Name});");
                sb.Append(pad2).AppendLine("    return hash;");
                sb.Append(pad2).AppendLine("}");
            }
            sb.Append(pad1).AppendLine("}");

            var toStr = fields.Length == 0
                ? $"\"{name}()\""
                : "$\"" + name + "(" + string.Join(", ", fields.Select(f => f.Name + "={this." + f.Name + "}")) + ")\"";
            sb.Append(pad1).AppendLine($"public override string ToString() => {toStr};");

            sb.Append(pad).AppendLine("}");
        }

        static void EmitEnvelope(StringBuilder sb, int indent, string name, string baseType, string payloadType, bool callBase)
        {
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            var pad2 = new string(' ', (indent + 2) * 4);

            sb.AppendLine();
            sb.Append(pad).AppendLine($"public sealed class {name} : {baseType}");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad1).AppendLine($"public {payloadType} Content;");
            sb.Append(pad1).AppendLine("/// <summary>Zero-copy read of the immutable payload — avoids copying the struct when reading its fields.</summary>");
            sb.Append(pad1).AppendLine($"public ref readonly {payloadType} View => ref this.Content;");
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
            sb.Append(pad1).AppendLine(
                $"catalog.ForDomain({m.DomainExpr}).Enroll<RequestMessage, ReplyMessage>({m.OpExpr});");
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

        static string ParamName(string fieldName)
        {
            var camel = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
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
