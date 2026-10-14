namespace RazorSlicesHtmx.AspNetCore.Html;

/// <summary>
/// Provides access to HTML field metadata (name, path, id) for a model property.
/// Implemented by source-generated accessor structs.
/// </summary>
public interface IPropertyAccessor
{
    string Name { get; }
    string Path { get; }
    string Id { get; }
}
