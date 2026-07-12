using RazorSlicesHtmx.HtmlNames;

namespace RazorSlicesHtmx.AspNetCore.HtmlNames;

/// <summary>
/// Combines HTML field metadata with the property value and optional validation errors.
/// Implements <see cref="IPropertyAccessor"/> for use in generic form helpers.
/// </summary>
public readonly struct FieldContext<T>(
    string name,
    string path,
    string id,
    T value,
    IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]>? errors = null)
    : IPropertyAccessor
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public string Id { get; } = id;
    public T Value { get; } = value;
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]>? Errors { get; } = errors;

    /// <summary>
    /// Returns the validation error messages for this field, or an empty array if none.
    /// </summary>
    public (string errorCode, string errorMessage)[] FieldErrors =>
        Errors is not null && Errors.TryGetValue(Name, out var messages) ? messages : [];

    /// <summary>
    /// Returns true if this field has validation errors.
    /// </summary>
    public bool HasErrors => FieldErrors.Length > 0;
}
