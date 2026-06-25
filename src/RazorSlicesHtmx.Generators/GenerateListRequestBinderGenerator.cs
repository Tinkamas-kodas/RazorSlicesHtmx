using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlicesHtmx.Generators;

[Generator]
public sealed class GenerateListRequestBinderGenerator : IIncrementalGenerator
{
    private const string ListRequestTypeName = "ListRequest";
    private const string ModelsNamespace = "RazorSlicesHtmx.AspNetCore.Models";
    private const string SortDisableAttributeMetadataName = "RazorSlicesHtmx.AspNetCore.Models.SortDisableAttribute";
    private const string CustomSortExpressionAttributeMetadataName = "RazorSlicesHtmx.AspNetCore.Models.CustomSortExpressionAttribute";
    private const string SortExpressionAttributeMetadataName = "RazorSlicesHtmx.AspNetCore.Models.SortExpressionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) =>
            {
                if (syntaxContext.Node is not ClassDeclarationSyntax classDeclaration)
                {
                    return null;
                }

                if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    return null;
                }

                INamedTypeSymbol? symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(classDeclaration);
                if (symbol is null)
                {
                    return null;
                }

                if (symbol.IsAbstract || !DerivesFromListRequest(symbol))
                {
                    return null;
                }

                if (HasFromQuery(symbol))
                {
                    return null;
                }

                var options = BinderOptions.FromSymbol(symbol);
                return new BinderTarget(symbol, options);
            })
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(targets.Collect(), static (spc, models) =>
        {
            if (models.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var model in models.Distinct(BinderTargetComparer.Instance))
            {
                EmitBinder(spc, model);
            }
        });
    }

    private static bool DerivesFromListRequest(INamedTypeSymbol symbol)
    {
        INamedTypeSymbol? current = symbol;
        while (current is not null)
        {
            if (string.Equals(current.Name, ListRequestTypeName, StringComparison.Ordinal)
                && current.Arity == 1
                && string.Equals(current.ContainingNamespace.ToDisplayString(), ModelsNamespace, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasFromQuery(INamedTypeSymbol symbol) =>
        symbol.GetMembers("FromQuery").OfType<IMethodSymbol>().Any(static m => m.IsStatic);

    private static bool HasSortMapOverride(INamedTypeSymbol symbol) =>
        symbol.GetMembers("SortMap").OfType<IPropertySymbol>().Any(static p => !p.IsStatic);

    private static ITypeSymbol? GetListItemType(INamedTypeSymbol symbol)
    {
        INamedTypeSymbol? current = symbol;
        while (current is not null)
        {
            if (string.Equals(current.Name, ListRequestTypeName, StringComparison.Ordinal)
                && current.Arity == 1
                && string.Equals(current.ContainingNamespace.ToDisplayString(), ModelsNamespace, StringComparison.Ordinal))
            {
                return current.TypeArguments[0];
            }

            current = current.BaseType;
        }

        return null;
    }

    private static IEnumerable<IPropertySymbol> GetSortableProperties(ITypeSymbol? rowType)
    {
        if (rowType is not INamedTypeSymbol named)
        {
            return Enumerable.Empty<IPropertySymbol>();
        }

        return named.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p =>
                p.DeclaredAccessibility == Accessibility.Public
                && !p.IsStatic
                && !p.IsIndexer
                && p.GetMethod is not null
                && p.Parameters.Length == 0)
            .Where(p => IsSortableType(p.Type) && !HasSortDisableAttribute(p));
    }

    private static bool HasSortDisableAttribute(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (string.Equals(fullName, SortDisableAttributeMetadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetSortExpressionBody(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (!string.Equals(fullName, SortExpressionAttributeMetadataName, StringComparison.Ordinal))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not string raw || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return NormalizeExpression(raw);
        }

        return null;
    }

    private static string? GetCustomSortMethodName(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (!string.Equals(fullName, CustomSortExpressionAttributeMetadataName, StringComparison.Ordinal))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length == 0
                || attr.ConstructorArguments[0].Value is not string methodName
                || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            return methodName.Trim();
        }

        return null;
    }

    private static string NormalizeExpression(string raw)
    {
        var trimmed = raw.Trim();
        var arrowIndex = trimmed.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIndex < 0)
        {
            return trimmed;
        }

        var parameter = trimmed.Substring(0, arrowIndex).Trim().Trim('(', ')');
        var body = trimmed.Substring(arrowIndex + 2).Trim();

        if (string.IsNullOrWhiteSpace(parameter)
            || string.Equals(parameter, "row", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        return Regex.Replace(body, $@"\b{Regex.Escape(parameter)}\b", "row");
    }

    private static bool IsSortableType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return IsSortableType(named.TypeArguments[0]);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        return type.SpecialType is SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_DateTime;
    }

    private static void EmitBinder(SourceProductionContext context, BinderTarget model)
    {
        var ns = model.Symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.Symbol.ContainingNamespace.ToDisplayString();

        var typeName = model.Symbol.Name;
        var defaultSortByLiteral = Escape(model.Options.DefaultSortBy);
        var rowType = GetListItemType(model.Symbol);
        var hasSortMapOverride = HasSortMapOverride(model.Symbol);
        var sortableProperties = GetSortableProperties(rowType);

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        if (!string.IsNullOrWhiteSpace(ns))
        {
            source.Append("namespace ").Append(ns).AppendLine(";");
            source.AppendLine();
        }

        source.Append("public sealed partial class ").Append(typeName)
            .Append(" : global::Microsoft.AspNetCore.Http.IBindableFromHttpContext<")
            .Append(typeName).AppendLine(">")
            .AppendLine("{");

        if (!hasSortMapOverride && rowType is not null)
        {
            var rowTypeName = rowType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            source.Append("    public override global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Linq.Expressions.Expression<global::System.Func<")
                .Append(rowTypeName)
                .AppendLine(", object?>>> SortMap { get; } =")
                .Append("        new global::System.Collections.Generic.Dictionary<string, global::System.Linq.Expressions.Expression<global::System.Func<")
                .Append(rowTypeName)
                .AppendLine(", object?>>>(global::System.StringComparer.OrdinalIgnoreCase)")
                .AppendLine("        {");

            foreach (var property in sortableProperties)
            {
                var key = property.Name.ToLowerInvariant();
                var methodName = GetCustomSortMethodName(property);
                if (!string.IsNullOrWhiteSpace(methodName))
                {
                    source.Append("            [\"").Append(Escape(key)).Append("\"] = ")
                        .Append(rowTypeName)
                        .Append(".")
                        .Append(methodName)
                        .AppendLine("(),");
                    continue;
                }

                var expressionBody = GetSortExpressionBody(property);
                if (string.IsNullOrWhiteSpace(expressionBody))
                {
                    expressionBody = "row." + property.Name;
                }

                source.Append("            [\"").Append(Escape(key)).Append("\"] = row => ")
                    .Append(expressionBody)
                    .AppendLine(",");
            }

            source.AppendLine("        };")
                .AppendLine();
        }

        source.Append("    public static ").Append(typeName)
            .AppendLine(" FromQuery(global::Microsoft.AspNetCore.Http.IQueryCollection query)")
            .AppendLine("    {")
            .Append("        var common = ReadCommonQuery(query, defaultPage: ").Append(model.Options.DefaultPage)
            .Append(", defaultPageSize: ").Append(model.Options.DefaultPageSize)
            .Append(", maxPageSize: ").Append(model.Options.MaxPageSize).AppendLine(");")
            .Append("        var normalizedSortBy = NormalizeSortBy(common.SortBy, new ")
            .Append(typeName)
            .Append("().SortMap.Keys, \"")
            .Append(defaultSortByLiteral).AppendLine("\");")
            .Append("        return new ").Append(typeName).AppendLine()
            .AppendLine("        {")
            .AppendLine("            SortBy = normalizedSortBy,")
            .AppendLine("            SortDirection = common.SortDirection,")
            .AppendLine("            Page = common.Page,")
            .AppendLine("            PageSize = common.PageSize")
            .AppendLine("        };")
            .AppendLine("    }")
            .AppendLine()
            .Append("    public static global::System.Threading.Tasks.ValueTask<").Append(typeName)
            .AppendLine("?> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context, global::System.Reflection.ParameterInfo parameter)")
            .Append("        => global::System.Threading.Tasks.ValueTask.FromResult<").Append(typeName)
            .AppendLine("?>(FromQuery(context.Request.Query));")
            .AppendLine("}");

        var nsSafe = string.IsNullOrWhiteSpace(ns) ? "Global" : ns!.Replace('.', '_');
        var hintName = nsSafe + "_" + typeName + ".ListRequestBinder.g.cs";
        context.AddSource(hintName, SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class BinderTarget
    {
        public BinderTarget(INamedTypeSymbol symbol, BinderOptions options)
        {
            Symbol = symbol;
            Options = options;
        }

        public INamedTypeSymbol Symbol { get; }

        public BinderOptions Options { get; }
    }

    private sealed class BinderOptions
    {
        public BinderOptions(string searchQueryKey, string defaultSortBy, int defaultPage, int defaultPageSize, int maxPageSize)
        {
            SearchQueryKey = searchQueryKey;
            DefaultSortBy = defaultSortBy;
            DefaultPage = defaultPage;
            DefaultPageSize = defaultPageSize;
            MaxPageSize = maxPageSize;
        }

        public string SearchQueryKey { get; }

        public string DefaultSortBy { get; }

        public int DefaultPage { get; }

        public int DefaultPageSize { get; }

        public int MaxPageSize { get; }

        public static BinderOptions FromSymbol(INamedTypeSymbol symbol)
        {
            var defaultSortBy = GetConstString(symbol, "DefaultSortBy") ?? "code";
            var defaultPage = GetConstInt(symbol, "DefaultPage") ?? 1;
            var defaultPageSize = GetConstInt(symbol, "DefaultPageSize") ?? 10;
            var maxPageSize = GetConstInt(symbol, "MaxPageSize") ?? 50;
            var searchQueryKey = "Search";

            if (defaultPage < 1)
            {
                defaultPage = 1;
            }

            if (defaultPageSize < 1)
            {
                defaultPageSize = 10;
            }

            if (maxPageSize < 1)
            {
                maxPageSize = 50;
            }

            return new BinderOptions(searchQueryKey, defaultSortBy, defaultPage, defaultPageSize, maxPageSize);
        }

        private static string? GetConstString(INamedTypeSymbol symbol, string fieldName)
        {
            var field = symbol.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault();
            return field is { IsConst: true } && field.ConstantValue is string s && !string.IsNullOrWhiteSpace(s)
                ? s
                : null;
        }

        private static int? GetConstInt(INamedTypeSymbol symbol, string fieldName)
        {
            var field = symbol.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault();
            return field is { IsConst: true } && field.ConstantValue is int i ? i : null;
        }
    }

    private sealed class BinderTargetComparer : IEqualityComparer<BinderTarget>
    {
        public static readonly BinderTargetComparer Instance = new();

        public bool Equals(BinderTarget? x, BinderTarget? y)
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

        public int GetHashCode(BinderTarget obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
    }
}
