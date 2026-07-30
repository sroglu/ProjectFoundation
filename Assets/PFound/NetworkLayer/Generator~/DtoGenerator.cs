#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PFound.NetworkLayer.Generation
{
    /// <summary>
    /// Adds the value-type ergonomics MessagePack does not, so a DTO author writes ONLY the keyed members:
    /// for every <c>[MessagePackObject]</c> type that declares <c>[Key(n)]</c> members, this emits value
    /// equality (<c>IEquatable&lt;T&gt;</c> + <c>Equals</c>/<c>GetHashCode</c>) and <c>ToString</c>, ordered by
    /// key index. Serialization is NOT emitted here — MessagePack's own source generator produces the wire
    /// formatter from the same user-written <c>[MessagePackObject]</c>/<c>[Key]</c> members (immutable DTOs are
    /// authored as a <c>readonly struct</c> with <c>init</c>-only keyed properties, which MessagePack
    /// member-sets through the <c>init</c> path — no first-party constructor is involved).
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class DtoGenerator : IIncrementalGenerator
    {
        const string MessagePackObjectAttribute = "MessagePack.MessagePackObjectAttribute";
        const string KeyAttribute = "MessagePack.KeyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var dtos = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    MessagePackObjectAttribute,
                    predicate: static (node, _) => true,
                    transform: static (ctx, _) => Describe(ctx))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            context.RegisterSourceOutput(dtos, static (spc, m) => spc.AddSource(m.HintName, Emit(m)));
        }

        // ---- model ---------------------------------------------------------

        sealed class DtoModel
        {
            public string? Namespace;
            public ImmutableArray<string> ContainingTypes; // outer→inner, empty when top level
            public string TypeName = "";
            public string Kind = "struct"; // struct | class
            public ImmutableArray<MemberModel> Members;
            public string HintName = "";
        }

        readonly struct MemberModel
        {
            public readonly int Index;
            public readonly string Name;
            public readonly string TypeFqn;
            public MemberModel(int index, string name, string typeFqn) { Index = index; Name = name; TypeFqn = typeFqn; }
        }

        static DtoModel? Describe(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol type)
                return null;

            // Opt-in + non-conflicting: only fill in the ergonomics for a `partial` type that has NOT already
            // hand-authored value equality (i.e. does not declare IEquatable<self>). This leaves every
            // hand-written [MessagePackObject] DTO elsewhere untouched — the generator never fights user code.
            if (ctx.TargetNode is not TypeDeclarationSyntax decl ||
                !decl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                return null;
            if (type.Interfaces.Any(i => i.Name == "IEquatable" && i.TypeArguments.Length == 1 &&
                                         SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type)))
                return null;

            var members = new List<MemberModel>();
            foreach (var member in type.GetMembers())
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

            // Nothing to add when there are no int-keyed members (e.g. a string-keyed or empty contract).
            if (members.Count == 0)
                return null;

            var containing = ContainingTypeChain(type);
            var ns = type.ContainingNamespace is { IsGlobalNamespace: false } n ? n.ToDisplayString() : null;
            var pathPrefix = string.Concat(containing.Select(c => c + "_"));

            var model = new DtoModel
            {
                Namespace = ns,
                ContainingTypes = containing,
                TypeName = type.Name,
                Kind = type.TypeKind == TypeKind.Struct ? "struct" : "class",
                Members = members.OrderBy(f => f.Index).ToImmutableArray(),
            };
            model.HintName = (ns is null ? "" : ns + ".") + pathPrefix + type.Name + ".Dto.g.cs";
            return model;
        }

        static ImmutableArray<string> ContainingTypeChain(INamedTypeSymbol type)
        {
            var stack = new Stack<string>();
            for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
                stack.Push(outer.Name);
            return stack.ToImmutableArray();
        }

        // ---- emission ------------------------------------------------------

        static SourceText Emit(DtoModel m)
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

            var name = m.TypeName;
            var members = m.Members;
            var pad = new string(' ', indent * 4);
            var pad1 = new string(' ', (indent + 1) * 4);
            var pad2 = new string(' ', (indent + 2) * 4);

            sb.Append(pad).AppendLine($"partial {m.Kind} {name} : global::System.IEquatable<{name}>");
            sb.Append(pad).AppendLine("{");

            sb.Append(pad1).AppendLine($"public bool Equals({name} other) =>");
            for (int i = 0; i < members.Length; i++)
            {
                var f = members[i];
                var prefix = i == 0 ? "" : "&& ";
                var suffix = i == members.Length - 1 ? ";" : "";
                sb.Append(pad2).AppendLine(
                    $"{prefix}global::System.Collections.Generic.EqualityComparer<{f.TypeFqn}>.Default.Equals(this.{f.Name}, other.{f.Name}){suffix}");
            }
            sb.Append(pad1).AppendLine($"public override bool Equals(object obj) => obj is {name} other && this.Equals(other);");

            sb.Append(pad1).AppendLine("public override int GetHashCode()");
            sb.Append(pad1).AppendLine("{");
            sb.Append(pad2).AppendLine("unchecked");
            sb.Append(pad2).AppendLine("{");
            sb.Append(pad2).AppendLine("    int hash = 17;");
            foreach (var f in members)
                sb.Append(pad2).AppendLine(
                    $"    hash = hash * 31 + global::System.Collections.Generic.EqualityComparer<{f.TypeFqn}>.Default.GetHashCode(this.{f.Name});");
            sb.Append(pad2).AppendLine("    return hash;");
            sb.Append(pad2).AppendLine("}");
            sb.Append(pad1).AppendLine("}");

            var toStr = "$\"" + name + "(" + string.Join(", ", members.Select(f => f.Name + "={this." + f.Name + "}")) + ")\"";
            sb.Append(pad1).AppendLine($"public override string ToString() => {toStr};");

            sb.Append(pad).AppendLine("}");

            foreach (var _ in m.ContainingTypes)
            {
                indent--;
                sb.Append(' ', indent * 4).AppendLine("}");
            }
            if (m.Namespace is not null)
                sb.AppendLine("}");

            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }
    }
}
