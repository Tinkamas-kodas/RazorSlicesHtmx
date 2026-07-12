using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RazorSlicesHtmx.Generators;

[Generator]
public sealed class GenerateFormBinderGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "RazorSlicesHtmx.AspNetCore.Models.GenerateFormBinderAttribute";
    private const string ValidationResultInterfaceMetadataName = "global::RazorSlicesHtmx.AspNetCore.Models.IHaveValidationResult";
    private const string ErrorsPropertyName = "Errors";
    private const string UpsertRequestSuffix = "UpsertRequest";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            static (syntaxContext, _) =>
            {
                if (syntaxContext.Node is not TypeDeclarationSyntax typeDeclaration)
                {
                    return null;
                }

                if (!typeDeclaration.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                {
                    return null;
                }

                if (syntaxContext.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol symbol)
                {
                    return null;
                }

                if (symbol.IsAbstract || symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    return null;
                }

                if (!ShouldGenerate(symbol))
                {
                    return null;
                }

                if (HasExistingFormBinderMethods(symbol) || !HasPublicParameterlessConstructor(symbol))
                {
                    return null;
                }

                return new FormBinderTarget(symbol);
            })
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(targets.Collect(), static (spc, models) =>
        {
            if (models.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var model in models.Distinct(FormBinderTargetComparer.Instance))
            {
                EmitBinder(spc, model);
            }
        });
    }

    private static bool ShouldGenerate(INamedTypeSymbol symbol)
    {
        return HasGenerateFormBinderAttribute(symbol)
            || ImplementsValidationResultInterface(symbol)
            || symbol.Name.EndsWith(UpsertRequestSuffix, StringComparison.Ordinal);
    }

    private static bool HasGenerateFormBinderAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (string.Equals(attr.AttributeClass?.ToDisplayString(), AttributeMetadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }

            return false;
    }

    private static bool ImplementsValidationResultInterface(INamedTypeSymbol symbol)
    {
        foreach (var @interface in symbol.AllInterfaces)
        {
            if (string.Equals(@interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), ValidationResultInterfaceMetadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }

            return false;
    }

    private static bool HasExistingFormBinderMethods(INamedTypeSymbol symbol) =>
        symbol.GetMembers("BindAsync").OfType<IMethodSymbol>().Any(static m => m.IsStatic)
        || symbol.GetMembers("FromForm").OfType<IMethodSymbol>().Any(static m => m.IsStatic);

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol symbol) =>
        symbol.InstanceConstructors.Any(static ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0);

    private static IEnumerable<IPropertySymbol> CollectBindableProperties(INamedTypeSymbol symbol)
    {
            var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<IPropertySymbol>();

        INamedTypeSymbol? current = symbol;
        while (current is not null)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public
                    || property.IsStatic
                    || property.IsIndexer
                    || property.SetMethod is null)
                {
                    continue;
                }

                if (string.Equals(property.Name, ErrorsPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(property.Name))
                {
                    continue;
                }

                result.Add(property);
            }

            current = current.BaseType;
        }

        return result;
    }

    private static void EmitBinder(SourceProductionContext context, FormBinderTarget model)
    {
        var bindableProperties = CollectBindableProperties(model.Symbol).ToArray();
        if (bindableProperties.Length == 0)
        {
            return;
        }

        var ns = model.Symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.Symbol.ContainingNamespace.ToDisplayString();

        var typeName = model.Symbol.Name;
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
            .AppendLine()
            .AppendLine("{");

            AppendFromFormMethod(source, typeName, bindableProperties);
        source.AppendLine();
        AppendBindAsyncMethod(source, typeName);
        source.AppendLine();
        AppendParseHelpers(source);

        source.AppendLine("}");

        var nsSafe = string.IsNullOrWhiteSpace(ns) ? "Global" : ns!.Replace('.', '_');
        var hintName = nsSafe + "_" + typeName + ".FormBinder.g.cs";
        context.AddSource(hintName, SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void AppendFromFormMethod(StringBuilder source, string typeName, IReadOnlyList<IPropertySymbol> properties)
    {
        source.Append("    public static async global::System.Threading.Tasks.ValueTask<").Append(typeName).AppendLine("> FromForm(global::Microsoft.AspNetCore.Http.HttpContext context)");
        source.AppendLine("    {");
        source.AppendLine("        var form = await context.Request.ReadFormAsync();");
        source.Append("        var model = new ").Append(typeName).AppendLine("();");

        foreach (var property in properties)
        {
            var bindingExpression = GetBindingExpression(property);
            if (string.IsNullOrWhiteSpace(bindingExpression))
            {
                continue;
            }

            source.Append("        model.").Append(property.Name).Append(" = ").Append(bindingExpression).AppendLine(";");
        }

        source.AppendLine("        return model;");
        source.AppendLine("    }");
    }

    private static void AppendBindAsyncMethod(StringBuilder source, string typeName)
    {
        source.Append("    public static global::System.Threading.Tasks.ValueTask<").Append(typeName).AppendLine("?> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context)");
        source.AppendLine("        => BindAsync(context, null);");
        source.AppendLine();
        source.Append("    public static async global::System.Threading.Tasks.ValueTask<").Append(typeName).AppendLine("?> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context, global::System.Reflection.ParameterInfo? parameter)");
        source.AppendLine("    {");
        source.AppendLine("        return await FromForm(context);");
        source.AppendLine("    }");
    }

    private static string? GetBindingExpression(IPropertySymbol property)
    {
        var type = property.Type;
        var name = property.Name;

        if (type.SpecialType == SpecialType.System_String)
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated
                ? $"ReadNullableString(form, \"{Escape(name)}\")"
                : $"ReadString(form, \"{Escape(name)}\")";
        }

        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
        {
            var underlying = named.TypeArguments[0];
            return GetNullableValueExpression(underlying, name);
        }

        return GetNonNullableValueExpression(type, name);
    }

    private static string? GetNullableValueExpression(ITypeSymbol type, string propertyName)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return $"ReadNullableString(form, \"{Escape(propertyName)}\")";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"ReadNullableEnum<{enumTypeName}>(form, \"{Escape(propertyName)}\")";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => $"ReadNullableBoolean(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Byte => $"ReadNullableByte(form, \"{Escape(propertyName)}\")",
            SpecialType.System_SByte => $"ReadNullableSByte(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int16 => $"ReadNullableInt16(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt16 => $"ReadNullableUInt16(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int32 => $"ReadNullableInt32(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt32 => $"ReadNullableUInt32(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int64 => $"ReadNullableInt64(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt64 => $"ReadNullableUInt64(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Single => $"ReadNullableSingle(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Double => $"ReadNullableDouble(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Decimal => $"ReadNullableDecimal(form, \"{Escape(propertyName)}\")",
            _ when IsGuid(type) => $"ReadNullableGuid(form, \"{Escape(propertyName)}\")",
            _ when IsDateTime(type) => $"ReadNullableDateTime(form, \"{Escape(propertyName)}\")",
            _ when IsDateOnly(type) => $"ReadNullableDateOnly(form, \"{Escape(propertyName)}\")",
            _ when IsTimeOnly(type) => $"ReadNullableTimeOnly(form, \"{Escape(propertyName)}\")",
            _ => null
        };
    }

    private static string? GetNonNullableValueExpression(ITypeSymbol type, string propertyName)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"ReadEnum<{enumTypeName}>(form, \"{Escape(propertyName)}\")";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => $"ReadBoolean(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Byte => $"ReadByte(form, \"{Escape(propertyName)}\")",
            SpecialType.System_SByte => $"ReadSByte(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int16 => $"ReadInt16(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt16 => $"ReadUInt16(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int32 => $"ReadInt32(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt32 => $"ReadUInt32(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Int64 => $"ReadInt64(form, \"{Escape(propertyName)}\")",
            SpecialType.System_UInt64 => $"ReadUInt64(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Single => $"ReadSingle(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Double => $"ReadDouble(form, \"{Escape(propertyName)}\")",
            SpecialType.System_Decimal => $"ReadDecimal(form, \"{Escape(propertyName)}\")",
            _ when IsGuid(type) => $"ReadGuid(form, \"{Escape(propertyName)}\")",
            _ when IsDateTime(type) => $"ReadDateTime(form, \"{Escape(propertyName)}\")",
            _ when IsDateOnly(type) => $"ReadDateOnly(form, \"{Escape(propertyName)}\")",
            _ when IsTimeOnly(type) => $"ReadTimeOnly(form, \"{Escape(propertyName)}\")",
            _ => null
        };
    }

    private static bool IsGuid(ITypeSymbol type) =>
        type.Name == "Guid" && type.ContainingNamespace.ToDisplayString() == "System";

    private static bool IsDateTime(ITypeSymbol type) =>
        type.Name == "DateTime" && type.ContainingNamespace.ToDisplayString() == "System";

    private static bool IsDateOnly(ITypeSymbol type) =>
        type.Name == "DateOnly" && type.ContainingNamespace.ToDisplayString() == "System";

    private static bool IsTimeOnly(ITypeSymbol type) =>
        type.Name == "TimeOnly" && type.ContainingNamespace.ToDisplayString() == "System";

    private static void AppendParseHelpers(StringBuilder source)
    {
        source.AppendLine("    private static string ReadString(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => form.TryGetValue(key, out var values) ? values.ToString() : string.Empty;");
        source.AppendLine();

        source.AppendLine("    private static string? ReadNullableString(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : value;");
        source.AppendLine("    }");
        source.AppendLine();

        AppendBooleanHelpers(source);
        AppendNumericHelpers(source);
        AppendGuidHelpers(source);
        AppendDateHelpers(source);
        AppendEnumHelpers(source);
    }

    private static void AppendBooleanHelpers(StringBuilder source)
    {
        source.AppendLine("    private static bool ReadBoolean(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => bool.TryParse(ReadString(form, key), out var parsed) && parsed;");
        source.AppendLine();

        source.AppendLine("    private static bool? ReadNullableBoolean(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : bool.TryParse(value, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendNumericHelpers(StringBuilder source)
    {
        AppendNumericHelper(source, "Byte", "byte", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "SByte", "sbyte", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Int16", "short", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "UInt16", "ushort", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Int32", "int", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "UInt32", "uint", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Int64", "long", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "UInt64", "ulong", "global::System.Globalization.NumberStyles.Integer", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Single", "float", "global::System.Globalization.NumberStyles.Float | global::System.Globalization.NumberStyles.AllowThousands", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Double", "double", "global::System.Globalization.NumberStyles.Float | global::System.Globalization.NumberStyles.AllowThousands", "global::System.Globalization.CultureInfo.InvariantCulture");
        AppendNumericHelper(source, "Decimal", "decimal", "global::System.Globalization.NumberStyles.Number", "global::System.Globalization.CultureInfo.InvariantCulture");
    }

    private static void AppendNumericHelper(StringBuilder source, string suffix, string typeName, string numberStyles, string culture)
    {
        source.Append("    private static ").Append(typeName).Append(" Read").Append(suffix)
            .AppendLine("(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.Append("        => ").Append(typeName).Append(".TryParse(ReadString(form, key), ").Append(numberStyles).Append(", ").Append(culture).Append(", out var parsed) ? parsed : default;").AppendLine();
        source.AppendLine();

        source.Append("    private static ").Append(typeName).Append("? ReadNullable").Append(suffix)
            .AppendLine("(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.Append("        return string.IsNullOrWhiteSpace(value) ? null : ").Append(typeName).Append(".TryParse(value, ").Append(numberStyles).Append(", ").Append(culture).Append(", out var parsed) ? parsed : null;").AppendLine();
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendGuidHelpers(StringBuilder source)
    {
        source.AppendLine("    private static global::System.Guid ReadGuid(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => global::System.Guid.TryParse(ReadString(form, key), out var parsed) ? parsed : default;");
        source.AppendLine();

        source.AppendLine("    private static global::System.Guid? ReadNullableGuid(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : global::System.Guid.TryParse(value, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendDateHelpers(StringBuilder source)
    {
        source.AppendLine("    private static global::System.DateTime ReadDateTime(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => global::System.DateTime.TryParse(ReadString(form, key), global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : default;");
        source.AppendLine();

        source.AppendLine("    private static global::System.DateTime? ReadNullableDateTime(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : global::System.DateTime.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();

        source.AppendLine("    private static global::System.DateOnly ReadDateOnly(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => global::System.DateOnly.TryParse(ReadString(form, key), global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : default;");
        source.AppendLine();

        source.AppendLine("    private static global::System.DateOnly? ReadNullableDateOnly(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : global::System.DateOnly.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();

        source.AppendLine("    private static global::System.TimeOnly ReadTimeOnly(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("        => global::System.TimeOnly.TryParse(ReadString(form, key), global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : default;");
        source.AppendLine();

        source.AppendLine("    private static global::System.TimeOnly? ReadNullableTimeOnly(global::Microsoft.AspNetCore.Http.IFormCollection form, string key)");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : global::System.TimeOnly.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static void AppendEnumHelpers(StringBuilder source)
    {
        source.AppendLine("    private static TEnum ReadEnum<TEnum>(global::Microsoft.AspNetCore.Http.IFormCollection form, string key) where TEnum : struct, global::System.Enum");
        source.AppendLine("        => global::System.Enum.TryParse<TEnum>(ReadString(form, key), ignoreCase: true, out var parsed) ? parsed : default;");
        source.AppendLine();

        source.AppendLine("    private static TEnum? ReadNullableEnum<TEnum>(global::Microsoft.AspNetCore.Http.IFormCollection form, string key) where TEnum : struct, global::System.Enum");
        source.AppendLine("    {");
        source.AppendLine("        var value = ReadNullableString(form, key);");
        source.AppendLine("        return string.IsNullOrWhiteSpace(value) ? null : global::System.Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class FormBinderTarget(INamedTypeSymbol symbol)
    {
        public INamedTypeSymbol Symbol { get; } = symbol;
    }

    private sealed class FormBinderTargetComparer : IEqualityComparer<FormBinderTarget>
    {
        public static readonly FormBinderTargetComparer Instance = new();

        public bool Equals(FormBinderTarget? x, FormBinderTarget? y)
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

        public int GetHashCode(FormBinderTarget obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
    }
}