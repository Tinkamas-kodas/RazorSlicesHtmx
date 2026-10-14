namespace RazorSlicesHtmx.AspNetCore.Html;

/// <summary>
/// Marks a model class or record for <c>HtmlNames</c> generation: the <c>GenerateHtmlNamesGenerator</c>
/// emits a compile-time-safe hierarchy of form field name/id/path constants (e.g. <c>For.Code.Name</c>).
/// Declared once in <c>RazorSlicesHtmx.AspNetCore</c> so every consuming assembly shares the same type
/// instead of each getting its own generated copy.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateHtmlNamesAttribute : Attribute
{
    public GenerateHtmlNamesAttribute(int maxDepth = 0)
    {
        MaxDepth = maxDepth < 0 ? 0 : maxDepth;
    }

    /// <summary>Nesting depth the generator walks when collecting field entries.</summary>
    public int MaxDepth { get; }
}
