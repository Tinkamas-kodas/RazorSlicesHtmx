namespace RazorSlicesHtmx.HtmlNames;

/// <summary>
/// Combines HTML field metadata with the property value and optional validation errors.
/// Implements <see cref="IPropertyAccessor"/> for use in generic form helpers.
/// </summary>
public readonly struct FieldContext<T> : IPropertyAccessor
{
    public FieldContext(string name, string path, string id, T value,
        IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]>? errors = null)
    {
        Name = name;
        Path = path;
        Id = id;
        Value = value;
        Errors = errors;
    }

    public string Name { get; }
    public string Path { get; }
    public string Id { get; }
    public T Value { get; }
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]>? Errors { get; }

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
