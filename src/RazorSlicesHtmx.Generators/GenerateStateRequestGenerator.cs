using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlicesHtmx.Generators;

[Generator]
public sealed class GenerateStateRequestGenerator : IIncrementalGenerator
{
    private const string StateNotMapAttributeMetadataName = "RazorSlicesHtmx.AspNetCore.Models.StateNotMapAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all WithState(expr) call sites and collect the inferred argument type.
        var targets = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsWithStateInvocation(node),
            static (syntaxContext, _) =>
            {
                if (syntaxContext.Node is not InvocationExpressionSyntax invocation)
                {
                    return null;
                }

                if (invocation.ArgumentList.Arguments.Count == 0)
                {
                    return null;
                }

                var argExpr = invocation.ArgumentList.Arguments[0].Expression;
                if (syntaxContext.SemanticModel.GetTypeInfo(argExpr).Type is not INamedTypeSymbol argType
                    || argType.IsAbstract
                    || argType.TypeKind == TypeKind.Interface)
                {
                    return null;
                }

                // Must be declared in this compilation (not an external library type).
                if (argType.DeclaringSyntaxReferences.IsEmpty)
                {
                    return null;
                }

                // At least one declaration must carry the partial modifier.
                var isPartial = false;
                foreach (var sr in argType.DeclaringSyntaxReferences)
                {
                    var syntax = sr.GetSyntax();
                    if (syntax is TypeDeclarationSyntax tds
                        && tds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        isPartial = true;
                        break;
                    }
                }

                if (!isPartial)
                {
                    return null;
                }

                var properties = CollectStateProperties(argType);
                if (properties.Count == 0)
                {
                    return null;
                }

                return new StateTarget(argType, properties);
            })
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(targets.Collect(), static (spc, models) =>
        {
            if (models.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var model in models.Distinct(StateTargetComparer.Instance))
            {
                EmitStateImpl(spc, model);
            }
        });
    }

    private static bool IsWithStateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax inv)
        {
            return false;
        }

        return inv.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text == "WithState",
            IdentifierNameSyntax ident => ident.Identifier.Text == "WithState",
            _ => false
        };
    }

    private static IReadOnlyList<IPropertySymbol> CollectStateProperties(INamedTypeSymbol symbol)
    {
        var byName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);

        INamedTypeSymbol? current = symbol;
        while (current is not null)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer)
                {
                    continue;
                }

                if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (HasStateNotMapAttribute(property))
                {
                    continue;
                }

                if (!byName.ContainsKey(property.Name))
                {
                    byName[property.Name] = property;
                }
            }

            current = current.BaseType;
        }

        return byName.Values
            .OrderBy(static p => p.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasStateNotMapAttribute(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (string.Equals(fullName, StateNotMapAttributeMetadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void EmitStateImpl(SourceProductionContext context, StateTarget model)
    {
        var ns = model.Symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.Symbol.ContainingNamespace.ToDisplayString();

        var typeName = model.Symbol.Name;
        var stateId = ToKebabCase(typeName);
        var fieldsConst = Escape(stateId);

        var isRecord = model.Symbol.IsRecord;
        var keyword = isRecord ? "record" : "class";
        var isSealed = model.Symbol.IsSealed;
        var sealedKeyword = isSealed ? "sealed partial " : "partial ";

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();

        if (!string.IsNullOrWhiteSpace(ns))
        {
            source.Append("namespace ").Append(ns).AppendLine(";");
            source.AppendLine();
        }

        source.Append("public ").Append(sealedKeyword).Append(keyword).Append(' ').Append(typeName)
            .AppendLine(" : global::RazorSlicesHtmx.AspNetCore.Models.IHasState")
            .AppendLine("{");

        source.Append("    public const string StateId = \"").Append(fieldsConst).AppendLine("\";");
        source.AppendLine();

        AppendSerializeMethod(source, "Serialize", fieldsConst, model.Properties, oob: false);
        source.AppendLine();
        AppendSerializeMethod(source, "SerializeOob", fieldsConst, model.Properties, oob: true);

        source.AppendLine("}");

        var nsSafe = string.IsNullOrWhiteSpace(ns) ? "Global" : ns!.Replace('.', '_');
        var hintName = nsSafe + "_" + typeName + ".State.g.cs";
        context.AddSource(hintName, SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void AppendSerializeMethod(
        StringBuilder source,
        string methodName,
        string stateId,
        IReadOnlyList<IPropertySymbol> properties,
        bool oob)
    {
        var serializerMethod = oob ? "SerializeOob" : "Serialize";
        source.Append("    public global::Microsoft.AspNetCore.Html.IHtmlContent ").Append(methodName).AppendLine("()");
        source.AppendLine("    {");
        source.AppendLine("        return global::RazorSlicesHtmx.AspNetCore.Models.StateSerializer." + serializerMethod + "(");
        source.Append("            \"").Append(stateId).AppendLine("\",");
        source.AppendLine("            new global::RazorSlicesHtmx.AspNetCore.Models.StateFieldValue[]");
        source.AppendLine("            {");
        foreach (var prop in properties)
        {
            source.Append("                global::RazorSlicesHtmx.AspNetCore.Models.StateFieldValue.FromObject(\"")
                .Append(Escape(prop.Name))
                .Append("\", ")
                .Append(prop.Name)
                .AppendLine("),");
        }
        source.AppendLine("            });");
        source.AppendLine("    }");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private sealed class StateTarget
    {
        public StateTarget(INamedTypeSymbol symbol, IReadOnlyList<IPropertySymbol> properties)
        {
            Symbol = symbol;
            Properties = properties;
        }

        public INamedTypeSymbol Symbol { get; }

        public IReadOnlyList<IPropertySymbol> Properties { get; }
    }

    private sealed class StateTargetComparer : IEqualityComparer<StateTarget>
    {
        public static readonly StateTargetComparer Instance = new();

        public bool Equals(StateTarget? x, StateTarget? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(x.Symbol, y.Symbol);
        }

        public int GetHashCode(StateTarget obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
    }
}
