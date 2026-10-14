namespace Apskaita.Entities.Mapping;

/// <summary>
/// Helpers for mapping .NET enums to native MySQL <c>enum</c> columns.
/// </summary>
public static class MySqlEnumTypeExtensions
{
    /// <summary>
    /// Builds a MySQL <c>enum(...)</c> column definition listing the member names of <typeparamref name="T"/>,
    /// e.g. <c>enum('Draft', 'Final', 'Invoiced')</c>. Combine with <c>HasConversion&lt;string&gt;()</c> so
    /// EF Core reads and writes the enum member names instead of integers.
    /// The receiver value is irrelevant; only its type is used.
    /// </summary>
    public static string ToMySqlDefinition<T>(this T value)
        where T : struct, Enum =>
        $"enum({string.Join(", ", Enum.GetNames(typeof(T)).Select(name => $"'{name}'"))})";
}
